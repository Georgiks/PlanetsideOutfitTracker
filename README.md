# PlanetsideOutfitTracker
Usage:
------
`> Tracker.exe <outfit tag> <sessionname>`

*Example*: `> Tracker.exe DIG mysession`
After gathering is finished (enter is pressed), 2 new files are created containing all recorded events and online player statistics

TODO:
-----
+ make it more error-proof (reconnect to API stream if connection lost)
+ ~~__add spot kills and more to tracker__~~ (done)
+ __add killstreaks, multikills etc. notices__
+ add cache timeout for players? for long-running program uses
+ player online time is not always correct in multithreaded environment (one thread registers him as offline before another thread registers him online with earlier timestamp)
+ hint to playercache to fetch given players at once (all outfit (online) players)
+ __output data from class instead of printing them at end__

Notes:
------
* Tracking a lot of online players for few minutes after launch, events can be delyed even few minutes. Although that should not happen after some time.
  The reason is players are fetched from database on demand (and then cached), and when there is a lot of unknown players to program, it takes some time before
  they are all acquired from DB. Events involving cached players should then be processed in no time.
  With 125 online players, 4-5 minutes before events are realtime.

  In 7 minutes, 500 players cached (DIG; 3.00 PM Sunday playing).

  After 30 minutes, 1100 players cached (~1700 playing at that time).

* Vehicle kills does not count turret kills (glaive too)

Statistics counter diagram:
![alt text](https://github.com/Georgiks/PlanetsideOutfitTracker/blob/master/StatisticsDiagram.png "How different stats include other stats")
