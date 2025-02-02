using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace barzap.Services {

    public abstract class BaseQueueHandler<T> : BackgroundService {

        protected readonly ILogger _Logger;
        private readonly BaseQueue<T> _Queue;

        /// <summary>
        ///     Name of the service. Can be disabled using commands
        /// </summary>
        private readonly string _ServiceName;

        private Stopwatch _RunTimer = Stopwatch.StartNew();

        public BaseQueueHandler(string serviceName,
            ILoggerFactory factory, BaseQueue<T> queue) {

            _ServiceName = serviceName;

            _Logger = factory.CreateLogger($"barzap.Services.BaseQueueHandler<{typeof(T).Name}>");
            _Logger.LogInformation($"created queue handler [type={typeof(T).Name}]");

            _Queue = queue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            _Logger.LogInformation($"started [service name={_ServiceName}]");

            while (stoppingToken.IsCancellationRequested == false) {
                try {
                    T queueEntry = await _Queue.Dequeue(stoppingToken);

                    _RunTimer.Restart();
                    bool toRecord = await _ProcessQueueEntry(queueEntry, stoppingToken);
                    long timeToProcess = _RunTimer.ElapsedMilliseconds;

                    if (toRecord == true) {
                        _Queue.AddProcessTime(timeToProcess);
                    }
                } catch (Exception ex) {
                    _Logger.LogError(ex, $"error in queue processor {_ServiceName}");
                }
            }

            _Logger.LogInformation($"stopping");
        }

        /// <summary>
        ///     Process a queue entry, returning true if the duration of processing that entry is to be recorded
        /// </summary>
        /// <param name="entry">Queue entry to be processed</param>
        /// <param name="cancel">Stopping token</param>
        /// <returns>
        ///     A boolean value indicating if the time it took to process this entry will be recorded
        /// </returns>
        protected abstract Task<bool> _ProcessQueueEntry(T entry, CancellationToken cancel);

    }
}
