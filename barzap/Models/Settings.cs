using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using barzap.Panels;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace barzap.Models {

    public class Settings {

        private const string SETTINGS_PATH = "./settings.json";

        static Settings() {
            //Instance = LoadSettings();
        }

        public static Settings Instance { get; } = new();

        /// <summary>
        ///     accent color for the theme
        /// </summary>
        public ThemeName CurrentTheme { get; set; } = ThemeName.Cyan;

        /// <summary>
        ///     will the theme be dark mode or not
        /// </summary>
        public bool DarkMode { get; set; } = true;

        /// <summary>
        ///     maximum value to get shocked at. any shock above this value will get clamped
        /// </summary>
        public int MaxShock { get; set; } = 1;

        /// <summary>
        ///     will the shock charge slowly decrease
        /// </summary>
        public bool DoDischarge { get; set; } = true;

        /// <summary>
        ///     will having low power (below <see cref="LowPowerStopDischargeValue"/>),
        ///     prevent discharge from occuring
        /// </summary>
        public bool DoesLowPowerStopDischarge { get; set; } = true;

        /// <summary>
        ///     if <see cref="DoesLowPowerStopDischarge"/> is true, what percent
        ///     of power will prevent discharge from occuring?
        /// </summary>
        public decimal LowPowerStopDischargeValue { get; set; } = 0.05m;

        /// <summary>
        ///     will the discharge per second scale based on how full energy is?
        ///     if false, <see cref="MinimumDischargeRate"/> is always used
        /// </summary>
        public bool ScaleDischarge { get; set; } = true;

        /// <summary>
        ///     minimum discharge rate
        /// </summary>
        public decimal MinimumDischargeRate { get; set; } = 0.01m;

        /// <summary>
        ///     when <see cref="ScaleDischarge"/> is true, the maximum rate
        ///     that charge will disperse
        /// </summary>
        public decimal MaximumDischargeRate { get; set; } = 0.05m;

        /// <summary>
        ///     passive charge rate
        /// </summary>
        public decimal PassiveChargeRate { get; set; } = 0.00m;

        /// <summary>
        ///     will killing enemies discharge the shock charger
        /// </summary>
        public bool DoEnemyKillsDischarge { get; set; } = true;

        /// <summary>
        ///     if <see cref="DoEnemyKillsDischarge"/> is true, any charge change
        ///     is multiplied by this value first
        /// </summary>
        public decimal EnemyKillDischargeScale { get; set; } = 1.0m;

        private static Settings LoadSettings() {
            try {
                string contents = File.ReadAllText(SETTINGS_PATH);
                Settings trySettings = JsonConvert.DeserializeObject<Settings>(contents) ?? throw new Exception();

                return trySettings;
            } catch (Exception ex) {
                return new Settings();
            }
        }

        public void SaveSettings() {
            string settingsText = JsonConvert.SerializeObject(this);
            File.WriteAllText(SETTINGS_PATH, settingsText);
        }

    }
}