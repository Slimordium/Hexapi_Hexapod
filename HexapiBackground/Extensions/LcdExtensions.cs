using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexapiBackground.Extensions
{
    internal static class LcdExtensions
    {
        internal static void WriteToLcd(this string text)
        {
            Display.Write(text);
        }

        internal static void WriteToLcd(this string text, int line)
        {
            Display.Write(text, line);
        }

    }
}
