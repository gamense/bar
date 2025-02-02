using System;
using System.Windows;
using System.Windows.Controls;
using ControlzEx.Theming;
using barzap.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MahApps.Metro.Controls;

namespace barzap.Panels;

public partial class SettingsPanel {

    private readonly ILogger<SettingsPanel> _Logger;
    private Settings _CurrentSettings;

    public SettingsPanel() {
        _Logger = App.Services.GetRequiredService<ILogger<SettingsPanel>>();

        _CurrentSettings = Settings.Instance;
        InitializeComponent();
        ApplyTheme();
    }

    private void ApplyTheme() {
        string? sThemeName = Enum.GetName(_CurrentSettings.CurrentTheme);

        string sDarkMode = _CurrentSettings.DarkMode ? "Dark" : "Light";

        string sThemeId = sDarkMode + "." + sThemeName;

        _Logger.LogDebug($"Applying theme {sThemeId}");

        string? themeName = Enum.GetName(_CurrentSettings.CurrentTheme);
        if (ComboBoxThemeColorSelection.Text != themeName) {
            ComboBoxThemeColorSelection.Text = themeName;
        }

        if (RadioButtonSettingsDarkMode.IsChecked != _CurrentSettings.DarkMode) {
            RadioButtonSettingsDarkMode.IsChecked = _CurrentSettings.DarkMode;
        }

        if (RadioButtonSettingsLightMode.IsChecked == _CurrentSettings.DarkMode) {
            RadioButtonSettingsLightMode.IsChecked = !_CurrentSettings.DarkMode;
        }

        ThemeManager.Current.ChangeTheme((App)Application.Current, sThemeId);

        Settings_PassiveDischarge.IsOn = _CurrentSettings.DoDischarge;
        Settings_PassiveDischargeEnergyScaling.IsOn = _CurrentSettings.ScaleDischarge;
        Settings_PassiveDischargeMinimum.Value = (double)_CurrentSettings.MinimumDischargeRate;
        Settings_PassiveDischargeMaximum.Value = (double)_CurrentSettings.MaximumDischargeRate;
        Settings_EnemyKillsDischarge.IsOn = _CurrentSettings.DoEnemyKillsDischarge;
        Settings_EnemyKillsDischargeMultiplier.Value = (double)_CurrentSettings.EnemyKillDischargeScale;
        Settings_MaxShock.Value = _CurrentSettings.MaxShock;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) {
        Settings.Instance.SaveSettings();
        _CurrentSettings = Settings.Instance;
    }

    private bool _ToggleSwitch(object sender) {
        ToggleSwitch? ts = sender as ToggleSwitch;
        if (ts == null) {
            return false;
        }

        return ts.IsOn;
    }

    private void Theme_Selection_Changed_ComboBox(object sender, SelectionChangedEventArgs e) {
        object? theme = e.AddedItems[0];
        if (theme != null) {
            _CurrentSettings.CurrentTheme = (ThemeName)theme;

            ApplyTheme();
        }
    }

    private void Dark_Mode_RadioButton(object sender, RoutedEventArgs e) {
        _CurrentSettings.DarkMode = true;
        ApplyTheme();
    }

    private void Light_Mode_RadioButton(object sender, RoutedEventArgs e) {
        _CurrentSettings.DarkMode = false;
        ApplyTheme();
    }

    private void Settings_PassiveDischarge_Toggled(object sender, RoutedEventArgs e) {
        _CurrentSettings.DoDischarge = _ToggleSwitch(sender);
    }

    private void Settings_PassiveDischargeEnergyScaling_Toggled(object sender, RoutedEventArgs e) {
        _CurrentSettings.ScaleDischarge = _ToggleSwitch(sender);
    }

    private void Settings_PassiveDischargeMaximum_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e) {
        _CurrentSettings.MaximumDischargeRate = (decimal)(e.NewValue ?? 0.05d);
    }

    private void Settings_PassiveDischargeMinimum_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e) {
        _CurrentSettings.MinimumDischargeRate = (decimal)(e.NewValue ?? 0.05d);
    }

    private void Settings_EnemyKillsDischarge_Toggled(object sender, RoutedEventArgs e) {
        _CurrentSettings.DoEnemyKillsDischarge = _ToggleSwitch(sender);
    }

    private void Settings_EnemyKillsDischargeMultiplier_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e) {
        _CurrentSettings.EnemyKillDischargeScale = (decimal)(e.NewValue ?? 1.00d);
    }

    private void Settings_MaxShock_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e) {
        _CurrentSettings.MaxShock = (int)(e.NewValue ?? 1);
    }

}