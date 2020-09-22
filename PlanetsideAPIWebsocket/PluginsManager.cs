using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    /// <summary>
    /// Manager handling search for available plugins
    /// </summary>
    static class PluginsManager
    {
        const string PluginsDirectory = "plugins";

        /// <summary>
        /// Searches for plugins in 'plugins' subdirectory and return list of initialized instances of found plugins
        /// </summary>
        public static List<IPlugin> GetAvailablePlugins()
        {
            List<IPlugin> plugins = new List<IPlugin>();

            var path = Path.Combine(Environment.CurrentDirectory, PluginsDirectory);
            if (!Directory.Exists(path.ToString()))
                return plugins;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path.ToString(), "*.dll"))
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(Path.Combine(path.ToString(), file));
                        //Console.WriteLine($"Found assembly: {assembly.FullName}");
                        foreach (var type in assembly.DefinedTypes)
                        {
                            if (type.ImplementedInterfaces.Contains(typeof(IPlugin)) && !type.IsAbstract)
                            {
                                plugins.Add((IPlugin)Activator.CreateInstance(type));
                                Console.WriteLine($"Found plugin: {type.FullName}");
                            }
                        }

                    } catch (BadImageFormatException)
                    {
                        throw;
                    }
                }
            } catch (IOException)
            {
                throw;
            }

            return plugins;
        }
    }
}
