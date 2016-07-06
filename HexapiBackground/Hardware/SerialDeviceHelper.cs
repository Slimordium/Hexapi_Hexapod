using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;

namespace HexapiBackground.Hardware
{
    internal class SerialDeviceHelper
    {


        internal async Task<SerialDevice> GetSerialDevice(string identifier, int baudRate, TimeSpan readTimeout, TimeSpan writeTimeout)
        {
            var deviceInformationCollection = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
            var selectedPort = deviceInformationCollection.FirstOrDefault(d => d.Id.Contains(identifier) || d.Name.Equals(identifier));

            if (selectedPort == null)
            {
                //await Display.Write($"not found {identifier}");
                return null;
            }

            var serialDevice = await SerialDevice.FromIdAsync(selectedPort.Id);

            if (serialDevice == null)
            {
                //await Display.Write($"not opened {identifier}");
                return null;
            }

            //await Display.Write($"Found - {identifier}");

            serialDevice.ReadTimeout = readTimeout;
            serialDevice.WriteTimeout = writeTimeout;
            serialDevice.BaudRate = (uint)baudRate;
            serialDevice.Parity = SerialParity.None;
            serialDevice.StopBits = SerialStopBitCount.One;
            serialDevice.DataBits = 8;
            serialDevice.Handshake = SerialHandshake.None;

            Debug.WriteLine($"Found - {identifier}");

            return serialDevice;
        }

        internal static async void ListAvailablePorts()
        {
            Debug.WriteLine("Available Serial Ports------------------");
            foreach (var d in await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector()))
            {
                Debug.WriteLine($"{d.Id}");
            }
            Debug.WriteLine("----------------------------------------");
        }
    }
}