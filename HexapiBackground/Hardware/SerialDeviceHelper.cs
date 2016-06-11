using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;

namespace HexapiBackground.Hardware
{
    internal static class SerialDeviceHelper
    {
        internal static async Task<SerialDevice> GetSerialDevice(string identifier, int baudRate, TimeSpan readTimeout, TimeSpan writeTimeout)
        {
            var deviceInformationCollection = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
            var selectedPort = deviceInformationCollection.FirstOrDefault(d => d.Id.Contains(identifier) || d.Name.Equals(identifier));

            if (selectedPort == null)
            {
                Debug.WriteLine($"Could not find device information for {identifier}");

                return null;
            }

            var serialDevice = await SerialDevice.FromIdAsync(selectedPort.Id);

            if (serialDevice == null)
            {
                Debug.WriteLine($"Could not open serial port at {identifier}");

                return null;
            }

            Debug.WriteLine($"Found - {identifier} as {selectedPort.Id}");

            serialDevice.ReadTimeout = readTimeout;
            serialDevice.WriteTimeout = writeTimeout;
            serialDevice.BaudRate = (uint)baudRate;
            serialDevice.Parity = SerialParity.None;
            serialDevice.StopBits = SerialStopBitCount.One;
            serialDevice.DataBits = 8;
            serialDevice.Handshake = SerialHandshake.None;

            return serialDevice;
        }
    }
}