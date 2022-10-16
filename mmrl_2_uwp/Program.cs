using System;
using System.Threading.Tasks;
using System.Linq;
using MbientLab.MetaWear.Data;
using MbientLab.MetaWear;

// This example code shows how you could implement the required main function for a 
// Console UWP Application. You can replace all the code inside Main with your own custom code.

// You should also change the Alias value in the AppExecutionAlias Extension in the 
// Package.appxmanifest to a value that you define. To edit this file manually, right-click
// it in Solution Explorer and select View Code, or open it with the XML Editor.

namespace mmrl_2_uwp
{
    class Program
    {
        public static async Task ScenarioStreamAcceleration(MetaWearBoardsManager boardsManager, IMetaWearBoard board)
        {
            Acceleration accData = new Acceleration(0, 0, 0);
            Console.WriteLine("Starting accelerometer data stream... Press x to stop!");
            await boardsManager.StartAccelerometerStream(board, accData);
            Console.WriteLine("Press any key to stop the accelerometer.");
            Console.ReadKey();
            boardsManager.StopAccelerometerStream(board);
            Console.WriteLine("Stopped accelerometer.\n");
        }

        public static async Task ScenarioStreamFusion(MetaWearBoardsManager boardsManager, IMetaWearBoard board)
        {
            FusionData fusionData = new FusionData();
            Console.WriteLine("\nStarting sensor fusion data stream... Press any key to stop!");
            await boardsManager.StartSensorFusionStream(board, fusionData);
            Console.ReadKey();
            boardsManager.StopSensorFusionStream(board);
            Console.WriteLine("Stopped sensor fusion.\n");
        }

        /// <summary>
        /// IMPORTANT: Sensor fusion and accelerometer modules cannot be used simultaneously!
        /// https://mbientlab.com/csdocs/1/sensor_fusion.html
        /// Problem: Tapping on sensor to start calibration of arm posture is not possible.
        /// Ideas how to deal with this in the HoloLens:
        ///  * Speech-command to trigger arm calibration.
        ///  * Button that can be pressed an after a countdown of, e.g., 10 seconds during which there is enough time to get the arm into posture, calibration is started.
        /// 
        /// The .NET app can avoid this problem by waiting after calling AddRouteAsync(). However, this does not work reliably with UWP apps anymore.
        /// I could get both async streams to work while debuggung but not reliably implement it :(
        /// </summary>
        public static async Task ScenarioStreamFusion_double(MetaWearBoardsManager boardsManager, IMetaWearBoard board)
        {
            Console.WriteLine("\nStarting sensor fusion AND acceleration data stream... Press any key to stop!");
            await boardsManager.StartSensorFusionAndAccelerometerStream(board);
            Console.ReadKey();
            await boardsManager.StopSensorFusionAndAccelerometerStream(board);
            Console.WriteLine("Stopped sensor fusion.\n");
        }

        static async Task Main(string[] args)
        {
            /***** Find all MetaWear boards. *****/
            var mwScanner = new MetaWearScanner();
            mwScanner.StartScanning();
            Console.WriteLine("Press key x to stop scanning. Current scan results:");

            char key;
            do
            {
                key = Console.ReadKey().KeyChar;
            } while (key != 'x');

            Console.WriteLine("");
            mwScanner.StopScanning();

            Console.WriteLine("Results:");
            foreach (var address in mwScanner.ScanResults)
            {
                Console.WriteLine(MetaWearScanner.MacUlongToString(address));
            }

            if (mwScanner.ScanResults.Count > 0)
            {
                /***** Just connect to the first board found. *****/
                Console.WriteLine("Trying to connect to a board...");
                var firstBoardMac = mwScanner.ScanResults.First<ulong>();

                MetaWearBoardsManager boardsManager = new MetaWearBoardsManager();
                var connectingTask = boardsManager.ConnectToBoard(firstBoardMac);
                await connectingTask;
                var board = connectingTask.Result;
                if (board != null)
                {
                    Console.WriteLine("Successfully conected.");
                }
                else
                {
                    Console.WriteLine($"Could not connect to {board.MacAddress}.");
                    return;
                }

                /***** Calibrate sensor fusion *****/
                // TODO

                /***** Stream sensor fusion data *****/
                await ScenarioStreamFusion(boardsManager, board);
                //await ScenarioStreamFusion_double(boardsManager, board);

                /***** Stream accelerometer *****/
                //await ScenarioStreamAcceleration(boardsManager, board);

                /***** Disconnect board *****/
                Console.WriteLine("\nPress any key to disconnect.");
                Console.ReadKey();
                if (boardsManager.DisconnectBoard(board) == 0)
                {
                    Console.WriteLine("Successfully disconnected.");
                }
                else
                {
                    Console.WriteLine("Error: Could not disconnect.");
                }
            }
        }
    }
}
