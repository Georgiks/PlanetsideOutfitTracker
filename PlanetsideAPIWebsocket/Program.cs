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
            if (args.Length != 2)
            {
                Console.WriteLine("Usage:\n  [cmd] <outfit_alias> <all_events_logfile>");
                return;
            }
            // ensure DatabaseCache singleton is created in safe non-threaded environment
            InitiateCaches();

            try
            {
                var tracker = new OutfitMembersTracker(args[0], args[1]);
                Console.WriteLine("Waiting for enter...");
                Console.ReadLine();
                tracker.FinishGathering();

                string statsFileName = args[1] + "_stats.csv";
                Console.WriteLine("Writing statistics to " + statsFileName);
                File.WriteAllText(statsFileName, tracker.GetPlayerStatistics());
                Console.WriteLine("Statistics written!");
            }
            catch (JsonException e)
            {
                Console.WriteLine(e.Message);
                throw;
            } catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.Error.WriteLine(e.ToString());
                throw;
            }


        }
        public static void InitiateCaches()
        {
            var t1 = Task.Run(() => RuntimeHelpers.RunClassConstructor(typeof(PlayerCache).TypeHandle));
            var t2 = Task.Run(() => RuntimeHelpers.RunClassConstructor(typeof(VehicleCache).TypeHandle));
            var t3 = Task.Run(() => RuntimeHelpers.RunClassConstructor(typeof(WeaponsCache).TypeHandle));
            var t4 = Task.Run(() => RuntimeHelpers.RunClassConstructor(typeof(LoadoutCache).TypeHandle));
            Task.WhenAll(t1, t2, t3, t4).GetAwaiter().GetResult();
        }
    }
}
