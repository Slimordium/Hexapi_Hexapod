//using System;
//using System.Collections.Generic;
//using System.Text;
//using System.Threading.Tasks;
//using HexapiBackground.Hardware;
//using Microsoft.Azure.Devices.Client;
//using TransportType = Microsoft.Azure.Devices.Client.TransportType;

//namespace HexapiBackground.Iot
//{
//    /// <summary>
//    /// Azure IoT Hub MQTT client
//    /// </summary>
//    internal sealed class IoTClient
//    {
//        private DeviceClient _deviceClient;
//        private readonly SparkFunSerial16X2Lcd _display;

//        internal static event EventHandler<IotEventArgs> IotEvent;

//        private string _connectionString =
//            @"";

//        internal IoTClient(SparkFunSerial16X2Lcd display)
//        {
//            _display = display;
//        }

//        internal async Task InitializeAsync()
//        {
//            try
//            {
//                _deviceClient = DeviceClient.CreateFromConnectionString(_connectionString, TransportType.Amqp); //add connection string

//                await _deviceClient.OpenAsync();
//            }
//            catch (Exception)
//            {
//                //Display?
//            }
//        }

//        internal async Task SendEventAsync(string key, string eventData)
//        {
//            if (_deviceClient == null || string.IsNullOrEmpty(eventData))
//                return;

//            try
//            {
//                var eventMessage = new Message();

//                eventMessage.Properties.Add(key, eventData);

//                await _deviceClient.SendEventAsync(eventMessage);
//            }
//            catch (Exception)
//            {
//                //Display?
//            }
//        }

//        /// <summary>
//        /// This starts waiting for messages from the IoT Hub. 
//        /// </summary>
//        /// <returns></returns>
//        internal async Task StartAsync()
//        {
//            if (_deviceClient == null)
//                return;

//            while (true)
//            {
//                try
//                {
//                    var receivedMessage = await _deviceClient.ReceiveAsync(new TimeSpan(int.MaxValue));

//                    if (receivedMessage == null)
//                        continue;

//                    foreach (var prop in receivedMessage.Properties)
//                    {
//                        await _display.WriteAsync($"{prop.Key} {prop.Value}");
//                    }

//                    await _deviceClient.CompleteAsync(receivedMessage);

//                    var messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());

//                    IotEvent?.Invoke(null, new IotEventArgs { EventData = receivedMessage.Properties, MessageData = messageData });
//                }
//                catch
//                {
//                    //Write out to the display perhaps
//                }
//            }
//        }
//    }

//    internal class IotEventArgs : EventArgs
//    {
//        internal IDictionary<string, string> EventData { get; set; }

//        internal string MessageData { get; set; }
//    }
//}