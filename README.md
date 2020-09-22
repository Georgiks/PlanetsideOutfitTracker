# PlanetsideOutfitTracker
Usage:
------
`> Tracker.exe <outfit tag> <sessionname>`

*Example*: `> Tracker.exe DIG mysession`
After gathering is finished (enter is pressed), new UI window with data preview and save options is shown

TODO:
-----
+ make it more error-proof (reconnect to API stream if connection lost) - now stream is ended
+ ~~__add spot assists and more to tracker__~~ (done - spot assists are unaccessible from game)
+ add killstreaks, multikills etc. notices
+ ~~add cache timeout for players? for long-running program uses~~ (done - 10 minutes)
+ hint to playercache to fetch given players at once (all outfit (online) players)
+ OutfitKills and OutfitDeaths needs to check outfit ID, not tag (can be empty) and check for null

Notes:
------
* Vehicle kills does not count turret kills (glaive too)

Statistics counter diagram:
![alt text](https://github.com/Georgiks/PlanetsideOutfitTracker/blob/master/StatisticsDiagram.png "How different stats include other stats")
