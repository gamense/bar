using barzap.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace barzap.Panels {

    /// <summary>
    /// Interaction logic for ConnectPanel.xaml
    /// </summary>
    public partial class ConnectPanel : UserControl {

        private readonly ILogger<ConnectPanel> _Logger;
        private readonly MatchManager _Match;
        private readonly Charger _Charger;
        private readonly ConnectionCount _ConnectionCount;
        private readonly Bt _Bt;

        private readonly PeriodicTimer _RefreshTimer;
        private readonly Thread _RefreshThread;

        public ConnectPanel() {
            InitializeComponent();

            _Logger = App.Services.GetRequiredService<ILogger<ConnectPanel>>();
            _Match = App.Services.GetRequiredService<MatchManager>();
            _Charger = App.Services.GetRequiredService<Charger>();
            _ConnectionCount = App.Services.GetRequiredService<ConnectionCount>();
            _Bt = App.Services.GetRequiredService<Bt>();

            App_Status.Text = "Status: idle";

            _RefreshTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            _RefreshThread = new Thread(async () => {
                try {
                    while (await _RefreshTimer.WaitForNextTickAsync()) {
                        Application.Current.Dispatcher.Invoke(delegate {

                            if (_ConnectionCount.Value > 0) {
                                App_Status.Text = "Status: Connected";
                            } else {
                                App_Status.Text = "Status: Not connected";
                            }

                            MetalAmount.Text = $"{_Match.Get().TotalMetal:N0}";
                            EnergyAmount.Text = $"{_Match.Get().TotalEnergy:N0}";

                            double chargeProgress = (double)_Charger.Get() / 10d * 100d;

                            ChargeBar.Value = chargeProgress;
                            ChargeProgress.Text = $"{Math.Min(100d, chargeProgress):F2}%";

                            if (_Charger.IsLocked()) {
                                ChargeBar.Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                            } else if (ChargeBar.Value > 50d) {
                                ChargeBar.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 0));
                            } else if (ChargeBar.Value > 80d) {
                                ChargeBar.Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 0));
                            } else {
                                ChargeBar.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                            }

                            ShockAmount.Text = $"{_Charger.GetShockStrength():N2}%";

                            if (_ConnectionCount.Value <= 0) {
                                DischargeRate.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                                DischargeRate.Text = "--";
                            } else if (_Charger.IsDischarging == false) {
                                DischargeRate.Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                                DischargeRate.Text = $"!! ENERGY STALLED !!";
                            } else {
                                DischargeRate.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                                DischargeRate.Text = $"Active: {_Charger.DischargeRate:N2}";
                            }

                            MetalCurrent.Text = $"{_Match.Get().Metal.Current:N0}";
                            MetalStored.Text = $"{_Match.Get().Metal.Max:N0}";
                            MetalIncome.Text = $"+{_Match.Get().Metal.Income:N0}/s";
                            MetalExpense.Text = $"-{_Match.Get().Metal.Expense:N0}/s";

                            EnergyCurrent.Text = $"{_Match.Get().Energy.Current:N0}";
                            EnergyStored.Text = $"{_Match.Get().Energy.Max:N0}";
                            EnergyIncome.Text = $"+{_Match.Get().Energy.Income:N0}/s";
                            EnergyExpense.Text = $"-{_Match.Get().Energy.Expense:N0}/s";
                        });
                    }
                } catch (Exception ex) {
                    _Logger.LogError(ex, "error in refresh thread");
                }
            });
            _RefreshThread.Start();
        }

        public void Dispose() {

        }

        private async void Button_Connect_Click(object sender, RoutedEventArgs e) {

            Button_Connect.IsEnabled = false;
            Button_Connect.Content = "Scanning...";

            try {
                App_Status.Text = $"Status: Connected";

                await _Bt.Scan();

            } catch (Exception ex) {
                _Logger.LogError(ex, "failed to connect");
            } finally {
                Button_Connect.IsEnabled = true;
                Button_Connect.Content = "Connect";
            }
        }

        private async void Button_Disconnect_Click(object sender, RoutedEventArgs e) {
            try {
                App_Status.Text = $"Status: Disconnected";

                await _Bt.StopScan();
                await _Bt.Disconnect();
            } catch (Exception ex) {
                _Logger.LogError(ex, "failed to disconnect");
            }
        }

        private async void Button_Tone_Click(object sender, RoutedEventArgs e) {
            try {
                await _Bt.Tone();
            } catch (Exception ex) {
                _Logger.LogError(ex, $"failed to tone");
            }
        }

        private async void Button_Vibrate_Click(object sender, RoutedEventArgs e) {
            try {
                await _Bt.Vibrate();
            } catch (Exception ex) {
                _Logger.LogError(ex, $"failed to vibrate");
            }
        }

        private async void Button_Shock_Click(object sender, RoutedEventArgs e) {
            try {
                await _Bt.Shock(3);
            } catch (Exception ex) {
                _Logger.LogError(ex, "failed to shock");
            }
        }

        private void Button_SafetyOn_Click(object sender, RoutedEventArgs e) {
            _Bt.EnableSafety();
            Safety_Active.Visibility = Visibility.Collapsed;
            Button_SafetyOn.IsEnabled = false;
            Button_SafetyOff.IsEnabled = true;
        }

        private void Button_SafetyOff_Click(object sender, RoutedEventArgs e) {
            _Bt.DisableSafety();
            Safety_Active.Visibility = Visibility.Visible;
            Button_SafetyOn.IsEnabled = true;
            Button_SafetyOff.IsEnabled = false;
        }
    }
}
