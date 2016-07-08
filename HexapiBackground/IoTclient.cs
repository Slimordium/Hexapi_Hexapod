using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Amazon.IoT;
using Amazon.IoT.Model;
using Amazon.Runtime;


namespace HexapiBackground
{
    internal sealed class IoTClient
    {
        //private DeviceClient _deviceClient;

        internal async Task<bool> Start()
        {
            var credentials = new BasicAWSCredentials("AKIAJ65L4IEEOERWDLIA", "uGmcAXcDYMIN96OfW32XZMx4BgMMbYLbzfs7coLV");

            var config = new AmazonIoTConfig();
            config.ServiceURL = @"https://av37z0myd83yw.iot.us-west-2.amazonaws.com/things/Hexapod/shadow";



            var client = new AmazonIoTClient(credentials, config);
            
                        


            var payload = new AttributePayload();
            payload.Attributes.Add("YPR", "1,2,3");
            

            var request = new UpdateThingRequest();
            request.ThingName = "Hexapod";
            request.AttributePayload = payload;

            var r = await client.UpdateThingAsync(request);


            try
            {
                //_deviceClient = DeviceClient.CreateFromConnectionString(DeviceConnectionString, TransportType.Http1);

                //await _deviceClient.OpenAsync();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal async Task SendEvent(string message)
        {
            //if (_deviceClient == null)
            //    return;

            try
            {
                //var eventMssage = new Message(Encoding.UTF8.GetBytes(message));

                //await _deviceClient.SendEventAsync(eventMssage);
            }
            catch (Exception)
            {
                //   
            }
        }

        internal async Task ReceiveCommands()
        {
            //if (_deviceClient == null)
            //    return;

            //while (true)
            //{
            //    var receivedMessage = await _deviceClient.ReceiveAsync();

            //    if (receivedMessage != null)
            //    {
            //        var messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
            //        Debug.WriteLine("\t{0}> Received message: {1}", DateTime.Now.ToLocalTime(), messageData);

            //        await _deviceClient.CompleteAsync(receivedMessage);
            //    }

            //    //  Note: In this sample, the polling interval is set to 
            //    //  10 seconds to enable you to see messages as they are sent.
            //    //  To enable an IoT solution to scale, you should extend this //  interval. For example, to scale to 1 million devices, set 
            //    //  the polling interval to 25 minutes.
            //    //  For further information, see
            //    //  https://azure.microsoft.com/documentation/articles/iot-hub-devguide/#messaging
            //    await Task.Delay(TimeSpan.FromSeconds(10));
            //}
        }
    }
}