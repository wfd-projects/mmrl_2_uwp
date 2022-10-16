using MbientLab.MetaWear;
using System;
using System.Collections.Generic;
using Windows.Devices.Bluetooth.Advertisement;
using System.Linq;
using System.Globalization;

namespace mmrl_2_uwp
{
    class MetaWearScanner
    {
        /// <summary>
        /// MAC addresses of the MetaWear boards. Each board is listed only once.
        /// </summary>
        public HashSet<ulong> ScanResults { get; private set; }
        private BluetoothLEAdvertisementWatcher _watcher;

        public MetaWearScanner()
        {
            ScanResults = new HashSet<ulong>();

            BluetoothLEManufacturerData manufacturerFilter = new BluetoothLEManufacturerData();

            _watcher = new BluetoothLEAdvertisementWatcher();
            _watcher.Received += OnAdvertisementReceived;
            // Actively scanning consumes more power but makes sure we receive scan response advertisements as well.
            _watcher.ScanningMode = BluetoothLEScanningMode.Active;
        }

        public void StartScanning()
        {
            Console.WriteLine("Scanning for MetaWear boards...");
            _watcher.Start();
        }

        public void StopScanning()
        {
            Console.WriteLine("Stopped scanning.");
            _watcher.Stop();
        }

        /// <summary>
        /// Converts a MAC address from ulong into a more readable colon-separated string.
        /// Example: (ulong)253022581560120 becomes (string)E6:1F:69:18:13:38
        /// Code taken from: https://stackoverflow.com/questions/50519301/how-to-convert-a-mac-address-from-string-to-unsigned-int
        /// </summary>
        /// <param name="macAddress"></param>
        /// <returns></returns>
        public static string MacUlongToString(ulong macAddress)
        {
            return string.Join(":", BitConverter.GetBytes(macAddress).Reverse().Select(b => b.ToString("X2"))).Substring(6);
        }

        /// <summary>
		/// Convert a MAC address from its string representation to ulong.
        /// Example: From (string)E6:1F:69:18:13:38 to (ulong)253022581560120
		/// </summary>
        /// <returns>MAC address in ulong type.</returns>
		public static ulong MacUlongFromString(string macAddress)
        {
            return ulong.Parse(macAddress.Replace(":", ""), NumberStyles.HexNumber);
        }

        private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            // Remember new BLE device only when it is a MetaWear board.
            if (!ScanResults.Contains(eventArgs.BluetoothAddress) && eventArgs.Advertisement.ServiceUuids.Contains(Constants.METAWEAR_GATT_SERVICE))
            {
                ScanResults.Add(eventArgs.BluetoothAddress);
                Console.WriteLine($"Found new device with MAC address {MacUlongToString(eventArgs.BluetoothAddress)}.");
            }
        }
    }
}
