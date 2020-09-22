using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    /// <summary>
    /// Simple Console logger for writing information with timestamp
    /// </summary>
    class Logger
    {
        public Logger()
        {

        }

        public void Log(string msg, long? gameTimestamp = null)
        {
            DateTime time;
            if (gameTimestamp.HasValue)
            {
                time = DateTimeOffset.FromUnixTimeSeconds(gameTimestamp.Value).UtcDateTime.ToLocalTime();
            }
            else
            {
                time = DateTime.Now;
            }
            StringBuilder sb = new StringBuilder();
            if (gameTimestamp.HasValue) sb.Append('[');
            sb.Append(time.ToString("HH:mm:ss"));
            if (gameTimestamp.HasValue) sb.Append(']');
            sb.Append(" > ");
            sb.Append(msg);

            Console.WriteLine(sb.ToString());
        }
    }
}
