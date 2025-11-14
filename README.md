CypherCore is an open source server project for World of Warcraft written in C#.

The current support game version is: 3.4.3.54261

### Prerequisites
* [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet)
* [MariaDB 10.6 or higher](https://mariadb.org/download/)
* Optional: Visual Studio 2022, Visual Studio Code or Jetbrains Rider
* Optional: [boost_1_78_0](https://www.boost.org/releases/1.78.0/) (for TrinityCore extractors compiling, This is a proven version)
* Optional: [CMake 3.31.4](https://cmake.org/download/) (for TrinityCore extractors compiling, This is a proven version)
* Optional: [MySQL Server 8.0.35](https://downloads.mysql.com/archives/community/) (instead of MariaDB, This is a proven version)

### Server Setup
* ~~Download and Complie the Extractor [Download](https://github.com/CypherCore/Tools)~~ Use TrinityCore extractors for now: [Project for compilation](https://github.com/TrinityCoreLegacy/TrinityCore/tree/3.4.3)
* Run all extractors in the wow directory
* Copy all created folders into server directory (ex: C:\CypherCore\Data)
* Make sure Conf files are updated and point the the correct folders

### Installing the database
* Download the full Trinity Core database [(TDB_full_343.24081_2024_08_17)](https://github.com/TrinityCore/TrinityCore/releases/tag/TDB343.24081)
* Extract the sql files into the core sql folder (ex: C:\CypherCore\sql)
* Make sure Conf files are updated and point the the correct folders and sql user and databases

### Playing
* Must use [Arctium WoW Client Launcher](https://arctium.io/wow)
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
* Check out our [Discord](https://discord.gg/3skVwCay7z)
* Check out [Trinity Core Wiki](https://trinitycore.atlassian.net/wiki/spaces/tc/pages/2130077/Installation+Guide) as a few steps are the same
* The project is currently under development and a lot of things have not been implemented. Updated according to updates in the appropriate branch of [TrinityCore](https://github.com/TrinityCoreLegacy/TrinityCore/tree/3.4.3)

### Notes
* The version of the Mmap/Vmap/etc extractor itself must support the current client version. The version of the extractor's output files must match the version of the processor for these files in the solution. Unfortunately, when working with outdated TC branches, the supported version of the extractor can only be determined from the commit history. To avoid errors, simply follow Server Setup section.
* To run the emulator in debug mode, you need to configure configuration files directly in the final build folder.
* It is recommended to reassign the path to the SQL files to a trusted folder whose contents will not change each time you work with different branches in version control systems (e.g. Git) to avoid database corruption due to automatic DB updates. And copy updates from the solution manually to the trusted folder (or control the automatic update settings in the configuration files in the all final build folders of all branches of the solution).

### Legal
* Blizzard, Battle.net, World of Warcraft, and all associated logos and designs are trademarks or registered trademarks of Blizzard Entertainment.
* All other trademarks are the property of their respective owners. This project is **not** affiliated with Blizzard Entertainment or any of their family of sites.
