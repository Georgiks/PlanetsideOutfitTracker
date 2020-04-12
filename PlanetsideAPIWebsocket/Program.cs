using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using System.IO;
using JsonParser;
using System.Net;
using System.Runtime.CompilerServices;

namespace PlanetsideAPIWebsocket
{
    class Program
    {
        public static Logger Logger { get; } = new Logger();
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage:\n  [cmd] <outfit_alias>");
                return;
            }
            // ensure DatabaseCache singleton is created in safe non-threaded environment
            InitiateCaches();

            //try
            //{
                var tracker = new OutfitMembersTracker(args[0]);
            //}
            //catch (JsonException e)
            //{
            //    Console.WriteLine(e.Message);
            //    throw;
            //} catch (Exception e)
            //{
            //    Console.WriteLine(e.Message);
            //    Console.Error.WriteLine(e.ToString());
            //    throw;
            //}

            Console.WriteLine("Waiting for enter...");
            Console.ReadLine();
            tracker.FinishGathering();

        }
        public static void InitiateCaches()
        {
            RuntimeHelpers.RunClassConstructor(typeof(PlayerCache).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(VehicleCache).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(WeaponsCache).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(LoadoutCache).TypeHandle);
        }
    }
}
