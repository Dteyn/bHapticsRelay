# bHapticsRelay

![logo](/assets/bHapticsRelay-logo.jpg)

![Latest release](https://img.shields.io/github/v/release/Dteyn/bHapticsRelay?include_prereleases)
![Downloads](https://img.shields.io/github/downloads/Dteyn/bHapticsRelay/total)
![Visitors](https://visitor-badge.laobi.icu/badge?page_id=Dteyn/bHapticsRelay)

> [!WARNING]
> This project is a **Work-In-Progress** and should be considered Early Access at this point. If you encounter any troubles, please open an Issue so the tool can be improved!

![screenshot-v030](/assets/screenshot-v030.jpg)

## Table of Contents

- [What is bHapticsRelay?](#what-is-bhapticsrelay)
  - [How does it work?](#how-does-it-work)
  - [bHaptics SDK Agreement](#bhaptics-sdk-agreement)
- [General Overview](#general-overview)
  - [Project Scope](#project-scope)
  - [Getting Started](#getting-started)
  - [Architecture and Features Overview](#architecture-and-features-overview)
  - [Offline Fallback](#offline-fallback)
  - [Command Parsing Logic](#command-parsing-logic)
  - [Log File Monitoring Mode](#log-file-monitoring-mode)
  - [WebSocket Server Mode](#websocket-server-mode)
  - [Command Handling](#command-handling)
- [Configuration (`config.cfg`)](#configuration-configcfg)
- [Deployment](#deployment)
- [Compilation from Source](#compilation-from-source)
- [Game Integration Guide](#game-integration-guide)
  - [Log File Tailing](#log-file-tailing)
  - [WebSocket Mode](#websocket-mode)
- [Example Implentations](#example-implentations)
- [Future Ideas](#future-ideas)
- [Contributions](#contributions)
- [Reporting Issues](#reporting-issues)
- [Acknowledgements and Shout-Outs](#acknowledgements-and-shout-outs)
- [Bhaptics SDK2 API Reference](#bhaptics-sdk2-api-reference)
    - [SDK Connection & Initialization](#sdk-connection--initialization)
      - [`registryAndInit`](#registryandinit)
      - [`registryAndInitHost`](#registryandinithost)
      - [`wsIsConnected`](#wsisconnected)
      - [`wsClose`](#wsclose)
      - [`reInitMessage`](#reinitmessage)
    - [Playback Controls](#playback-controls)
      - [`play`](#play)
      - [`playParam`](#playposparam)
      - [`playDot`](#playdot)
      - [`playWaveform`](#playwaveform)
      - [`playPath`](#playpath)
      - [`playWithStartTime`](#playwithstarttime)
      - [`playLoop`](#playloop)
      - [`pause`](#pause)
      - [`resume`](#resume)            
      - [`stop`](#stop)
      - [`stopByEventId`](#stopbyeventid)
      - [`stopAll`](#stopall)
      - [`isPlaying`](#isplaying)
      - [`isPlayingByRequestId`](#isplayingbyrequestid)
      - [`isPlayingByEventId`](#isplayingbyeventid)
    - [Device and Connectivity Utilities](#device-and-connectivity-utilities)
      - [`isbHapticsConnected`](#isbhapticsconnected)
      - [`ping`](#ping)
      - [`pingAll`](#pingall)
      - [`swapPosition`](#swapposition)
      - [`getDeviceVsm`](#getdevicevsm)
      - [`getDeviceInfoJson`](#getdeviceinfojson)
    - [Player Application Controls](#player-application-controls)
      - [`isPlayerInstalled`](#isplayerinstalled)
      - [`isPlayerRunning`](#isplayerrunning)
      - [`launchPlayer`](#launchplayer)
    - [Advanced Message & Mapping Retrieval](#advanced-message--mapping-retrieval)
      - [`getEventTime`](#geteventtime)
      - [`getHapticMappingsJson`](#gethapticmappingsjson)
- [Licensing Notes](#licensing-notes)

## What is bHapticsRelay?

**bHapticsRelay** is a tool for modders and developers who want to add bHaptics support to any game or app.

> [!TIP]
> If you're just looking for the SDK2 API reference, you can find it here: [**bHaptics SDK2 API reference**](https://github.com/Dteyn/bHapticsRelay?tab=readme-ov-file#bhaptics-sdk2-api-reference)

## How does it work?

The app works by monitoring a game's log file (or WebSocket) for haptic commands, and in turn triggering effects on bHaptics gear.

As long as you can make your game or mod write lines to a log file (or send websocket messages), this tool can translate those into haptics on your bHaptics gear.

No SDK integration needed - bHapticsRelay takes care of that for you!

**Examples:**
- OpenMW (Morrowind) mod prints `[bHaptics]play,magic_cast` to the game log when casting magic, triggering vest and sleeve effects.
- A driving game writes `[bHaptics]play,car_crash` to game log when the car hits something, triggering vest vibrations.
- An RPG game writes `play,heartbeat` over websocket when player is low on health.

In each of these cases, bHapticsRelay will forward these events to the bHaptics Player.

This makes it easy to integrate bHaptics support any game as long as you can control writing to log file (or websocket) based on in-game actions.

## bHaptics SDK Agreement

> [!IMPORTANT]
> Use of bHaptics SDK (and therefore, bHapticsRelay) is subject to the bHaptics SDK agreement (https://bhaptics.gitbook.io/license-sdk/).

### No Affiliation with bHaptics

This project is an **unofficial community-driven tool**. It is not affiliated with or endorsed by bHaptics Inc.

For more details, see the [_Licensing Notes_](#licensing-notes) section at the end of this document.

## bHaptics SDK2 Integration

bHapticsRelay uses the newer **bHaptics SDK version 2** which provides some new features and enhancements vs the previous SDK1.

New features in SDK2 include (but are not limited to):

- New playback methods such as `playLoop`, `playParam`, and `playWaveform` which allow for greater control of haptic feedback compared to SDK1.

- Functions for getting device information and status, devices connected, as well as player application management (check if player is installed / running, launch player).

- Offline mode is supported via the 'Default Config' option, which bHapticsRelay supports and is detailed further down in this guide.

I've also included a full [**bHaptics SDK2 API reference**](https://github.com/Dteyn/bHapticsRelay?tab=readme-ov-file#bhaptics-sdk2-api-reference) at the bottom of this document, which includes examples for using via bHapticsRelay, as well as C# examples for integrating SDK2 into other projects.

# General Overview

**bHapticsRelay** is meant as a tool for modders who want to add tactile feedback to games via mods or scripts. If you are comfortable editing config files and writing game scripts (ie. in Lua or similar) to output events, bHapticsRelay will allow you to trigger bHaptics feedback without writing a full plugin in C++.

> [!NOTE]  
> You are expected to be familiar with basic modding concepts and must have access to the bHaptics Developer tools (Designer and Developer Portal).

### Project Scope

**bHapticsRelay** itself is a hobby project provided for free and without any warranties. It's ***not an official bHaptics product***, and it doesn't come with any game-specific profiles itself. The tool is essentially a lightweight integration layer and uses bHaptics's proprietary SDK libraries under the hood.

Modders must use the official bHaptics Designer and Developer Portal to create haptic patterns and events, and in turn may use this relay to trigger those events from their game mod.

Once deployed, players must use the official bHaptics Player software in conjunction with bHapticsRelay in order to feel the haptic effects.

# Getting Started

Before using this app, you should become familiar with the official **bHaptics SDK and Developer Documentation** to learn how everything works.

> [!IMPORTANT]  
> Take the time to read and understand the bHaptics official docs. You'll thank yourself later!

### [Getting Started | bHaptics Developer](https://docs.bhaptics.com/start-here/getting-started)

Nearly everything in the guide is relevant to what we're doing here; bHapticsRelay only replaces the SDK portion which is normally baked into the game itself.

## Architecture and Features Overview

bHapticsRelay's architecture is designed to intercept game-generated events (via log lines or network messages) and dispatch them as haptic feedback through the bHaptics SDK. The tool loads a config, listens for incoming commands from games (either by monitoring a log file or via WebSocket), and triggers the appropriate haptic events in the bHaptics Player.

The general workflow of the application is:

**Config Loading**: On startup, bHapticsRelay reads its `config.cfg` file to determine settings like which mode to use (log or WebSocket), file paths, network ports, and offline mode details. It also loads any offline event data if provided (see *Offline Fallback* below). This allows modders to adapt the tool to different games without recompiling.

**bHaptics Player**: The relay initializes the bHaptics SDK connection using SDK2 (via `bhaptics_library.dll`). It checks if the bhaptics Player is installed and/or running. If installed but not running, a 'Launch Player' button is provided.

**Registration**: Once initialized, the app registers the haptic events either via an online API call (using your App ID/API key) or via a local JSON file (offline mode).

**Tail/Listen for Events**: After registration, the tool then begins tailing the log or listening on websocket, and in turn will send commands to the Player to play specific feedback patterns.

**Modes Supported**: Currently, bHapticsRelay supports two input methods for receiving haptic commands from games. More methods may be added in the future.

- **Log File Tailing Mode**: In this mode, the relay monitors a specified text log file in real-time. It searches each new line for the token `[bHaptics]command`. If a line contains `[bHaptics]command`, the relay will interpret this as a haptic trigger.

- **WebSocket Server Mode**: In this mode, the relay hosts an internal WebSocket server and listens for incoming commands. Game mods or external scripts can connect to this WebSocket (e.g. `ws://127.0.0.1:50451`) and send commands to trigger haptics.

The WebSocket server also sends replies with return values, allowing for two-way communication with bHaptics Player. This is useful if the modding environment has networking support or if you prefer a direct programmatic approach instead of writing to a log file.

**Haptic Event Dispatch**: Whether an event command comes from the tail log or the WebSocket, the relay handles it the same way: it parses the event name and any parameters and calls the appropriate bHaptics SDK function. Events are identified by their Event ID (a string name, like `heartbeat`) which must match an event in your bHaptics app configuration in the Developer Portal.

Modders can refer to the **bHaptics SDK2 API Reference** at the bottom of this document for details on all the commands supported, and how to use them.

A [**Test Suite**](#test-suite) is also provided which allows modders to test bHapticsRelay and become familiar with the various functions that SDK2 offers.

**Player Connection & Checks**: The relay continuously ensures it has a valid connection to bHaptics Player. If the Player isn't running or if the connection is lost, bHapticsRelay will log an error or attempt to reconnect.

**Debug Logging**: Debug logging statements are built into the codebase, which are normally disabled on Release version. When developing a mod, using the Debug version is recommended to obtain the most detailed logging info for setup and troubleshooting.

## Offline Fallback

bHaptics SDK2 supports including an offline fallback for scenarios where an internet connection is unavailable to the end user of the mod.

In standard SDK2 usage, the bHaptics Player normally fetches your haptic project's event configuration from the cloud using your App ID and API key.

The offline fallback bypasses any online requirement by loading a JSON config file which you can download from the bHaptics Developer Portal for your app (see "_Download Default Config_" in lower left of Dev portal).

The 'Default Config' JSON contains the definitions of all haptic patterns and events in your project and allows bHapticsRelay to continue working even in cases where there is no internet connection.

This is similar to providing .tact files when using SDK1 -- using the offline JSON with SDK2, the mod can be completely self-contained.

## Command Parsing Logic

As mentioned above, bHapticsRelay supports two modes for receiving commands: **Log File Monitoring** and **WebSocket**.

### Log File Monitoring Mode
In this mode, the relay continuously watches a specified log file. It specifically looks for log entries tagged with `[bHaptics]`. Anything following that tag is considered a Command.

For example:

`[bHaptics]play,explosion`

When bHapticsRelay sees a tagged entry in the log, it extracts the command immediately following `[bHaptics]` - in this case, `play,explosion`.

> [!NOTE]  
> In Log File Monitoring mode, bHapticsRelay processes the commands and sends them to bHaptics Player, but doesn't return or log any feedback about the result.

### WebSocket Server Mode
In this mode, the relay can receive commands directly over WebSocket connections. In WebSocket mode, ***you do not need*** the `[bHaptics]` tag. Instead, you simply send commands directly, such as:

`play,explosion`

> [!TIP]
> The WebSocket mode **does** send back results to the client after processing each command, letting you know immediately if your command succeeded or failed, or providing request ID's or data as requested.

### Command Handling
Regardless of the mode used, the parsing logic for the command handler remains consistent. The relay splits each incoming command string by commas. The first part is interpreted as the function name (like `play`, `stop`, etc.), and any remaining parts are treated as parameters.

Once parsed, the command and its parameters are passed directly to the corresponding methods provided by the bHaptics SDK2 `bhaptics_library.dll`.

# Configuration (`config.cfg`)

The core of every bHapticsRelay-powered mod is the `config.cfg` file. This required file configures the app title, tells bHapticsRelay which mode to run in (Log tail/WebSocket), and specified your bHaptics API key/app ID.

Below is a breakdown of each setting, what it does, and some practical examples.

## `[Settings]` section

### `Title`

   * **What it does:** Sets the window title and label in the bHapticsRelay UI.
   * **Valid values:** Any string (ideally the name of the game or mod).
   * **Use case:** If making a Streets of Rogue mod, you might use:

   ```ini
   Title=Streets of Rogue
   ```

   bHapticsRelay will display this string on the UI interface and in the titlebar as: 'bHapticsRelay for Streets of Rogue v{Version}'

---

### `Version`

   * **What it does:** Displays your mod's version in the title bar - handy for debugging and support.
   * **Valid values:** A semantic version string (e.g., `1.0.0`, `2.1.5-beta`).
   * **Use case:** During development you could bump this each build:

   ```ini
   Version=0.9.2-alpha
   ```

   bHapticsRelay will display this string in the titlebar as: 'bHapticsRelay for Streets of Rogue v0.9.2-alpha'   

---

### `Mode`

   * **What it does:** Chooses how bHapticsRelay listens for in-game events:

     * `Tail`: Reads a local log file and watches for [bHaptics] tags
     * `Websocket`: Listens on a port for SDK2 commands
   * **Valid values:** `Tail`, `Websocket` (case-insensitive)
   
   **Use case:** If your game or mod writes haptic events to `game.log`, go with Tail:

   ```ini
   Mode=Tail
   ```

   If you're using a custom client that pushes events over WS:

   ```ini
   Mode=Websocket
   ```

   Websocket mode will send replies if the command returns anything.

---

### `LogFile`

   * **What it does:** (Tail mode only) Specifies which log file to watch for **[bHaptics]** commands.
   * **Valid values:** Path to any plaintext log containing valid bHapticsRelay commands.
   * **Use case:** Many games write to `game.log`:

   ```ini
   LogFile=Logs/game.log
   ```

---

### `Port`


   * **What it does:** (Websocket mode only) Sets the TCP port for incoming command messages.
   * **Valid values:** Any open port (recommended range: `49152` - `65535` for non-application specific ports).
   * **Use case:** If port `50451` is free:

   ```ini
   Port=50451
   ```

---

### `TestEvent`

   * **What it does:** Name of a valid haptic event that fires when you hit the **Test** button in the UI - great for troubleshooting your pipeline.
   * **Valid values:** Any event identifier you've set in bHaptics Developer Portal.
   * **Use case:** If you have a heartbeat event:

   ```ini
   TestEvent=heartbeat
   ```

   When the 'Test' button is clicked, a `heartbeat` effect will be sent.

---

## `[bHaptics]` section

### `ApiKey`

   * **What it does:** This is your 20-character API key from the bHaptics Developer Portal used to authenticate calls to the online API.
   * **Valid values:** 20-character string (e.g., `abcd1234efgh5678ijkl`).
   * **Use case:** Copy from your portal section **Settings -> Application Information -> Latest API Key** and paste here:

   ```ini
   ApiKey=abcd1234efgh5678ijkl
   ```

### `AppId`

   * **What it does:** This is your 24-character deployment key or workspace ID from bHaptics Developer Portal.
   * **Valid values:** 24-character string (e.g., `1234567890abcdef12345678`).
   * **Use case:** Copy from your portal section **Settings -> Application Information -> Application ID** and paste here:

   ```ini
   AppId=1234567890abcdef12345678
   ```

### `DefaultConfig`

   * **What it does:** Path to the offline configuration JSON to be used as a fallback if the bHaptics Player can't reach the API.
   * **Valid values:** A valid `DefaultConfig.json` file you've downloaded from the Developer Portal (click _Download Default Config_ link after Deployment)
   * **Use case:** If you saved the JSON to your mod folder, specify the filename here (you can name it anything you want):

   ```ini
   DefaultConfig=DefaultConfig.json
   ```

---

## Required Keys

> [!WARNING]  
> The following settings *must* be present and non-empty, or bHapticsRelay will refuse to start:

```
Settings:Title
Settings:Version
Settings:Mode
bHaptics:ApiKey
bHaptics:AppId
```


## Deployment

This section covers packaging and shipping your finished mod.

After you've got your mod working and have your `config.cfg` dialed in, you can simply pack everything into a single .zip file and distribute that.

bHapticsRelay is standalone .NET 8 WPF app and does not require an installer. Players may extract the .zip to a folder anywhere on their PC and run from there.

### 1. Verify Package Contents

You should have these files in your .zip package:

```
bhaptics_library.dll
bHapticsRelay.exe
config.cfg
DefaultConfig.json
LICENSE
Readme.txt (see note below)
```

You can rename `bHapticsRelay.exe` if you want, to something like `bHaptics for AppName.exe` or whatever you prefer.

> [!IMPORTANT]  
> **Please do not omit the `Readme.txt` file** - it contains attributions for bHaptics, bHapticsRelay and other important information for the end-user.

You may modify the `Readme.txt` file (in fact, this would be recommended), but please use your best judgement and keep the attributions and important info intact. Thank you. :)

### 2. Zip and distribute

- **Create a single .zip** of the folder containing all files above.
- **Distribute** that archive (e.g., `MyMod-bHaptics.zip`) via your usual channels (Nexus, Moddb, GitHub Releases, etc.).

### 3. End-user Installation

End users will install and run the mod as follows. More detailed instructions are included in `Readme.txt`.

1. **Unzip** the archive to any folder on their PC (no admin rights needed).
2. **Double-click** `bHapticsRelay.exe` to launch the relay. They'll see your game title, version, and Test Event button.

> [!NOTE]  
> All paths in `config.cfg` are relative to the EXE's folder, so players can drop the whole folder anywhere such as on the desktop, in `Program Files`, or even in the game folder itself.

# Compilation from Source

If you'd like to build bHapticsRelay yourself (to customize, debug, or contribute), follow these steps:

### Prerequisites

* **.NET 8 SDK** installed ([https://dotnet.microsoft.com](https://dotnet.microsoft.com))
* **Visual Studio 2022+** with **.NET desktop development** workloads
* (Optional) **Git** to clone the repo

### bhaptics_library.dll

For licensing reasons, the `bhaptics_library.dll` file is not included in this repo's source. You must download it separately from the `tact-csharp2` bHaptics repo:

Link: [bhaptics_library.dll](https://github.com/bhaptics/tact-csharp2/raw/refs/heads/master/tact-csharp2/tact-csharp2/bhaptics_library.dll)

### Required NuGet Packages & Versions

- Costura.Fody 6.0.0
- Fleck 1.2.0
- Fody 6.9.3
- Microsoft.Extensions.Configuration.Ini 9.0.6
- Microsoft.NET.ILLink.Tasks 8.0.16
- Serilog 4.3.0
- Serilog.Sinks.File 7.0.0

### 1. Clone the repository

```bash
git clone https://github.com/Dteyn/bHapticsRelay.git
cd bHapticsRelay
```

### 2. Open the solution

Double-click **`bHapticsRelay.sln`** in the repo root, or run:

```bash
dotnet sln open bHapticsRelay.sln
```

### 3. Configure build settings

1. In Visual Studio, set the **Solution Configuration** to `Debug` (recommended for building and testing).
2. Ensure the **Platform** is `Any CPU`.

### 4. Restore and build

From the IDE:

* **Build** > **Restore NuGet Packages**
* **Build** > **Build Solution**

Or via CLI:

```bash
dotnet restore
dotnet build -c Debug
```

### 5. Locate your artifacts

After a successful build, you'll find the output in:

Debug build:
```
bin/Debug/net8.0-windows/win-x64/
```

Release build:
```
bin/Release/net8.0-windows/win-x64/
```

These folders will contain all the depencency DLLs. To create a self-contained .exe, right click the project and click Publish. Set a publish location, and then click Publish.

The resulting Publish folder should contain a self-contained .exe for distribution. The .pdb file does not need to be included.

### 6. Test your build

1. Edit or confirm `config.cfg` in the output folder.
2. Run `bHapticsRelay.exe`.
3. Verify your game title, version, and test event all appear correctly.

### Debug Logging

In the Debug release, bHapticsRelay will generate additional debug logging in the `/Logs/bhaptics-relay-(date).txt` log file.

Once you have everything dialed in, switch to the Release version and you should be good to go. Feel free to open an Issue or reach out if you have any questions.

# Game Integration Guide

This section is for mod developers who want to emit events from their game scripts to trigger haptics via bHapticsRelay.

Depending on your game and modding environment, you can choose to use log file output or direct WebSocket messages. 

This section covers how to implement both, with an example using OpenMW (Morrowind) Lua modding.

**Basic Requirements**

For this setup to work, you must have some way of controlling what the game writes to a log file, and when. 

The game engine must support some kind of scripting or mod support to achieve this, or you may also be able to add custom logging if the game's source code is available.

## Log File Tailing

In log tailing mode, your game or mod needs to output lines to a text file, using the `[bHaptics]command` syntax. When bHapticsRelay is tailing that file, any time it sees that pattern, it will trigger the named haptic event.

**General Approach**

Identify a suitable log file. Many games output to a log file by default (for example, Unity games usually have a Player.log, older games have their own logs, etc.).

If your target game already has a log, find its location. If it's constantly appended to, bHapticsRelay can tail it.

If the game's log is hard to locate or rotates often, an alternative is for your mod to create its own log file just for haptics.

In your mod code, print or write lines that include `[bHaptics]command` at the appropriate times. Ideally, each haptic trigger is on a separate line, but it can be amidst other text.

**OpenMW Example**

In my bHaptics mod for OpenMW (The Elder Scrolls III: Morrowind engine), I use the built-in Lua scripting scripting system that allows mods to run code on game events.

By default, using OpenMW Lua's print() function will write to OpenMW's log file (openmw.log). We can leverage this to send haptic commands.

Here is part of my Lua mod script for OpenMW:

```lua
return {
    engineHandlers = {
        -- Haptics for consuming items
        onConsume = function(item)
            -- Get the item type
            itemType = tostring(item.type)

            if itemType == ("Potion") then
                print("[bHaptics]play,drinkeffect")
            end

            if itemType == ("Ingredient") then
                print("[bHaptics]play,eateffect")
            end
        end,
    }
```

In the above snippet, whenever the player consumes an item, the script checks if they are consuming a Potion or Ingredient.

When a Potion is used by the player, this code is triggered whcih in turn writes `[bHaptics]play,drinkeffect` to the log file.

bHapticsRelay sees this and processes the command as `play,drinkeffect` which is then sent to the bHaptics Player to trigger haptic feedback.

As a result, the player feels a 'potion drinking' effect anytime they drink a potion. Neat, eh!? :)

### Tips for log integration:

**Ensure the tag is exact**

The relay looks for the exact case-sensitive string `[bHaptics]` in the log line. Make sure your output matches this format exactly, including the square brackets and capitalization. The event name inside is also case-sensitive and should exactly match what you have in your haptic config (IDs typically are lowercase).

**Write one event per line**

It's safest to output the tag in its own line or at least ensure it's not appearing multiple times in one line. The relay will typically trigger on the first occurrence per line.

If you want to trigger multiple events at once, you can output multiple lines (the relay can handle them in quick succession).

**Log timing**

The relay reads the log file continuously. There may be a tiny delay (typically only milliseconds) between your mod writing the line and the relay reacting, depending on file I/O buffering. For most haptic feedback, this latency is negligible.

**Custom log files**

If your mod can write to a file directly (e.g., using file I/O in Lua or a plugin in another language), you might consider making a dedicated `haptics.log` file. That way, you can point bHapticsRelay to this file and you won't have other unrelated log spam.

## WebSocket Mode

In WebSocket mode, your mod needs to act as a client that sends messages to a local WebSocket server hosted by bHapticsRelay. This allows more structured and potentially faster communication (no disk I/O). The trade-off is that your modding environment must support networking or an external script.

**General Approach**

From your mod or script, establish a WebSocket connection to `ws://localhost:<port>` (make sure the port matches the one you specified in `config.cfg`). The relay will accept the connection from any local client.

> [!NOTE]  
> For security reasons, **only clients on the same PC or LAN** can connect to bHapticsRelay. The websocket server is not accessible over the internet.

**Send Commands**

Once connected, your mod can send commands as simple text messages. For example, sending `play,heartbeat` will issue a heartbeat effect to bHaptics Player.

The server will respond with return values if the command has any.

**External Python Client**

If your modding environment doesn't support WebSockets directly, another approach is to create a small external Python script or program that interfaces with the game (via memory reading or other means) and then sends WebSocket messages.

For example, a Python script could listen for game events (through an API or memory watch) and then use a WebSocket client library to forward those events to bHapticsRelay. This essentially offloads the event detection to an external process which might be easier in some cases.

**Python Testing Script for WebSocket Mode**

Here is a small script I've put together for testing the WebSocket mode. This will allow you to send messages to bHapticsRelay via WebSocket and will display the returned results.

```python
from websocket import create_connection

# Set the URL and port to connect to
url = "ws://localhost:50451"

try:
    ws = create_connection(url)
    print("Connected. Type messages to send. Type 'exit' or 'quit' to close.")
    print("Example: play,heartbeat")

    while True:
        msg = input("> ")
        if msg.lower() in ('exit', 'quit'):
            break
        ws.send(msg)
        print("Sent:", msg)
        try:
            response = ws.recv()
            print("Received:", response)
        except Exception as recv_err:
            print("Error receiving response:", recv_err)
            break

    ws.close()
    print("Connection closed.")
except Exception as e:
    print("Error:", e)
```

Feel free to use as needed for debugging, or adapt to your use case.

### Tips for WebSocket integration:

Here are a few things to keep in mind when using WebSocket to ensure a reliable experience.

**Choose a stable port**
Ports below 49151 may be used by other services, so ideally you should use a port in the range of 49152 - 65535.

Best practice would be to make your mod easily configurable on the client side, so you can easily change the port if needed.

**Connection handling**

Your mod should ideally handle the case where bHapticsRelay isn't running or the socket can't connect. Usually, if the relay isn't up, the connection will refuse, so some error handling here would be useful on your mod's client side.

**One message at a time**

Always send a single command per message. Do not send multiple commands per message; bHapticsRelay processes messages individually.

**Performance notes**

WebSocket mode is very low-latency and is suitable for rapid or frequent events. However, be mindful not to spam too many haptic events too quickly - overlapping too many patterns can overwhelm the end user with too much feedback and then it just feels like constant vibrations. Consider using intensity or logic in your mod to throttle or combine events if needed.

**Use cases for Websocket**

This mode might be the only option for games that do not have easily accessible logs, or if you want your mod to interact with the bHaptics Player since log mode doesn't offer any feedback.

## Ensuring Correct Event Mapping (capitalization, etc)

Regardless of which mode you choose to use, one crucial aspect is making sure the Event IDs you trigger from the game match those in your bHaptics configuration in Developer Portal.

After you set up events in the Developer Portal or Designer, keep a reference of event names either in your mod comments or config so it's easy to update if you change them on the portal side.

If there's any mismatch (typo, different case, etc) you will see an error in bHaptics Player 'eventName not registered in this Workspace'. Double-check spelling and make sure you're using the same event names from the Developer Portal after selecting Deploy.

If you change any of the event names in the portal, make sure to keep everything in sync in your mod code and the portal side.
  
# Example Implentations

## Test Suite

I've included a Test Suite which can be used to test bHapticsRelay, along with an example implementations with some effects to try. The Test Suite also includes a way to test all commands in SDK2 to become familiar with them.

### [bHapticsRelay_Test_Suite.pyw](https://raw.githubusercontent.com/Dteyn/bHapticsRelay/refs/heads/master/bHapticsRelay_Test_Suite.pyw)
(right click, Save As)

**Requirements:** [Python 3.x](https://python.org)

To use it, make sure bHapticsRelay is started and in Websocket mode. Then, start the Test Suite and make sure the Websocket address/port are correct, and click Connect.

You can then try various commands to see how they work and verify that bHapticsRelay is working correctly.

You can also use this with your own project's API key to test everything; simply edit the `config.cfg` and/or `DefaultConfig.json` for bHapticsRelay to use your project, and then start the test suite. It will list out the effects from your project and let you test them accordingly.

The test suite is just a simple Python script for now and I don't intend to improve it further, but if anyone wants to submit improvements for that I'll happily merge them.


## OpenMW (Morrowind) bHaptics

My first implementation of bHapticsRelay will be [OpenMW-bHaptics](https://github.com/Dteyn/OpenMW-bHaptics) which uses the log tailing mode to add bHaptics effects to the game.

It's a work-in-progress and as of writing this, the repo is currently empty. Once published, I'll come back here to update this with some more details on how it works.


## Other Examples

bHapticsRelay is flexible and can be used in many ways. Below are a few examples of how bHapticsRelay can be used in conjunction with Python.

> [!NOTE]
> bHaptics provides a library for direct suport within Python: [tact-python](https://github.com/bhaptics/tact-python)
> The below examples are provided only to illustrate how bHapticsRelay works; `tact-python` should be considered first if you are developing something in Python to interface with bHaptics Player directly.

## Twitch Chat Bot with haptic effects

Using [twitchio](https://twitchio.dev/), you can create a simple Twitch Bot to listen for chat commands and write haptic instructions to a log file monitored by bHapticsRelay and trigger effects.

The below example listens to Twitch chat for commands (like `!bhaptics explosion`) and writes lines such as `[bHaptics]play,explosion` to a text file. bHapticsRelay can pick this up and trigger an effect.

```python
import twitchio
from twitchio.ext import commands

LOGFILE = r"C:\Path\To\Your\logfile.log"  # <-- Use the same file set in bHapticsRelay config!

class Bot(commands.Bot):

    def __init__(self):
        super().__init__(
            token="oauth:YOUR_TWITCH_OAUTH_TOKEN",
            prefix="!",
            initial_channels=["your_channel"]
        )

    async def event_message(self, message):
        if message.echo:
            return

        # Only trigger on commands like "!bhaptics something"
        if message.content.lower().startswith("!bhaptics "):
            effect = message.content.split(" ", 1)[1].strip()

            # Ensure the effect name is lowercase
            effect = effect.lower()

            # Write to log file in the format bHapticsRelay expects
            with open(LOGFILE, "a", encoding="utf-8") as log:
                log.write(f"[bHaptics]play,{effect}\n")
            print(f"Triggered haptic: {effect}")

if __name__ == "__main__":
    bot = Bot()
    bot.run()
```

Again, consider [tact-python](https://github.com/bhaptics/tact-python) first if you're developing something like this, but this is a good example of how any text file can be used to trigger haptics with bHapticsRelay - not just game log files.

## Monitoring Game Memory (Advanced)

This is out of the scope of this guide, but if you are able to monitor RAM for specific values and then based on those, write events to a log file (or send over Websocket), that's another way you could trigger haptics based on what's happening in the game.

Python library [`pymem`](https://pymem.readthedocs.io/en/latest/) may be worth looking at for this type of solution.


## Using Named Pipes

If your game or mod can write to a named pipe, a Python script can read from it and forward messages to bHapticsRelay, using either Websocket or by writing to a text file.



# Future Ideas

## Community Contributions

As an open-source, hobbyist-focused project, bHapticsRelay welcomes contributions from the community.

Whether it's code improvements, new features, or simply testing and feedback, all help is appreciated. If you have an idea or find a bug, feel free to open an issue or pull request on the GitHub repository. Modders often have creative solutions and specific needs, so this project can evolve with community input.

## Support for Additional Input Methods

Right now, only log or WebSocket are supported input methods. The next most obvious input method to tackle would be named pipes, but I don't have much experience with that yet.

## SDK1 Support

Currently, only SDK2 is integrated and supported. Adding support for SDK1 via `.tact` files would be a great way to extend the program.

Since SDK1 is deprecated, I don't currently have plans to add SDK1 support, but if someone else wants to add it for backwards compatibility, I'm certainly open to pull requests on that.

## Better UI/UX

The UI is pretty minimal at the moment. bHaptics SDK2 allows for more detail from the player, so in theory could add a section for devices connected or even show haptic feedback patterns in the app.

# Contributions

If you are a developer interested in any of the above features or have your own, don't hesitate to fork the project and experiment. I just ask that you share your improvements back with the community if possible. Given this is for fun and the betterment of immersive gaming, collaboration is key!

# Reporting Issues

For any bugs or unexpected behavior, please open an issue on the repository. Include details like the log output of bHapticsRelay, your config, and what you were doing.

# Acknowledgements and Shout-Outs

This project stands on the shoulders of the bHaptics SDK and the many modders in the bHaptics modding community who pioneered haptic feedback mods in games.

## [bHaptics Inc.](https://www.bhaptics.com)

Firstly, I want to thank bHaptics for their awesome gear, great customer support, and for providing the SDK and encouraging community integration.

bHaptics should absolutely be commended for supporting community-driven projects and for providing a great SDK for interacting with their gear. This level of community support is rare these days and is hugely appreciated.

bHaptics has a Discord community as well, be sure to check it out: [bHaptics Discord](https://discord.com/invite/bhaptics-724544309214445599)

## [Astienth's VR Mods & Projects](https://github.com/Astienth/VR-Mods-Projects)

I also specifically want to thank my good friend **Astienth**, whose friendship and guidance has been instrumental in making this project a reality.

Astienth makes many excellent bHaptics and flat2VR mods for many games, you can find his GitHub page here with a listing of all his current projects:

https://github.com/Astienth/VR-Mods-Projects

## [Florian's bHaptics projects](https://github.com/floh-bhaptics)

A huge thanks to bHaptics community member **Florian Fahrenberger** who has made many bHaptics mods.

Florian also provides a great guide and source code example for adding bHaptics mods to Unity games on his [Shadow Legend VR](https://github.com/floh-bhaptics/ShadowLegend_bhaptics) repository. Check it out!

https://github.com/floh-bhaptics

## [farmertrueVR](https://www.farmertrueVR.com)

My good friend **farmertrue** is a variety VR streamer who streams a wide range of VR content at the highest level possible.

He has a true passion for VR, and gives his honest opinion unlike so many other creators these days. He often streams the latest VR games, and if there is a bHaptics mod available, you can be certain he'll be using it.

Farmertrue also has a great 18+ Discord community, where we chat about VR on a daily basis, help users with PCVR builds or troubleshooting, and generally just chill out and have a great time hanging in VR. 

Be sure to check him out if you're looking for the best in VR entertainment and information!

- [www.farmertruevr.com](https://www.farmertrueVR.com)
- [twitch.tv/farmertrue](https://twitch.tv/farmertrue)
- [youtube.com/@farmertrueVR](https://youtube.com/@farmertrueVR)
- [Join the farmertrueVR Discord](https://discord.gg/SpKY7ySjXX)

# Bhaptics SDK2 API Reference

bHapticsRelay **fully supports** all functions provided by `bhaptics_library.dll`. Each available function from triggering haptic effects to querying device status is wrapped and accessible directly through bHapticsRelay.

I've also updated `BhapticsSDK2Wrapper.cs` (based on [original source](https://github.com/bhaptics/tact-csharp2/blob/master/tact-csharp2/tact-csharp2/BhapticsSDK2Wrapper.cs) from [bHaptics tact-csharp2](https://github.com/bhaptics/tact-csharp2)) to include comments on usage for each of the exports available in `bhaptics_library.dll`.

For reference, I've documented all exported functions below to assist with integrating bHapticsRelay or BhapticsSDK2Wrapper into other projects.

C# examples are provided for every function, and bHapticsRelay command examples are also provided for most.

> [!WARNING]  
> Some details below may be incorrect. If anything is inaccurate, please create an Issue with corrected details and I'll update accordingly.

## SDKv2 Functions

Below is a list of functions available in SDK2 (and therefore, bHapticsRelay). More detailed documentation on each of these follows.

### SDK Connection & Initialization

| Command                                     | Description                                                        |
| ------------------------------------------- | ------------------------------------------------------------------ |
| [registryAndInit](#registryandinit)         | Registers and initializes the SDK client with the default host.    |
| [registryAndInitHost](#registryandinithost) | Registers and initializes the SDK client with a custom server URL. |
| [wsIsConnected](#wsisconnected)             | Checks if the WebSocket connection to bHaptics is live.            |
| [wsClose](#wsclose)                         | Closes the WebSocket connection.                                   |
| [reInitMessage](#reinitmessage)             | Re-initializes the message channel (refreshes auth/workspace).     |

### Playback Controls

| Command                                       | Description                                                                         |
| --------------------------------------------- | ----------------------------------------------------------------------------------- |
| [play](#play)                                 | Plays a pre-defined haptic pattern by event ID.                                     |
| [playParam](#playparam)                       | Plays a haptic pattern with custom parameters (intensity/duration/rotation/offset). |
| [playDot](#playdot)                           | Activates specific motors as a dot pattern.                                         |
| [playWaveform](#playwaveform)                 | Plays a waveform pattern using intensity, timing, and shape arrays.                 |
| [playPath](#playpath)                         | Plays a path-based haptic effect using x/y coordinates and intensities.             |
| [playWithStartTime](#playwithstarttime)       | Plays a haptic pattern starting at a specific time offset, with custom parameters.  |
| [playLoop](#playloop)                         | Plays a looping haptic pattern with interval and loop count.                        |
| [pause](#pause)                               | Pauses a specific haptic playback by event ID.                                      |
| [resume](#resume)                             | Resumes a previously paused haptic playback by event ID.                            |
| [stop](#stop)                                 | Stops playback by request ID.                                                       |
| [stopByEventId](#stopbyeventid)               | Stops playback by event name/key.                                                   |
| [stopAll](#stopall)                           | Stops all active haptic feedback.                                                   |
| [isPlaying](#isplaying)                       | Checks if any haptic pattern is currently playing.                                  |
| [isPlayingByRequestId](#isplayingbyrequestid) | Checks if a specific request ID is playing.                                         |
| [isPlayingByEventId](#isplayingbyeventid)     | Checks if a specific event name/key is playing.                                     |

### Device and Connectivity Utilities

| Command                                     | Description                                             |
| ------------------------------------------- | ------------------------------------------------------- |
| [isbHapticsConnected](#isbhapticsconnected) | Checks if a device is connected at a specific position. |
| [ping](#ping)                               | Pings a specific device.                                |
| [pingAll](#pingall)                         | Pings all connected devices.                            |
| [swapPosition](#swapposition)               | Swaps left/right device positions.                      |
| [setDeviceVsm](#setdevicevsm)               | Sets the VSM (vibration sequence mode) for a device.    |
| [getDeviceInfoJson](#getdeviceinfojson)     | Retrieves device info as a JSON string.                 |

### Player Application Controls

| Command                                 | Description                                     |
| --------------------------------------- | ----------------------------------------------- |
| [isPlayerInstalled](#isplayerinstalled) | Checks if the bHaptics Player app is installed. |
| [isPlayerRunning](#isplayerrunning)     | Checks if the bHaptics Player app is running.   |
| [launchPlayer](#launchplayer)           | Launches the bHaptics Player app.               |

### Advanced Message & Mapping Retrieval

| Command                                         | Description                                       |
| ----------------------------------------------- | ------------------------------------------------- |
| [getEventTime](#geteventtime)                   | Retrieves the timing metadata for a haptic event. |
| [getHapticMappingsJson](#gethapticmappingsjson) | Retrieves all mapping data as a JSON string.      |


## SDK Connection & Initialization

> [!NOTE]
> Only C# Examples are provided for this section.

### `registryAndInit`

```csharp
public static extern bool registryAndInit(string sdkAPIKey, string workspaceId, string initData);
```

**Description:**
Registers the app with a locally running bHaptics Player and initializes it with an initial haptic message.

**Parameters:**

* **sdkAPIKey** (`string`): Your bHaptics SDK API key.
* **workspaceId** (`string`): Workspace/session identifier.
* **initData** (`string`): Initialization parameters (Default Config JSON). Must be an empty string if not specifying any JSON data.

**Returns:** `bool` - `true` if successful

**C# Example:**

```csharp
bool success = BhapticsSDK2Wrapper.registryAndInit("YOUR_API_KEY", "workspace_1", "{}");
```

---

### `registryAndInitHost`

```csharp
public static extern bool registryAndInitHost(string sdkAPIKey, string workspaceId, string initData, string url);
```

**Description:**
Like `registryAndInit`, but lets you specify a custom server URL (for self-hosted or staging environments).

**Parameters:**

* **sdkAPIKey** (`string`): Your bHaptics SDK API key.
* **workspaceId** (`string`): Workspace/session identifier.
* **initData** (`string`): Initialization parameters (Default Config JSON). Must be an empty string if not specifying any JSON data.
* **url** (`string`): Custom WebSocket server address (e.g., `ws://localhost:9000`).

**Returns:** `bool` - `true` if successful.

**C# Example:**

```csharp
bool success = BhapticsSDK2Wrapper.registryAndInitHost("YOUR_API_KEY", "workspace_1", "{}", "ws://localhost:9000");
```

---

### `wsIsConnected`

```csharp
public static extern bool wsIsConnected();
```

**Description:**
Checks if the WebSocket connection to bHaptics is live.

**Parameters:** none

**Returns:** `bool` - `true` if connected.

**C# Example:**

```csharp
if (!BhapticsSDK2Wrapper.wsIsConnected()) {
    // Try reconnecting...
}
```

---

### `wsClose`

```csharp
public static extern void wsClose();
```

**Description:**
Closes the WebSocket connection to bHaptics player.

**Parameters:** none

**Returns:** nothing

**C# Example:**

```csharp
BhapticsSDK2Wrapper.wsClose();
```

---

### `reInitMessage`

```csharp
public static extern bool reInitMessage(string sdkAPIKey, string workspaceId, string initData);
```

**Description:**
Re-initializes the message channel (e.g., refresh authentication/workspace without a full restart).

**Parameters:**

* **sdkAPIKey** (`string`): Your bHaptics SDK API key.
* **workspaceId** (`string`): Workspace/session identifier.
* **initData** (`string`): Initialization parameters (Default Config JSON).

**Returns:** `bool` - `true` if succeeded.

**C# Example:**

```csharp
bool refreshed = BhapticsSDK2Wrapper.reInitMessage("YOUR_API_KEY", "workspace_2", "{}");
```

---

## Playback Controls

## `play`

```csharp
public static extern int play(string eventId);
```

**Description:**
Plays a pre-defined haptic pattern by event ID.

**Parameters:**
* **eventId** (`string`): Identifier for the haptic event.

**Returns:** `int` - Request ID (>0 if started; <=0 on error).

**C# Example:**

```csharp
int reqId = BhapticsSDK2Wrapper.play("heartbeat");
```

**bHapticsRelay Example:**
```csharp
play,heartbeat
```

---

## `playParam`

```csharp
public static extern int playParam(string eventId, int requestId, float intensity, float duration, float angleX, float offsetY);
```

**Description:**
Plays a haptic pattern with custom parameters (intensity/duration/rotation/offset). Requires a caller-supplied `requestId` for tracking and stop calls.

**Parameters:**

* **eventId** (`string`): Identifier for the haptic pattern.
* **requestId** (`int`): Caller-assigned request ID for tracking (0 to auto-generate)
* **intensity** (`float`): Strength multiplier (Default = 1.0)
* **duration** (`float`): Duration multiplier (Default = 1.0)
* **angleX** (`float`): X-axis offset (Default = 0.0)
* **offsetY** (`float`): Y-axis offset (Default = 0.0)

**Returns:** `int` - Request ID (>0 if started; <=0 on error).

**C# Example:**

```csharp
int reqId = 101;
int result = BhapticsSDK2Wrapper.playParam("impact", reqId, 0.8f, 1.0f, 45.0f, 0.0f);
```

**bHapticsRelay Example:**

```csharp
playParam,impact,101,0.8,1.0,45,0.0
```

---

## `playWithStartTime`

```csharp
public static extern void playWithStartTime(string eventId, int requestId, int startMillis, float intensity, float duration, float angleX, float offsetY);
```

**Description:**
Plays a haptic pattern starting at a specific time offset with custom parameters. Requires a caller-supplied `requestId` for tracking.

**Parameters:**

* **eventId** (`string`): Identifier for the haptic pattern.
* **requestId** (`int`): Caller-assigned request ID for tracking (0 to auto-generate)
* **startMillis** (`int`): Start offset in milliseconds from the beginning of the pattern
* **intensity** (`float`): Strength multiplier (Default = 1.0)
* **duration** (`float`): Duration multiplier (Default = 1.0)
* **angleX** (`float`): X-axis offset (Default = 0.0)
* **offsetY** (`float`): Y-axis offset (Default = 0.0)

**Returns:** `void` - No return value; playback is fire-and-forget.

**C# Example:**

```csharp
int reqId = 201;
BhapticsSDK2Wrapper.playWithStartTime("explosion", reqId, 500, 0.9f, 1.2f, 30.0f, 0.0f);
```

**bHapticsRelay Example:**

```csharp
playWithStartTime,explosion,201,500,0.9,1.2,30,0
```

---

## `playLoop`

```csharp
public static extern int playLoop(string eventId, int requestId, float intensity, float duration, float angleX, float offsetY, int interval, int maxCount);
```

**Description:**
Plays a looping haptic pattern. Loop timing and maximum count can be specified.

**Parameters:**

* **eventId** (`string`): Identifier for the haptic pattern.
* **requestId** (`int`): Caller-assigned request ID for tracking (0 to auto-generate)
* **intensity** (`float`): Strength multiplier (Default = 1.0)
* **duration** (`float`): Duration multiplier (Default = 1.0)
* **angleX** (`float`): X-axis offset (Default = 0.0)
* **offsetY** (`float`): Y-axis offset (Default = 0.0)
* **interval** (`int`): Milliseconds between loop iterations
* **maxCount** (`int`): Maximum number of loops

**Returns:** `int` - Request ID (>0 if started; <=0 on error).

**C# Example:**

```csharp
int reqId = 301;
int result = BhapticsSDK2Wrapper.playLoop("pulse", reqId, 1.0f, 0.5f, 0.0f, 0.0f, 1000, 5);
```

**bHapticsRelay Example:**

```csharp
playLoop,pulse,301,1.0,0.5,0,0,1000,5
```

---

## `playDot`

```csharp
public static extern int playDot(int requestId, int position, int durationMillis, int[] motors, int size);
```

**Description:**
Activates specific motors as a dot pattern. Requires a caller-supplied `requestId` for tracking and stop calls.

**Parameters:**

* **requestId** (`int`): Caller-assigned request ID for tracking.
* **position** (`int`): Device position index
* **durationMillis** (`int`): Duration per dot (ms)
* **motors** (`int[]`): Indices of motors to activate
* **size** (`int`): Length of `motors`

**Returns:** `int` - Request ID (>0 if started; <=0 on error).

**C# Example:**

```csharp
int reqId = 102;
int[] motors = { 1, 4, 7 };
int result = BhapticsSDK2Wrapper.playDot(reqId, 0, 200, motors, motors.Length);
```

**bHapticsRelay Example:**

```csharp
playDot,102,0,200,1|4|7,3
```

*(motors are pipe-separated; last value is count)*

---

## `playWaveform`

```csharp
public static extern int playWaveform(int requestId, int position, int[] motorValues, int[] playTimeValues, int[] shapeValues, int motorLen);
```

**Description:**
Plays a waveform pattern by specifying motor intensities, play times, and shape values. Requires a caller-supplied `requestId` for tracking and stop calls.

**Parameters:**

* **requestId** (`int`): Caller-assigned request ID for tracking.
* **position** (`int`): Device position index
* **motorValues** (`int[]`): Intensities per motor
* **playTimeValues** (`int[]`): Play duration per motor
* **shapeValues** (`int[]`): Shape parameters
* **motorLen** (`int`): Length of motor arrays

**Returns:** `int` - Request ID (>0 if started; <=0 on error).

**C# Example:**

```csharp
int reqId = 103;
int[] intensities = { 100, 80, 0, 0, 50 };
int[] durations = { 100, 100, 100, 100, 100 };
int[] shapes = { 1, 1, 1, 1, 1 };
int result = BhapticsSDK2Wrapper.playWaveform(reqId, 0, intensities, durations, shapes, intensities.Length);
```

**bHapticsRelay Example:**

```csharp
playWaveform,103,0,100|80|0|0|50,100|100|100|100|100,1|1|1|1|1,5
```

*(arrays are pipe-separated; last value is array length)*

---

## `playPath`

```csharp
public static extern int playPath(int requestId, int position, float[] xValues, float[] yValues, int[] intensityValues, int Len);
```

**Description:**
Plays a path-based haptic effect by specifying x/y coordinates and intensities. Requires a caller-supplied `requestId` for tracking and stop calls.

**Parameters:**

* **requestId** (`int`): Caller-assigned request ID for tracking.
* **position** (`int`): Device position index
* **xValues** (`float[]`): Array of X-axis coordinates for each point
* **yValues** (`float[]`): Array of Y-axis coordinates for each point
* **intensityValues** (`int[]`): Array of intensity values for each point
* **Len** (`int`): Length of coordinate and intensity arrays

**Returns:** `int` - Request ID (>0 if started; <=0 on error).

**C# Example:**

```csharp
int reqId = 104;
float[] x = { 0.1f, 0.2f, 0.3f };
float[] y = { 0.5f, 0.5f, 0.5f };
int[] intensity = { 80, 60, 100 };
int result = BhapticsSDK2Wrapper.playPath(reqId, 0, x, y, intensity, x.Length);
```

**bHapticsRelay Example:**

```csharp
playPath,104,0,0.1|0.2|0.3,0.5|0.5|0.5,80|60|100,3
```

*(arrays are pipe-separated; last value is array length)*

---

## `pause`

```csharp
public static extern bool pause(string eventId);
```

**Description:**
Pauses a specific haptic playback by event ID.

**Parameters:**

* **eventId** (`string`): Name of the event identifier to pause.

**Returns:** `bool` - `true` if paused successfully

**C# Example:**

```csharp
BhapticsSDK2Wrapper.pause("fly_effect");
```

**bHapticsRelay Example:**

```csharp
pause,fly_effect
```

---

## `resume`

```csharp
public static extern bool resume(string eventId);
```

**Description:**
Resumes a previously paused haptic playback by request ID.

**Parameters:**

* **eventId** (`string`): Name of the event identifier to pause.

**Returns:** `bool` - `true` if resumed successfully

**C# Example:**

```csharp
BhapticsSDK2Wrapper.resume("fly_effect");
```

**bHapticsRelay Example:**

```csharp
resume,fly_effect
```

---

## `stop`

```csharp
public static extern bool stop(int reqId);
```

**Description:**
Stops playback by request ID.

**Parameters:**
* **reqId** (`int`): Request ID returned from play/playPosParam.

**Returns:** `bool` - `true` if stopped

**C# Example:**

```csharp
BhapticsSDK2Wrapper.stop(12345);
```

**bHapticsRelay Example:**
```csharp
stop,12345
```

---

## `stopByEventId`

```csharp
public static extern bool stopByEventId(string eventId);
```

**Description:**
Stops playback by event name/key.

**Parameters:**
* **eventId** (`string`): Name of the haptic event to stop.

**Returns:** `bool` - `true` if stopped

**C# Example:**

```csharp
BhapticsSDK2Wrapper.stopByEventId("pulse");
```

**bHapticsRelay Example:**
```csharp
stopByEventId,pulse
```

---

## `stopAll`

```csharp
public static extern bool stopAll();
```

**Description:**
Stops all playback.

**Parameters:** none

**Returns:** `bool` - `true` if stopped

**C# Example:**

```csharp
BhapticsSDK2Wrapper.stopAll();
```

**bHapticsRelay Example:**
```csharp
stopAll
```

---

## `isPlaying`

```csharp
public static extern bool isPlaying();
```

**Description:**
Checks if any pattern is currently playing.

**Parameters:** none

**Returns:** `bool` - `true` if any pattern is playing

**C# Example:**

```csharp
if (BhapticsSDK2Wrapper.isPlaying()) { /* ... */ }
```

**bHapticsRelay Example:**
```csharp
isPlaying
```

---

## `isPlayingByRequestId`

```csharp
public static extern bool isPlayingByRequestId(int requestId);
```

**Description:**
Checks if a specific request ID is playing.

**Parameters:**
* **requestId** (`int`): Request ID to check.

- **Returns:** `bool` - `true` if still playing

**C# Example:**

```csharp
if (BhapticsSDK2Wrapper.isPlayingByRequestId(reqId)) { /* ... */ }
```

**bHapticsRelay Example:**
```csharp
isPlayingByRequestId,12345
```

---

## `isPlayingByEventId`

```csharp
public static extern bool isPlayingByEventId(string eventId);
```

**Description:**
Checks if a specific event name/key is playing.

**Parameters:**
* **eventId** (`string`): Name of the haptic event to check.

- **Returns:** `bool` - `true` if still playing

**C# Example:**

```csharp
if (BhapticsSDK2Wrapper.isPlayingByEventId("slash")) { /* ... */ }
```

**bHapticsRelay Example:**
```csharp
isPlayingByEventId,slash
```

---

## Device and Connectivity Utilities

## `isbHapticsConnected`

```csharp
public static extern bool isbHapticsConnected(int position);
```

**Description:**
Checks if a device is connected at a specific position index.

**Parameters:**
* **position** (`int`): Device position index  

**Returns:** `bool` - `true` if any device is connected

**C# Example:**

```csharp
if (!BhapticsSDK2Wrapper.isbHapticsConnected(0)) {
    // Device not found
}
```

**bHapticsRelay Example:**
```csharp
isbHapticsConnected,0
```

---

## `ping`

```csharp
public static extern bool ping(string address);
```

**Description:**
Pings a specific device.

**Parameters:**
* **address** (`string`): Device address to ping

**Returns:** `bool` - `true` if device responded

**C# Example:**

```csharp
bool online = BhapticsSDK2Wrapper.ping("0");
```

**bHapticsRelay Example:**
```csharp
ping,0
```

> [!NOTE]
> Documentation here needs improvement - this command needs more testing.

---

## `pingAll`

```csharp
public static extern bool pingAll();
```

**Description:**
Pings all connected devices.

**Parameters:** none

**Returns:** `bool` - `true` if at least one device responded

**C# Example:**

```csharp
bool anyResponded = BhapticsSDK2Wrapper.pingAll();
```

**bHapticsRelay Example:**
```csharp
pingAll
```

---

## `swapPosition`

```csharp
public static extern bool swapPosition(string address);
```

**Description:**
Swaps left/right (primary/secondary) device positions.

**Parameters:**
* **address** (`string`): Device address to swap positions on.

**Returns:** `bool` - `true` if swap succeeded

**C# Example:**

```csharp
BhapticsSDK2Wrapper.swapPosition("0");
```

> [!CAUTION]
> The documentation here needs improvement - this command is not thoroughly tested.

**bHapticsRelay Example:**
```csharp
swapPosition,0
```

---

## `setDeviceVsm`

```csharp
public static extern bool setDeviceVsm(string address, int vsm);
```

**Description:**
Sets the VSM (vibration sequence mode) for a specific bHaptics device.

**Parameters:**

* **address** (`string`): Device address.
* **vsm** (`int`): VSM setting value.

**Returns:** `bool` - `true` if the operation succeeded; otherwise, false.

**C# Example:**

```csharp
bool success = BhapticsSDK2Wrapper.setDeviceVsm("0", 20);
```

**bHapticsRelay Example:**

```csharp
setDeviceVsm,0,20
```
> [!WARNING]
> This command has not been tested or verified - info to be updated.

---

## `getDeviceInfoJson`

```csharp
public static extern IntPtr getDeviceInfoJson();
```

**Description:**
Retrieves device info as a JSON string pointer.

**Parameters:** none

**Returns:** Pointer to a null-terminated C string containing device info in JSON format.

**C# Example:**

```csharp
IntPtr ptr = BhapticsSDK2Wrapper.getDeviceInfoJson();
string json = Marshal.PtrToStringAnsi(ptr);
```

**bHapticsRelay Example:**
```csharp
getDeviceInfoJson
```

---

## Player Application Controls

## `isPlayerInstalled`

```csharp
public static extern bool isPlayerInstalled();
```

**Description:**
Checks if the bHaptics Player app is installed.

**Parameters:** none

**Returns:** `bool` - `true` if player is installed

**C# Example:**

```csharp
if (!BhapticsSDK2Wrapper.isPlayerInstalled()) {
    // Prompt user to install
}
```

**bHapticsRelay Example:**
```csharp
isPlayerInstalled
```

---

## `isPlayerRunning`

```csharp
public static extern bool isPlayerRunning();
```

**Description:**
Checks if the bHaptics Player app is running.

**Parameters:** none

**Returns:** `bool` - `true` if player is running

**C# Example:**

```csharp
if (!BhapticsSDK2Wrapper.isPlayerRunning()) {
    // Optionally launch or warn
}
```

**bHapticsRelay Example:**
```csharp
isPlayerRunning
```

---

## `launchPlayer`

```csharp
public static extern bool launchPlayer(bool tryLaunch);
```

**Description:**
Launches the bHaptics Player app. Must pass 'true' as parameter for launch to proceed.

**Parameters:**
* **tryLaunch** (`bool`): `true` to start

**Returns:** `bool` - `true` if launch was successful

**C# Example:**

```csharp
BhapticsSDK2Wrapper.launchPlayer(true);
```

**bHapticsRelay Example:**
```csharp
launchPlayer,true
```

---

## Advanced Message & Mapping Retrieval

## `getEventTime`

```csharp
public static extern int getEventTime(string eventId);
```

**Description:**
Retrieves the event timing metadata for a given event identifier (e.g., duration in ms).

**Parameters:**
* **eventId** (`string`): Name of the haptic event.

**Returns:** `int` - Integer representing timing data

**C# Example:**

```csharp
int duration = BhapticsSDK2Wrapper.getEventTime("impact");
```

**bHapticsRelay Example:**
```csharp
getEventTime,impact
```

---

## `getHapticMappingsJson`

```csharp
public static extern IntPtr getHapticMappingsJson();
```

**Description:**
Retrieves all mapping data as a JSON string pointer.

**Parameters:** none

**Returns:** `IntPtr` - Pointer to a null-terminated C string containing all mappings in JSON format.

**C# Example:**

```csharp
IntPtr ptr = BhapticsSDK2Wrapper.getHapticMappingsJson();
string json = Marshal.PtrToStringAnsi(ptr);
```

**bHapticsRelay Example:**
```csharp
getHapticMappingsJson
```

# Licensing Notes

## bHaptics SDK Agreement

Use of bHaptics SDK (and therefore, bHapticsRelay) is subject to the bHaptics SDK agreement (https://bhaptics.gitbook.io/license-sdk/).

## No Affiliation with bHaptics

This project is an unofficial community-driven tool. It is not affiliated with or endorsed by bHaptics Inc.

Use of the term "bHaptics" and interoperability with their hardware is solely for compatibility purposes. All trademarks are property of their respective owners. We include their SDK libraries purely to allow this tool to function with their hardware, under their allowed SDK distribution.

## Non-Commercial Use Emphasis

bHapticsRelay is aimed at **non-commercial** hobby projects. If you create a mod with bHapticsRelay that adds haptics to a game, you should distribute it for free.

All bHaptics content (patterns, feedback) must be accessible to end-users who own bHaptics hardware without any additional purchase.

Commercial game developers wanting to add bHaptics support to their games should go through official SDK integration and reach out directly to bHaptics Business Development team (bd@bhaptics.com).

## MIT License, SDK Binaries, and Source Code Usage

bHapticsRelay itself is released under the MIT License, which applies to **all original code in this repository**. 

However, this license **does not apply** to the bHaptics SDK (`bhaptics_library.dll`), or any other SDK binaries or proprietary content owned by bHaptics Inc.

### About BhapticsSDK2Wrapper.cs

This repository includes a modified version of [`BhapticsSDK2Wrapper.cs`](https://github.com/bhaptics/tact-csharp2/blob/master/tact-csharp2/tact-csharp2/BhapticsSDK2Wrapper.cs). The modified version only contains additional comments and a few extra exports, but **does not modify the underlying SDK** or attempt to reverse engineer it. 

The modified wrapper is provided for convenience and is always accompanied by the original copyright notice, as required by bHaptics.

**If you redistribute the modified `BhapticsSDK2Wrapper.cs`, you must retain the bHaptics copyright notice.**

### You May Not:

- Re-license, modify, reverse engineer, or redistribute the SDK itself or its binaries under open source terms.
- Claim bHapticsRelay is an official bHaptics product.

For further questions about SDK usage or redistribution, please refer to the [bHaptics SDK License Agreement](https://bhaptics.gitbook.io/license-sdk/) or contact bHaptics Inc. directly at support@bhaptics.com.

## Summary

You are free to use and modify bHapticsRelay for your personal projects. Just remember that the actual haptic drivers (Player and library) are proprietary, and owned by bHaptics Inc.

Use of the bHapticsRelay tool is at your own risk. It shouldn't ever do damage; at worst it just might not work as expected.

If you redistribute bHapticsRelay, adhere to the policies outlined above and include our license file and the Readme.txt which includes important attributions and credits.

And definitely do not try to sell it or misuse bHaptics's technology in ways that violate their policies.

### About Me

Aside from sharing my coding projects, I also enjoy streaming on Twitch. I usually stream GTFO VR Mod once a week, feel free to pop by the chat some time!

https://twitch.tv/Dteyn

I also have a YouTube channel where I post various videos, lately I have started a new series on After the Fall VR featuring hidden and unused content. Check it out!

https://youtube.com/@Dteyn

Finally, if you found this project useful and feel like sending a few bucks my way, I also have a Ko-Fi page here: https://ko-fi.com/Dteyn

#### Make it a great day! :)
