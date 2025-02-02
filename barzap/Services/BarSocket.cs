using barzap.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace barzap.Services {

    public class BarSocket : BackgroundService {

        private readonly ILogger<BarSocket> _Logger;
        private readonly PacketQueue _PacketQueue;
        private readonly ConnectionCount _ConnectionCount;

        private readonly TcpListener _TcpListener;

        private int _ThreadCount = 0;

        private const int HOST_PORT = 41666;

        public BarSocket(ILogger<BarSocket> logger, PacketQueue packetQueue,
            ConnectionCount connectionCount) {
            _Logger = logger;
            _PacketQueue = packetQueue;
            _ConnectionCount = connectionCount;

            _TcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), HOST_PORT);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) {
            return Task.Run(async () => {
                _TcpListener.Start();
                _Logger.LogInformation($"tcp socket opened [port={HOST_PORT}]");

                while (stoppingToken.IsCancellationRequested == false) {
                    TcpClient client = await _TcpListener.AcceptTcpClientAsync(stoppingToken);
                    _ConnectionCount.Value++;

                    Thread clientThread = new(() => {
                        ThreadMain(client, _PacketQueue, stoppingToken, _ThreadCount++);
                    });

                    clientThread.Start();
                    _Logger.LogInformation($"tcp client connected [client={client.Client.RemoteEndPoint?.ToString()}]");
                }

                _Logger.LogInformation($"stopping tcp socket");
                _TcpListener.Stop();
            }, stoppingToken);
        }

        public void ThreadMain(TcpClient client, PacketQueue queue, CancellationToken cancel, int threadId) {
            _Logger.LogInformation($"client thread started [thread id={threadId}]");
            NetworkStream stream = client.GetStream();

            byte[] readBuffer = new byte[1024];

            List<byte> buffer = new();

            DateTime lastRecv = DateTime.UtcNow;

            while (cancel.IsCancellationRequested == false) {
                while (!stream.DataAvailable) {
                    cancel.ThrowIfCancellationRequested();

                    if (DateTime.UtcNow - lastRecv > TimeSpan.FromSeconds(5)) {
                        _Logger.LogWarning($"socket timeout [thread id={threadId}]");
                        client.Close();
                    }

                    if (client.Connected == false) {
                        break;
                    }
                }

                if (client.Connected == false) {
                    _Logger.LogInformation($"client disconncted [thread id={threadId}]");
                    break;
                }

                int readCount = stream.Read(readBuffer, 0, 1024);
                lastRecv = DateTime.UtcNow;

                buffer.AddRange(readBuffer.AsSpan()[..readCount]);

                _Logger.LogTrace($"tcp read [amt={readCount}] [str={string.Join("", readBuffer[0..readCount].Select(iter => (char)iter))}]");

                while (cancel.IsCancellationRequested == false) {
                    cancel.ThrowIfCancellationRequested();

                    Packet? p = parsePacket(buffer);

                    if (p == null) {
                        break;
                    }

                    queue.Queue(p);

                    if (buffer.Count == 0) {
                        break;
                    }
                }
            }

            _Logger.LogInformation($"exiting tcp thread [thread id={threadId}]");
            _ConnectionCount.Value--;
        }

        public Packet? parsePacket(List<byte> buffer) {
            Packet p = new() {
                Op = "",
                DataSize = 0,
                Data = ""
            };

            bool sizeDone = false;
            string size = "";
            int i = 0;
            for (i = 0; i < buffer.Count; ++i) {
                if (i < 2) {
                    p.Op += (char)buffer[i];
                    continue;
                }

                if (buffer[i] == ';') {
                    sizeDone = true;
                    break;
                }

                size += (char)buffer[i];
            }

            if (sizeDone == false) {
                Console.WriteLine($"no size");
                return null;
            }
            //Console.WriteLine($"size is {size}");

            int dataSize = int.Parse(size);
            //Console.WriteLine($"dsize is {dataSize}");
            p.DataSize = dataSize;
            int packetSize = i + dataSize + 1;
            if (buffer.Count < packetSize) {
                _Logger.LogDebug($"not enough data for a full packet [want={packetSize}] [have={buffer.Count}]");
                return null;
            }

            byte[] packet = buffer[..packetSize].ToArray();
            p.Data = Encoding.ASCII.GetString(packet[(i + 1)..]);
            buffer.RemoveRange(0, packetSize);

            if (p.Data.Length != p.DataSize) {
                _Logger.LogWarning($"packet error, wrong size! [expected={p.DataSize}] [actual={p.Data.Length}]");
                return null;
            }

            _Logger.LogTrace($"packet parsed [op={p.Op}] [size={p.DataSize}] [data={p.Data}]");

            return p;
        }

    }
}
