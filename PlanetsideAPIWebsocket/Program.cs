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
        // simple logger instance
        public static Logger Logger { get; } = new Logger();


        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage:\n  [cmd] <outfit_alias> <session_name>");
                return;
            }

            // ensure DatabaseCache singleton is created in safe non-threaded environment
            InitiateCaches();

            try
            {

                List<IPlugin> plugins = PluginsManager.GetAvailablePlugins();
                Console.WriteLine("Found plugins: " + plugins.Count);

                // create tracker - that fill fetch all outfit information including list of members
                var tracker = new OutfitMembersTracker(args[0], args[1]);

                // initiate plugins
                foreach (var plugin in plugins)
                {
                    plugin.Init(tracker, args[1]);
                }
                // start the streaming websocket
                tracker.StartListening();

                Console.WriteLine("Press ENTER to finish tracking...");
                Console.ReadLine();

                // end tracking, close streaming websocket and wait for all events processings in progress
                tracker.Finish();
                
                // inform plugins that tracking has ended
                foreach (var plugin in plugins)
                {
                    plugin.TrackingEnded();
                }

            } catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.Error.WriteLine(e.ToString());
                throw;
            }


        }

        /// <summary>
        /// Construct caches
        /// </summary>
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
