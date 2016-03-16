using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace HexapiBackground
{
    internal static class FileHelpers
    {
        internal static async Task<string> ReadStringFromFile(string filename)
        {
            var text = string.Empty;

            try
            {
                var appFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;

                Debug.WriteLine($"AppFolder {appFolder.Path}");

                var storageFile = await appFolder.GetFileAsync(filename);
                var stream = await storageFile.OpenAsync(FileAccessMode.Read);
                var buffer = new Windows.Storage.Streams.Buffer((uint)stream.Size);

                await stream.ReadAsync(buffer, (uint)stream.Size, InputStreamOptions.None);

                text = Encoding.UTF8.GetString(buffer.ToArray());
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            return text;
        }

        //https://github.com/Microsoft/Windows-universal-samples/blob/master/Samples/ApplicationData/cs/Scenario1_Files.xaml.cs
        internal static void SaveStringToFile(string filename, string content)
        {
            var bytesToAppend = Encoding.UTF8.GetBytes(content.ToCharArray());

            Task.Factory.StartNew(async () =>
            {
                try
                {
                    var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(filename, CreationCollisionOption.OpenIfExists).AsTask();

                    Debug.WriteLine($"Writing {file.Path}");

                    using (var stream = await file.OpenStreamForWriteAsync())
                    {
                        stream.Position = stream.Length;
                        stream.Write(bytesToAppend, 0, bytesToAppend.Length);
                    }
                }
                catch
                {
                    Debug.WriteLine("Save failed for " + filename);
                }
            });
        }
    }
}