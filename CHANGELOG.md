# Changelog:

1.1.2
- Fixed a bug where bridges with split joints could break



1.1.1
- Changed how the twitch detection node is added, it will now replace an existing node instead of adding a new one. This should fix any butterfly effects.



1.1.0
- Added save slots for streamers
- Streamer id's will now be cashed to make it less likely for the "too many requests" error to occur
- Fixed issue where an empty layout would load when the loading of a layout failed
- Fixed issue where it would keep displaying "sending bridge (3/3)" after an error occured
- Displays message if poly bridge is not linked with twitch



1.0.2
- Cleaned the code a bit
- Ptf now checks for updates
- Made the "too many requests" error less likely to occur when typing in streamer name
