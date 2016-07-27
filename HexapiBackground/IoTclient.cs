using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using HexapiBackground.Hardware;
using Microsoft.Azure.Devices.Client;

namespace HexapiBackground
{
    /// <summary>
    /// Azure IoT Hub MQTT client
    /// </summary>
    internal sealed class IoTClient
    {
        private DeviceClient _deviceClient;
        private readonly SparkFunSerial16X2Lcd _display;

        internal static event EventHandler<IotEventArgs> IotEvent;

        internal IoTClient(SparkFunSerial16X2Lcd display)
        {
            _display = display;
        }

        internal async Task InitializeAsync()
        {
            _deviceClient = DeviceClient.CreateFromConnectionString("", TransportType.Mqtt); //add connection string

            await _deviceClient.OpenAsync();
        }

        internal async Task SendEventAsync(string eventData)
        {
            var eventMessage = new Message(Encoding.UTF8.GetBytes(eventData));

            await _deviceClient.SendEventAsync(eventMessage);
        }

        /// <summary>
        /// This starts waiting for messages from the IoT Hub. 
        /// </summary>
        /// <returns></returns>
        internal async Task StartAsync()
        {
            while (true)
            {
                try
                {
                    var receivedMessage = await _deviceClient.ReceiveAsync(new TimeSpan(int.MaxValue));

                    if (receivedMessage == null)
                        continue;

                    foreach (var prop in receivedMessage.Properties)
                    {
                        await _display.WriteAsync($"{prop.Key} {prop.Value}");
                    }

                    await _deviceClient.CompleteAsync(receivedMessage);

                    var messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());

                    IotEvent?.Invoke(null, new IotEventArgs { EventData = receivedMessage.Properties, MessageData = messageData});
                }
                catch
                {
                    //Write out to the display perhaps
                }
            }
        }
    }

    internal class IotEventArgs : EventArgs
    {
        internal IDictionary<string, string> EventData { get; set; }

        internal string MessageData { get; set; }
    }
}