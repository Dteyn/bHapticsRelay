bHapticsRelay v0.3.0 by Dteyn
https://github.com/Dteyn/bHapticsRelay

Welcome to bHapticsRelay!

This app was built to help you enjoy haptic (vibration/rumble) feedback with your bHaptics gear in games that don't natively support it.

Whether you're a gamer, a modder, or just a tinkerer, bHapticsRelay makes it super simple to connect in-game events to your bHaptics vest, arms, gloves, and more.

WHAT DOES IT DO?
----------------

bHapticsRelay listens for special commands from games or mods - either written into a text log file, or sent over the network (WebSocket). When it sees these commands, it tells your bHaptics Player app to trigger the matching haptic effect (like a rumble for an explosion, or a heartbeat when you're low on health).

- No coding required for end users!
- No need to mess with the official SDK or complicated integration.
- As long as the mod or game writes the right command, bHapticsRelay will do the rest.

QUICK START GUIDE
-----------------

First, make sure you have the bHaptics Player app already installed and running. You can download it from www.bhaptics.com.

Then, using this mod is as simple as:

1. Unzip everything from this package into a folder on your PC. You can put it anywhere you like (desktop, game folder, wherever).
   
2. Double-click bHapticsRelay.exe to start the bridge. You'll see the app window pop up with the game/mod title, version, and a test button.

3. You may need to select the game's log file - click Browse and select the game's log file. Once chosen, bHapticsRelay will remember the log location.

4. Use the Test button to make sure you feel the effects on your gear.

5. Play your game! If everything's set up, haptic effects should just work when in-game events trigger them.


TROUBLESHOOTING & NOTES
-----------------------

- Paths in config.cfg file are relative to where you unzipped the folder. So if you move the folder, you don't have to change any settings - just drop the folder anywhere you like.

- Don't rename bHapticsRelay.exe! The app needs its original name to start correctly.

- DefaultConfig.json is optional. The mod creator may choose to include this file which provides offline support, but it is not required for bHapticsRelay to function.

- All other files in this zip are required for things to run smoothly.

- Double-check the mod instructions, or contact the mod creator.


LICENSE & SDK AGREEMENT
-----------------------

Use of bHapticsRelay (and the bHaptics SDK files included) is subject to the bHaptics SDK license agreement:  

https://bhaptics.gitbook.io/license-sdk/

bHapticsRelay is an unofficial, community-driven tool. It's not made or endorsed by bHaptics Inc.
  
This tool and all original code are released under the MIT License. See the LICENSE file for full details.

You are free to use and modify bHapticsRelay for non-commercial, personal, or modding projects. If you want to make commercial products using bHaptics SDK, contact bHaptics Inc. directly at support@bhaptics.com.


CREDITS & THANKS
----------------

bHaptics Inc. for the awesome haptic hardware, SDK, and community support! (www.bhaptics.com)

Modding Community: Huge thanks to all the modders, testers, and VR streamers who helped grow bHaptics into what it is today!

Special shoutouts:

 - Astienth - For amazing haptics mods and support: https://github.com/Astienth/VR-Mods-Projects

 - Florian Fahrenberger - More cool bHaptics mods and guides: 
 https://github.com/floh-bhaptics

 - FarmertrueVR - For community vibes, VR streaming, and feedback: https://farmertrueVR.com

For updates, help, or to share your mods, check out the bHaptics Discord and GitHub community.


QUESTIONS OR FEEDBACK?
----------------------

If you find a bug, want to request a feature, or have an idea, check the GitHub repo:

https://github.com/Dteyn/bHapticsRelay

HAVE FUN & FEEL THE GAME! :)

If you enjoy bHapticsRelay and want to support the developer: https://ko-fi.com/Dteyn
