using barzap.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace barzap.Services {

    public class MatchManager {

        private readonly ILogger<MatchManager> _Logger;

        private readonly BarMatch _Match;

        public MatchManager(ILogger<MatchManager> logger) {
            _Logger = logger;

            _Match = new BarMatch();
        }

        public BarMatch Get() {
            return _Match;
        }

    }
}
