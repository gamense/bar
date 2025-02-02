using barzap.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Timers;

namespace barzap {

    public class Vibrate : IDisposable {

        private readonly ILogger<Vibrate> _Logger;

        private readonly Timer _Timer;
        private DateTime _LastEvent;
        
        private int _CurrentStrength = 0;
        private int _CurrentLevel = 0;

        public Vibrate(ILogger<Vibrate> logger) {
            _Logger = logger;

            _LastEvent = DateTime.UtcNow;

            _Timer = new Timer();
            _Timer.AutoReset = true;
            _Timer.Interval = 1000;
            _Timer.Elapsed += _TimerElapsed;
            _Timer.Start();
        }

        public void Dispose() {
            _Timer.Elapsed -= _TimerElapsed;
        }

        private void _TimerElapsed(object? sender, ElapsedEventArgs args) {
            TimeSpan diffSpan = args.SignalTime.ToUniversalTime() - _LastEvent;
            int diff = (int)diffSpan.TotalSeconds;
        }

        /// <summary>
        ///     Get the value 0-100 that represents the current strength of the toy.
        /// </summary>
        public int GetCurrentStrength() {
            return _CurrentStrength;
        }

        /// <summary>
        ///     Get the current vibration level
        /// </summary>
        public int GetCurrentLevel() {
            return _CurrentLevel;
        }

    }
}
