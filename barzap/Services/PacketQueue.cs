using barzap.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace barzap.Services {

    public class PacketQueue : BaseQueue<Packet> {

        public PacketQueue(ILoggerFactory factory) : base(factory) { }

    }
}
