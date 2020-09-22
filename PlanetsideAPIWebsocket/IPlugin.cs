using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    /// <summary>
    /// Interface for extensions to OutfitMembersTracker
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Called when OutfitMemberTracker with basic outfit info is constructed and before streaming socket is opened
        /// </summary>
        /// <param name="tracker">constructed OutfitMemberTracker instance</param>
        /// <param name="sessionName">Name of the session defined at start of program</param>
        void Init(OutfitMembersTracker tracker, string sessionName);

        /// <summary>
        /// Called when tracking session ended and all records are processed
        /// </summary>
        void TrackingEnded();
    }
}
