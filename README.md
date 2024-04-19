CypherCore is an open source server project for World of Warcraft written in C#.

The current support game version is: 3.4.3.54261

### Prerequisites
* .NET 8.0 SDK [Download](https://dotnet.microsoft.com/en-us/download/dotnet)
* MariaDB 10.6 or higher [Download](https://mariadb.org/download/)
* Optional: Visual Studio 2022, Visual Studio Code or Jetbrains Rider

### Server Setup
* ~~Download and Complie the Extractor [Download](https://github.com/CypherCore/Tools)~~ Use TrinityCore extractors for now: [Download](https://github.com/TrinityCore/TrinityCore/tree/wotlk_classic)
* Run all extractors in the wow directory
* Copy all created folders into server directory (ex: C:\CypherCore\Data)
* Make sure Conf files are updated and point the the correct folders and sql user and databases

### Installing the database
* Download the full Trinity Core database (TDB_full_343.23121_2023_12_20) [Download](https://github.com/TrinityCore/TrinityCore/releases/tag/TDB343.23121)
* Extract the sql files (full and updates) into the core sql folder (ex: C:\CypherCore\sql)

### Playing
* Must use Arctium WoW Client Launcher [Download](https://arctium.io/wow)
* Create link with next parameters (example for Windows): "<path>\World of Warcraft Classic\Arctium WoW Launcher.exe" --version=Classic
* Modify your "<path>\World of Warcraft\_classic_\WTF\Config.wtf"  ->  SET portal "127.0.0.1"

### Account creating
* To create your account:
    - Type: bnetaccount create
    - Example: bnetaccount create test@test test
* To set your account level:
    - Type: account set gmlevel <user#realm> 3 -1
    - Example: account set gmlevel 1#1 3 -1
* Note1:
    - The username used for setting your gmlevel is not the same as the username you create with bnetaccount.
    - You must manually find the username in auth.account.username. These are formatted as 1#1, 2#1, etc.
* Note2:
    - if you have connected before using this command you will need to relog.

### Support / General Info
* Check out our Discord [Here](https://discord.gg/3skVwCay7z)
* Check out Trinity Core Wiki as a few steps are the same [Here](https://trinitycore.atlassian.net/wiki/spaces/tc/pages/2130077/Installation+Guide)
* The project is currently under development and a lot of things have not been implemented. Updated according to updates in the appropriate branch of [TrinityCore](https://github.com/TrinityCore/TrinityCore/tree/wotlk_classic)

### Legal
* Blizzard, Battle.net, World of Warcraft, and all associated logos and designs are trademarks or registered trademarks of Blizzard Entertainment.
* All other trademarks are the property of their respective owners. This project is **not** affiliated with Blizzard Entertainment or any of their family of sites.
