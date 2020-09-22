using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    /// <summary>
    /// Interface for consumers of EventRecords received by OutfitMemberTracker
    /// </summary>
    public interface IEventRecordHandler
    {
        void Handle(PlayerLoginEventRecord record);
        void Handle(PlayerLogoutEventRecord record);
        void Handle(KillEventRecord record);
        void Handle(ReviveEventRecord record);
        void Handle(VehicleDestroyedEventRecord record);
        void Handle(MinorExperienceEventRecord record);
    }
}
