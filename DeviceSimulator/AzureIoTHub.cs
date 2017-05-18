using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Common.Exceptions;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Device.Location;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Devices = Microsoft.Azure.Devices;

namespace DeviceSimulator
{
    public class AzureIoTHub
    {
        /// <summary>
        /// Please replace with correct connection string value
        /// The connection string could be got from Azure IoT Hub -> Shared access policies -> iothubowner -> Connection String:
        /// </summary>
        private const string connectionString = "";

        /// <summary>
        /// Please replace with correct device connection string
        /// The device connect string could be got from Azure IoT Hub -> Devices -> {your device name } -> Connection string
        /// </summary>
        private const string deviceConnectionString = "";

        private const string iotHubD2cEndpoint = "messages/events";

        private GeoCoordinateWatcher _watcher = null;
        private CurrentLocation _location = null;

        public AzureIoTHub()
        {
            SetUpGeoLocation();
        }

        private void SetUpGeoLocation()
        {
            _watcher = new GeoCoordinateWatcher();

            // Catch the StatusChanged event.
            _watcher.StatusChanged += Watcher_StatusChanged;
            _watcher.PositionChanged += Watcher_PostionChanged;
            // Start the watcher.
            _watcher.Start();
        }


        public async Task<string> CreateDeviceIdentityAsync(string deviceName)
          {
              var registryManager = Devices.RegistryManager.CreateFromConnectionString(connectionString);
              Devices.Device device;
              try
              {
                  device = await registryManager.AddDeviceAsync(new Devices.Device(deviceName));
              }
              catch (DeviceAlreadyExistsException)
              {
                  device = await registryManager.GetDeviceAsync(deviceName);
              }
              return device.Authentication.SymmetricKey.PrimaryKey;
          }
            
        private void Watcher_PostionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            Console.WriteLine("Position has changed");

            GeoCoordinate location =
                _watcher.Position.Location;
            _location = new CurrentLocation()
            {
                Latitude = location.Latitude.ToString(),
                Longitude = location.Longitude.ToString()
            };

            Console.WriteLine($"New Position Is : Latitude : {_location.Latitude}: Longitude: {_location.Longitude}");
        }

        private void Watcher_StatusChanged(object sender, GeoPositionStatusChangedEventArgs e)
        {
            if (e.Status == GeoPositionStatus.Ready)
            {
                // Display the latitude and longitude.
                if (_watcher.Position.Location.IsUnknown)
                {
                    Console.WriteLine("Cannot find location data");
                }
                else
                {
                    GeoCoordinate location =
                        _watcher.Position.Location;
                    _location = new CurrentLocation()
                    {
                        Latitude = location.Latitude.ToString(),
                        Longitude = location.Longitude.ToString()
                    };

                    Console.WriteLine($"Initial Position Is : Latitude : {_location.Latitude}: Longitude: {_location.Longitude}");

                }
            }
        }

        public async Task SendDeviceToCloudMessageAsync(CancellationToken cancelToken)
        {
            var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);

            while (true)
            {
                if (cancelToken.IsCancellationRequested)
                    break;
               
                var messageString = JsonConvert.SerializeObject(_location);
                var message = new Message(Encoding.ASCII.GetBytes(messageString));
                await deviceClient.SendEventAsync(message);
                Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);
                await Task.Delay(1000);
            }
        }

        public async Task<string> ReceiveCloudToDeviceMessageAsync()
        {
            var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);

            while (true)
            {
                var receivedMessage = await deviceClient.ReceiveAsync();

                if (receivedMessage != null)
                {
                    var messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                    await deviceClient.CompleteAsync(receivedMessage);
                    return messageData;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        public async Task ReceiveMessagesFromDeviceAsync(CancellationToken cancelToken)
        {
            EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(connectionString, iotHubD2cEndpoint);
            var d2cPartitions = eventHubClient.GetRuntimeInformation().PartitionIds;

            await Task.WhenAll(d2cPartitions.Select(partition => ReceiveMessagesFromDeviceAsync(eventHubClient, partition, cancelToken)));
        }

        private static async Task ReceiveMessagesFromDeviceAsync(EventHubClient eventHubClient, string partition, CancellationToken ct)
        {
            var eventHubReceiver = eventHubClient.GetDefaultConsumerGroup().CreateReceiver(partition, DateTime.UtcNow);
            while (true)
            {
                if (ct.IsCancellationRequested)
                    break;

                EventData eventData = await eventHubReceiver.ReceiveAsync(TimeSpan.FromSeconds(2));
                if (eventData == null) continue;

                string data = Encoding.UTF8.GetString(eventData.GetBytes());
                Console.WriteLine("Message received. Partition: {0} Data: '{1}'", partition, data);
            }
        }

        internal class CurrentLocation
        {
            public string Latitude { get; set; }
            public string Longitude { get; set; }

        }
    }
}
