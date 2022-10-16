using MbientLab.MetaWear;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using System.Linq;
using MbientLab.MetaWear.Data;
using MbientLab.MetaWear.Sensor;
using MbientLab.MetaWear.Core;
using MbientLab.MetaWear.Sensor.AccelerometerBmi160;
using MbientLab.MetaWear.Sensor.AccelerometerBosch;
using MbientLab.MetaWear.Core.SensorFusionBosch;

namespace mmrl_2_uwp
{
    /// <summary>
    /// Handles communication between user and MetaWear board.
    /// </summary>
    class MetaWearBoardsManager
    {
        /// <summary>
        /// MAC addresses of the boards to which a connection already exists.
        /// </summary>
        public HashSet<ulong> ConnectedBoardsAddresses { get; private set; }

        public enum AccelerometerSpeed
        {
            normal = 0,
            fast = 1
        }

        // Minimum battery level in percent.
        private const byte BATTERY_LEVEL_MIN = 20;

        public MetaWearBoardsManager()
        {
            ConnectedBoardsAddresses = new HashSet<ulong>();
        }

        /// <summary>
        /// Connects to and initialises a new MetaWear board.
        /// </summary>
        /// <param name="macAddress"></param>
        /// <returns>Returns a reference to a MetaWear board, null otherwise.</returns>
        public async Task<IMetaWearBoard> ConnectToBoard(ulong macAddress)
        {
            if (ConnectedBoardsAddresses.Contains(macAddress))
            {
                Console.WriteLine($"INFO: Already connected to a board with MAC address {MetaWearScanner.MacUlongToString(macAddress)}.");
            }
            else
            {
                var bleDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(macAddress);
                var metaWearBoard = MbientLab.MetaWear.Win10.Application.GetMetaWearBoard(bleDevice);
                try
                {
                    await metaWearBoard.InitializeAsync();

                    // TODO: Assign method which tries to re-connect to the board x-times and only then aborts the process with an error message.
                    metaWearBoard.OnUnexpectedDisconnect = () => Console.WriteLine($"Unexpectedly lost connection to board with MAC address {MetaWearScanner.MacUlongToString(macAddress)}!");

                    var batteryLevel = await metaWearBoard.ReadBatteryLevelAsync();
                    if (batteryLevel < BATTERY_LEVEL_MIN)
                    {
                        Console.WriteLine($"INFO: Battery level low! (MAC={MetaWearScanner.MacUlongToString(macAddress)}, Charge={batteryLevel}%)");
                    }

                    ConnectedBoardsAddresses.Add(macAddress);
                    return metaWearBoard;
                }
                catch (Exception e)
                {
                    metaWearBoard.TearDown();
                    Console.WriteLine($"ERROR: Could not connect to or initialise MetaWear board with MAC address {MetaWearScanner.MacUlongToString(macAddress)}!");
                    Console.WriteLine($"       Reason: {e}");
                }
            }
            return null;
        }

        /// <summary>
        /// Disconnects a MetaWear board.
        /// </summary>
        /// <param name="macAddress"></param>
        /// <returns>0 if the board could successfully be disconnected, -1 otherwise.</returns>
        public int DisconnectBoard(IMetaWearBoard board)
        {
            ulong macAddress = MetaWearScanner.MacUlongFromString(board.MacAddress);
            if (ConnectedBoardsAddresses.Contains(macAddress) || board.IsConnected)
            {
                board.TearDown();
                ConnectedBoardsAddresses.Remove(macAddress);
                return 0;
            }
            else
            {
                Console.WriteLine($"ERROR: Could not disconnect MetaWear board with MAC address {board.MacAddress}!");
                return -1;
            }
        }

        public async Task StartAccelerometerStream(IMetaWearBoard board, Acceleration accData, AccelerometerSpeed speed = AccelerometerSpeed.normal)
        {
            if (ConnectedBoardsAddresses.Contains(MetaWearScanner.MacUlongFromString(board.MacAddress)))
            {
                // Reduce the max BLE connection interval to 7.5ms so the BLE connection can handle 100 Hz sampling frequency.
                board.GetModule<ISettings>()?.EditBleConnParams(maxConnInterval: 7.5f);
                await Task.Delay(1500);

                IAccelerometerBmi160 accelerometer = board.GetModule<IAccelerometerBmi160>();

                // Set output data rate to 25Hz, set range to +/-4g.
                accelerometer.Configure(odr: OutputDataRate._25Hz, range: DataRange._4g);

                // Accelerometer has a fast mode that combines 3 data samples into 1 BLE package increasing the data throughput by 3x.
                if (speed == AccelerometerSpeed.fast)
                {
                    await accelerometer.PackedAcceleration.AddRouteAsync(source => source.Stream(data => {
                        accData = data.Value<Acceleration>();
                        Console.WriteLine("Acceleration = " + accData);
                    }
                    ));

                    // Start the acceleration data.
                    accelerometer.PackedAcceleration.Start();
                }
                else
                {
                    await accelerometer.Acceleration.AddRouteAsync(source => source.Stream(data => {
                        accData = data.Value<Acceleration>();
                        Console.WriteLine("Acceleration = " + accData);
                    }
                    ));

                    // Start the acceleration data.
                    accelerometer.Acceleration.Start();
                }

                // Put accelerometer in active mode.
                accelerometer.Start();
            }
            else
            {
                Console.WriteLine($"ERROR: Could not stream acceleration data from {board.MacAddress}!");
            }
        }

        public void StopAccelerometerStream(IMetaWearBoard board)
        {
            if (ConnectedBoardsAddresses.Contains(MetaWearScanner.MacUlongFromString(board.MacAddress)))
            {
                IAccelerometerBmi160 accelerometer = board.GetModule<IAccelerometerBmi160>();

                // Put accelerometer back into standby mode.
                accelerometer.Stop();

                // Stop accelerometer data collection.
                accelerometer.Acceleration.Stop();
            }
            else
            {
                Console.WriteLine($"ERROR: StopAccelerometerStream() could not find {board.MacAddress}!");
            }
        }

        /// <summary>
        /// Test if sensor fusion and acceleration data can be streamed simultaneously.
        /// </summary>
        /// <param name="board"></param>
        public async Task StartSensorFusionAndAccelerometerStream(IMetaWearBoard board)
        {
            board.GetModule<ISettings>()?.EditBleConnParams(maxConnInterval: 7.5f);
            await Task.Delay(1500);

            ISensorFusionBosch fusionModule = board.GetModule<ISensorFusionBosch>();
            if (fusionModule == null)
            {
                Console.WriteLine($"ERROR: Cannot connect to the sensor fusion module of board {board.MacAddress}!");
                return;
            }

            // "Compass" runs the accelerometer only on 25 Hz which saves bandwitdh + trying to manually reduce it even further.
            fusionModule.Configure(Mode.Compass, AccRange._2g, GyroRange._250dps, new object[] { OutputDataRate._12_5Hz, FilterMode.Osr2 });

            await fusionModule.Quaternion.AddRouteAsync(source => source.Stream(data =>
            {
                Console.WriteLine($"Quaternion = {data.Value<Quaternion>()}");
            }));
            await Task.Delay(1500);

            await fusionModule.LinearAcceleration.AddRouteAsync(source => source.Stream(data =>
            {
                Console.WriteLine($"Acceleration = {data.Value<Acceleration>()}");
            }));
            // Board needs some time for BLE communication.
            await Task.Delay(1500);

            fusionModule.Quaternion.Start();
            await Task.Delay(500);
            fusionModule.LinearAcceleration.Start();
            await Task.Delay(500);
            fusionModule.Start();
        }

        /// <summary>
        /// Test if sensor fusion and acceleration data can be streamed simultaneously.
        /// </summary>
        /// <param name="board"></param>
        public async Task StopSensorFusionAndAccelerometerStream(IMetaWearBoard board)
        {
            ISensorFusionBosch fusionModule = board.GetModule<ISensorFusionBosch>();
            fusionModule.Stop();
            await Task.Delay(500);
            fusionModule.Quaternion.Stop();
            fusionModule.LinearAcceleration.Stop();
        }

        public async Task StartSensorFusionStream(IMetaWearBoard board, FusionData fusionData)
        {
            // Reduce the max BLE connection interval to 7.5ms so the BLE connection can handle 100 Hz sampling frequency.
            board.GetModule<ISettings>()?.EditBleConnParams(maxConnInterval: 7.5f);
            await Task.Delay(1500);

            ISensorFusionBosch fusionModule = board.GetModule<ISensorFusionBosch>();
            if (fusionModule == null || !ConnectedBoardsAddresses.Contains(MetaWearScanner.MacUlongFromString(board.MacAddress)))
            {
                Console.WriteLine($"ERROR: Cannot connect to the sensor fusion module of board {board.MacAddress}!");
                return;
            }

            // Reason for IMUPlus mode: https://mbientlab.com/tutorials/SensorFusion.html#magnets-and-magnetometers
            fusionModule.Configure(Mode.ImuPlus);

            await fusionModule.Quaternion.AddRouteAsync(source => source.Stream(data =>
            {
                Console.WriteLine($"Quaternion = {data.Value<Quaternion>()}");
                fusionData.Quaternion = data.Value<Quaternion>();
            }));
            // Board needs some time for BLE communication.
            await Task.Delay(500);

            var calibrationStateTask = fusionModule.ReadCalibrationStateAsync();
            await calibrationStateTask;
            // Magnetometer is NOT checked as we use IMUPlus mode which does not use the magnetometer.
            if (calibrationStateTask.Result.accelerometer < CalibrationAccuracy.MediumAccuracy ||
                calibrationStateTask.Result.gyroscope < CalibrationAccuracy.MediumAccuracy)
            {
                Console.WriteLine($"SensorFusion calibration necessary! Calibration states:");
                Console.WriteLine($"Accelerometer: {CalibrationAccuracyToString(calibrationStateTask.Result.accelerometer)}");
                Console.WriteLine($"Accelerometer: {CalibrationAccuracyToString(calibrationStateTask.Result.gyroscope)}");
            }
            fusionData.ImuCalibrationState = calibrationStateTask.Result;
            // Board needs some time for BLE communication.
            await Task.Delay(1500);

            // Start data collection.
            fusionModule.Quaternion.Start();

            // Put sensor fusion module in active mode.
            fusionModule.Start();
        }

        public void StopSensorFusionStream(IMetaWearBoard board)
        {
            if (ConnectedBoardsAddresses.Contains(MetaWearScanner.MacUlongFromString(board.MacAddress)))
            {
                ISensorFusionBosch fusionModule = board.GetModule<ISensorFusionBosch>();

                // Put module back into standby mode.
                fusionModule.Stop();

                // Stop data collection.
                fusionModule.Quaternion.Stop();
            }
            else
            {
                Console.WriteLine($"ERROR: StopSensorFusionStream() could not find {board.MacAddress}!");
            }
        }

        private string CalibrationAccuracyToString(CalibrationAccuracy calibrationAccuracy)
        {
            string accuracy = "undefined";
            switch (calibrationAccuracy)
            {
                case CalibrationAccuracy.HighAccuracy:
                    accuracy = "high";
                    break;
                case CalibrationAccuracy.MediumAccuracy:
                    accuracy = "medium";
                    break;
                case CalibrationAccuracy.LowAccuracy:
                    accuracy = "low";
                    break;
                case CalibrationAccuracy.Unreliable:
                    accuracy = "unreliable";
                    break;
            }
            return accuracy;
        }
    }
}
