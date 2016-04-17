using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;

namespace HexapiLeapMotion
{
    class Program
    {
        static void Main(string[] args)
        {

            using (WebApp.Start<Startup>("http://*:8080/"))
            {
                Console.WriteLine("Server running at http://*:8080/");
                Console.ReadLine();
            }

        }
    }
}
