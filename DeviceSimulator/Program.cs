using System;
using System.Threading;
using System.Device.Location;
using System.Threading.Tasks;

namespace DeviceSimulator
{
    public class Program
    {
        private static AzureIoTHub _iotClient;
        // The coordinate watcher.
        public static void Main(string[] args)
        {
            _iotClient = new AzureIoTHub();
            SimulateDeviceToSendD2CAndReceiveD2C();
        }
        private static void SimulateDeviceToSendD2CAndReceiveD2C()
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            // Create the watcher.
        
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                tokenSource.Cancel();
                Console.WriteLine("Exiting ...");
            };
            Console.WriteLine("Press CTRL+C to exit");
      
            Task.WaitAll(
                _iotClient.SendDeviceToCloudMessageAsync(tokenSource.Token),
                _iotClient.ReceiveMessagesFromDeviceAsync(tokenSource.Token)
            );
            Console.ReadLine();
        }
    }

   
}
