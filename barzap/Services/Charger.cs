using barzap.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace barzap.Services {

    public class Charger {

        private readonly ILogger<Charger> _Logger;
        private readonly MatchManager _Match;
        private readonly Bt _Bt;

        private readonly PeriodicTimer _RefreshTimer;
        private readonly Thread _Thread;

        private decimal _Value;
        private DateTime? _ChargeLocked;

        public Charger(ILogger<Charger> logger, MatchManager match, Bt bt) {
            _Logger = logger;
            _Match = match;
            _Bt = bt;

            _RefreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            _Thread = new Thread(async () => {
                try {
                    while (await _RefreshTimer.WaitForNextTickAsync()) {
                        if (_ChargeLocked.HasValue && _ChargeLocked.Value < DateTime.UtcNow) {
                            int shockPower = Math.Max(1, (int)(_Value / 10m));
                            _Logger.LogInformation($"delivering shock! [power={shockPower}] [value={_Value:N2}]");

                            _Value = 0m;
                            _ChargeLocked = null;

                            await _Bt.Shock(shockPower);
                        }

                        decimal power = (decimal)_Match.Get().Energy.Current / Math.Max(1, _Match.Get().Energy.Max);

                        IsDischarging = Settings.Instance.DoDischarge == true
                            && (Settings.Instance.DoesLowPowerStopDischarge == false || power >= Settings.Instance.LowPowerStopDischargeValue);

                        if (IsDischarging == true && _Value > 0m) {
                            _Value -= DischargeRate;
                        }

                        if (Settings.Instance.PassiveChargeRate > 0m) {
                            _Value += Settings.Instance.PassiveChargeRate;
                        }

                        _Value = Math.Max(0m, _Value);

                    }
                } catch (Exception ex) {
                    _Logger.LogError(ex, "error in refresh thread");
                }
            });
            _Thread.Start();
        }

        public decimal DischargeRate {
            get {
                decimal power = (decimal)_Match.Get().Energy.Current / Math.Max(1, _Match.Get().Energy.Max);
                return Settings.Instance.ScaleDischarge == true
                    ? (Settings.Instance.MinimumDischargeRate * (1 - power) + Settings.Instance.MaximumDischargeRate * power)
                    : Settings.Instance.MinimumDischargeRate;
            }
        }

        public void AddCharge(decimal v) {
            _Value += v;

            //if (_ChargeLocked.HasValue == false && _Value > 10m) {
            if (_Value > 10m) {
                if (_ChargeLocked.HasValue == false) {
                    _Logger.LogInformation($"charge locked");
                }
                _ChargeLocked = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            }
        }

        public void RemoveCharge(decimal v) {
            _Value -= v;
            if (_Value < 0m) {
                _Value = 0m;
            }
        }

        public bool IsDischarging { get; private set; }

        public bool IsLocked() {
            return _ChargeLocked.HasValue;
        }

        public decimal Get() {
            return _Value;
        }

        public decimal GetShockStrength() {
            return Math.Max(0m, _Value - 10m);
        }
        
    }

}
