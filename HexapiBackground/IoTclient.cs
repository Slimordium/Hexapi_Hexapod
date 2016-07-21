using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.IotData;
using Amazon.IotData.Model;
using Amazon.IoT;
using Amazon.IoT.Model;
using Amazon.Runtime;


namespace HexapiBackground
{
    internal sealed class IoTClient
    {

        private AmazonIotDataClient _client;

        internal async Task Initialize()
        {
            await Task.Run(() =>
            {
                _client = new AmazonIotDataClient(@"https://av37z0myd83yw.iot.us-west-2.amazonaws.com/things/Hexapod/shadow");
            });
        }

        internal async Task<bool> Start()
        {


            var bytes = Encoding.ASCII.GetBytes("Online");

            await _client.UpdateThingShadowAsync(new UpdateThingShadowRequest
            {
                ThingName = "Hexapi",
                Payload = new MemoryStream(bytes)
            });

            return true;
        }

        internal async Task Publish(string message)
        {
            var bytes = Encoding.ASCII.GetBytes(message);

            var resp = await _client.PublishAsync(new PublishRequest
            {
                Topic = "Hexapi",
                Qos = 1,
                Payload = new MemoryStream(bytes)
            });

            Debug.WriteLine(resp.HttpStatusCode);


        }
    }
}