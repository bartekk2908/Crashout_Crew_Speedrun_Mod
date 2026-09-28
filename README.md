# Speedrun Mod for Crashout Crew

This mod adds a live timer along with a delta timer. The delta timer allows for a simple imitation of the LiveSplit program. The time is measured exactly like the built-in game timer (it only measures the duration of shifts). Best split times are saved, which allows you to compare your current attempt with your best one. This is helpful when you want to speedrun the game. Additionally, there is an option for quick level restarting and exiting to the menu.

## Screenshots

**In-Game Timer & Delta Comparisons:**

![Live Timer](https://github.com/bartekk2908/Crashout_Crew_Speedrun_Mod/blob/master/pictures/1.png?raw=true)

![Time Loss - Red Delta](https://github.com/bartekk2908/Crashout_Crew_Speedrun_Mod/blob/master/pictures/2.png?raw=true)

![Time Save - Green Delta](https://github.com/bartekk2908/Crashout_Crew_Speedrun_Mod/blob/master/pictures/3.png?raw=true)

![Best Segment - Gold Delta](https://github.com/bartekk2908/Crashout_Crew_Speedrun_Mod/blob/master/pictures/4.png?raw=true)

**Menu Integration:**

![Personal Best in Menu](https://github.com/bartekk2908/Crashout_Crew_Speedrun_Mod/blob/master/pictures/5.png?raw=true)

## Features

* **Live Timer & Delta Timer:** Measures active shift time and compares it against your best saved splits.
* **Menu UI:** Displays your personal best time, Sum of Best, and total attempt counter directly in the level selection menu.
* **Number of Players Tracking:** Times, splits, and attempts are automatically tracked and saved separately depending on the number of players in the lobby.
* **Quick Reset:** A dedicated keybind to instantly start a run or safely abort it.
* **Configurable:** Toggle visibility of specific UI elements, adjust text sizes, change colors, and rebind keys.

## Requirements

* BepInEx (version 5.x)

## Installation

1. Install BepInEx in your game directory.
2. Download `SpeedrunMod.dll` from the Releases page.
3. Place the `.dll` file into the `BepInEx/plugins/` folder.
4. Launch the game. The config and save files will be generated automatically.

## Controls

* **Quick Reset (Default: F9):**
  * When used in the lobby, it forces a quick start for the next run.
  * When used during an active run, it immediately aborts the mission and safely returns you to the lobby menu.
  * The key can be changed in the configuration file.

## Configuration

The mod creates a configuration file at `BepInEx/config/com.sialala.speedrun.cfg` after the first launch. You can edit it with any text editor to:
* Enable or disable the main timer, delta timer, or menu UI elements (PB, SoB, Attempts).
* Change the Quick Reset key.
* Adjust font sizes.
* Change text colors using standard HEX codes.

## Save Data

Personal bests, best segments, and attempt counters are saved in `SpeedrunSplits.json` inside the `BepInEx/plugins/` folder. The records are categorized by level and player count. You can back up, share, or delete this file to reset your times.