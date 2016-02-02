using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace HexapiBackground{
    internal static class Helpers
    {
        internal static string ReadStringFromFile(string filename)
        {
            return Task.Factory.StartNew(() =>
            {
                var local = Windows.Storage.ApplicationData.Current.LocalFolder;
                var stream = local.OpenStreamForReadAsync(filename).Result;
                string text;

                using (var reader = new StreamReader(stream))
                {
                    text = reader.ReadToEnd();
                }

                return text;
            }).Result;
        }

        internal static void SaveStringToFile(string filename, string content)
        {
            Task.Factory.StartNew(() =>
            {
                var bytesToAppend = System.Text.Encoding.UTF8.GetBytes(content.ToCharArray());
                var file = ApplicationData.Current.LocalFolder.CreateFileAsync(filename, CreationCollisionOption.OpenIfExists).AsTask().Result;
                var stream = file.OpenStreamForWriteAsync().Result;

                stream.Position = stream.Length;
                stream.Write(bytesToAppend, 0, bytesToAppend.Length);
                stream.Dispose();
            });
        }
    }
}