using barzap.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace barzap.Services {

    // much of this code was derived from 41666's existing code:
    // https://git.sapphic.engineer/noe/noe.sh/src/branch/main/if-it-dies-in-game-it-dies-in-real-life/index.html
    public class Bt {

        private readonly ILogger<Bt> _Logger;

        // service that contains the 2 characters we want to use, control and auth
        private const string COLLAR_SERVICE = "0bd51666-e7cb-469b-8e4d-2742f1ba77cc";

        private const string COLLAR_CHAR_AUTH = "0e7ad781-b043-4877-aae2-112855353cc2";
        private const string COLLAR_CHAR_CONTROL = "e7add780-b042-4876-aae1-112855353cc1";

        private const string COLLAR_SERVICE_BATTERY = "52534300-6822-5570-6886-123456789000";
        private const string COLLAR_CHAR_BATTERY = "52534300-6822-5570-6886-123456789abc";

        private const int MAX_SHOCK = 1;

        private BluetoothLEAdvertisementWatcher _AdvertismentWatcher;

        private bool _Connected = false;
        private BluetoothLEDevice? _Device;
        private GattCharacteristic? _AuthCharacteristic;
        private GattCharacteristic? _ControlCharacteristic;
        private GattCharacteristic? _BatteryCharacteristic;

        private HashSet<ulong> _FoundBluetoothDevices = new();
        private bool _Safety = true;

        public Bt(ILogger<Bt> logger) {
            _Logger = logger;

            _AdvertismentWatcher = new();
            _AdvertismentWatcher.ScanningMode = BluetoothLEScanningMode.Active;
            _AdvertismentWatcher.SignalStrengthFilter.InRangeThresholdInDBm = -80;
            _AdvertismentWatcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -90;
            _AdvertismentWatcher.AdvertisementFilter.Advertisement.LocalName = "PetSafe Smart Dog Trainer";

            _AdvertismentWatcher.Received += OnAdvertisementReceived;
        }

        public async Task Scan() {
            _Logger.LogDebug($"starting BT scan");
            _FoundBluetoothDevices.Clear();
            _AdvertismentWatcher.Start();

            while (_Connected == false) {
                await Task.Delay(100);
            }
        }

        public Task StopScan() {
            _Logger.LogDebug($"stopping BT scan");
            _AdvertismentWatcher.Stop();
            return Task.CompletedTask;
        }

        public void DisableSafety() {
            _Safety = false;
        }

        public void EnableSafety() {
            _Safety = true;
        }

        public Task Disconnect() {
            _Logger.LogDebug($"disconnecting");

            _AuthCharacteristic = null;
            _BatteryCharacteristic = null;
            _ControlCharacteristic = null;

            _Device?.Dispose();
            _Device = null;
            return Task.CompletedTask;
        }

        private void OnConnectionStatusChanged(BluetoothLEDevice device, object _) {
            _Logger.LogInformation($"connection staus change [name={device.Name}] [status={device.ConnectionStatus}]");
            if (device.ConnectionStatus != BluetoothConnectionStatus.Connected) {
                _Connected = false;
            }
        }
        
        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs args) {

            if (_FoundBluetoothDevices.Contains(args.BluetoothAddress)) {
                _Logger.LogTrace($"skipping advertisement, already seen [address={args.BluetoothAddress}] [name={args.Advertisement.LocalName}]");
                return;
            }

            _FoundBluetoothDevices.Add(args.BluetoothAddress);

            _Logger.LogDebug($"advertisment found [address={args.BluetoothAddress}] [name={args.Advertisement.LocalName}] [str={args.RawSignalStrengthInDBm}]");

            if (args.Advertisement.LocalName != "PetSafe Smart Dog Trainer") {
                return;
            }

            _AdvertismentWatcher.Stop();

            _AuthCharacteristic = null;
            _BatteryCharacteristic = null;
            _ControlCharacteristic = null;

            _Logger.LogInformation($"connecting to collar [address={args.BluetoothAddress}] [type={args.BluetoothAddressType}]");
            BluetoothLEDevice dev = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
            _Device = dev;

            _Device.ConnectionStatusChanged += OnConnectionStatusChanged;

            GattDeviceServicesResult gatt = await _Device.GetGattServicesAsync();

            _Logger.LogInformation($"connection to collar [status={gatt.Status}]");

            if (gatt.Status != GattCommunicationStatus.Success) {
                _Logger.LogError($"failed to connect to collar [status={gatt.Status}]");
                return;
            }

            foreach (GattDeviceService service in gatt.Services) {
                _Logger.LogDebug($"found collar service [uuid={service.Uuid}]");

                GattCharacteristicsResult chars = await service.GetCharacteristicsAsync();
                foreach (GattCharacteristic c in chars.Characteristics) {
                    _Logger.LogTrace($"service characteristic [uuid={c.Uuid}] [service={service.Uuid}] [description={c.UserDescription}]"
                        + $" [properties={c.CharacteristicProperties}]");
                }

                string serviceUuid = service.Uuid.ToString().ToLower();

                if (serviceUuid == COLLAR_SERVICE) {
                    GattCharacteristicsResult authChar = await service.GetCharacteristicsForUuidAsync(new Guid(COLLAR_CHAR_AUTH));
                    if (authChar.Status != GattCommunicationStatus.Success) {
                        _Logger.LogError($"failed to get auth characteristic [status={authChar.Status}]");
                    } else {
                        _AuthCharacteristic = authChar.Characteristics[0];
                        _Logger.LogDebug($"found auth characteristic [uuid={_AuthCharacteristic.Uuid}]");
                    }

                    GattCharacteristicsResult controlChar = await service.GetCharacteristicsForUuidAsync(new Guid(COLLAR_CHAR_CONTROL));
                    if (controlChar.Status != GattCommunicationStatus.Success) {
                        _Logger.LogError($"failed to get control characteristic [status={controlChar.Status}]");
                    } else {
                        _ControlCharacteristic = controlChar.Characteristics[0];
                        _Logger.LogDebug($"found control characteristic [uuid={_ControlCharacteristic.Uuid}]");
                    }
                } else if (serviceUuid == COLLAR_SERVICE_BATTERY) {

                    GattCharacteristicsResult batteryChar = await service.GetCharacteristicsForUuidAsync(new Guid(COLLAR_CHAR_BATTERY));
                    if (batteryChar.Status != GattCommunicationStatus.Success) {
                        _Logger.LogError($"failed to get battery characteristic [status={batteryChar.Status}]");
                    } else {
                        _BatteryCharacteristic = batteryChar.Characteristics[0];
                        _Logger.LogDebug($"found battery characteristic [uuid={_BatteryCharacteristic.Uuid}]");

                        _BatteryCharacteristic.ValueChanged += BatteryLevelUpdate;
                    }

                }
            }

            if (_AuthCharacteristic == null) {
                _Logger.LogWarning($"cannot auth, characteristic is null");
                return;
            }

            // PIN is hard coded to 0000
            DataWriter dw = new();
            dw.WriteBytes([0x55, 0x37, 0x37, 0x30, 0x30, 0x30, 0x30]);

            _Logger.LogDebug($"writing auth pin");
            GattCommunicationStatus res = await _AuthCharacteristic.WriteValueAsync(dw.DetachBuffer());
            _Logger.LogDebug($"auth write done [status={res}]");

            _Connected = true;
        }

        private void BatteryLevelUpdate(GattCharacteristic sender, GattValueChangedEventArgs args) {
            DataReader dr = DataReader.FromBuffer(args.CharacteristicValue);

            byte[] bytes = new byte[1024];
            dr.ReadBytes(bytes);

            _Logger.LogInformation($"battery [args{string.Join(" ", bytes)}");
        }

        public async Task Shock(int str) {
            if (_Safety == true) {
                _Logger.LogDebug($"not shocking, safety is on");
                return;
            }

            if (_ControlCharacteristic == null) {
                _Logger.LogDebug($"not sending shock, control char is null");
                return;
            }

            // cap to max allowed value
            str = Math.Min(Settings.Instance.MaxShock, str);

            if (str < 0 || str > 15) {
                throw new Exception($"shock strength is between 0 and 15");
            }

            DataWriter dw = new();
            dw.WriteBytes([0x55, 0x36, 0x31, 0x32, 0x33, (byte)(0x30 + str)]);
            _Logger.LogDebug($"writing shock [str={str}]");
            GattCommunicationStatus res = await _ControlCharacteristic.WriteValueAsync(dw.DetachBuffer());
            _Logger.LogDebug($"shock write done [status={res}]");
        }

        public async Task Tone() {
            if (_ControlCharacteristic == null) {
                _Logger.LogDebug($"not sending tone, control char is null");
                return;
            }

            _Logger.LogDebug($"writing tone command");
            DataWriter dw = new DataWriter();
            dw.WriteBytes([0x55, 0x36, 0x31, 0x31, 0x31, 0x30]);
            GattCommunicationStatus res = await _ControlCharacteristic.WriteValueAsync(dw.DetachBuffer());
            _Logger.LogDebug($"tone write done [status={res}]");
        }

        public async Task Vibrate() {
            if (_ControlCharacteristic == null) {
                _Logger.LogDebug($"not vibrating, control char is null");
                return;
            }

            _Logger.LogDebug($"writing tone command");
            DataWriter dw = new DataWriter();
            dw.WriteBytes([0x55, 0x36, 0x31, 0x33, 0x33, 0x30]);
            GattCommunicationStatus res = await _ControlCharacteristic.WriteValueAsync(dw.DetachBuffer());
            _Logger.LogDebug($"tone write done [status={res}]");
        }


    }
}
