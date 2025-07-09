/*
 * BhapticsSDK2Wrapper.cs
 * Copyright bHaptics Inc. All rights reserved
 *
 * Original Source: https://github.com/bhaptics/tact-csharp2/blob/master/tact-csharp2/tact-csharp2/BhapticsSDK2Wrapper.cs
 *
 * This is a thoroughly commented and documented version of the above source. In addition to comments,
 * additional support has been added for 5 additional exports not supported in the original source:
 * getEventTime, getHapticMappingsJson, playPos, playGlove, and playWithoutResult have been added.
 * 
 * Summary:
 *   P/Invoke wrapper for the native bhaptics_library.dll.  
 *   Enables C# applications to:
 *     – Initialize and manage the bHaptics SDK connection  
 *     – Play, stop, and query haptic patterns (including positional, dot, waveform, path, loop, glove)  
 *     – Inspect device connectivity and player application state  
 *     – Retrieve event timing and mapping data as JSON  
 *
 * Available Exports:
 *   // Initialization & Connection
 *   bool   registryAndInit(string sdkAPIKey, string workspaceId, string initData)
 *   bool   registryAndInitHost(string sdkAPIKey, string workspaceId, string initData, string url)
 *   bool   wsIsConnected()
 *   void   wsClose()
 *   bool   reInitMessage(string sdkAPIKey, string workspaceId, string initData)
 *
 *   // Playback Controls
 *   int    play(string key)
 *   int    playPos(string key, int position)
 *   int    playPosParam(string key, int position, float intensity, float duration, float angleX, float offsetY)
 *   int    playGlove(string key, int timeout)
 *   void   playWithoutResult(string key)
 *   bool   stop(int key)
 *   bool   stopByEventId(string eventId)
 *   bool   stopAll()
 *   bool   isPlaying()
 *   bool   isPlayingByRequestId(int key)
 *   bool   isPlayingByEventId(string eventId)
 *
 *   // Low-Level Pattern Playback
 *   int    playDot(int position, int durationMillis, int[] motors, int size)
 *   int    playWaveform(int position, int[] motorValues, int[] playTimeValues, int[] shapeValues, int motorLen)
 *   int    playPath(int position, float[] xValues, float[] yValues, int[] intensityValues, int Len)
 *   int    playLoop(string key, float intensity, float duration, float angleX, float offsetY, int interval, int maxCount)
 *
 *   // Device & Connectivity Utilities
 *   bool   isbHapticsConnected(int position)
 *   bool   ping(string address)
 *   bool   pingAll()
 *   bool   swapPosition(string address)
 *   IntPtr getDeviceInfoJson()
 *
 *   // Player Application Controls
 *   bool   isPlayerInstalled()
 *   bool   isPlayerRunning()
 *   bool   launchPlayer(bool launch)
 *
 *   // Advanced Message & Mapping Retrieval
 *   IntPtr bHapticsGetHapticMessage(string apiKey, string appId, int lastVersion, out int status)
 *   IntPtr bHapticsGetHapticMappings(string apiKey, string appId, int lastVersion, out int status)
 *   int    getEventTime(string eventId)
 *   IntPtr getHapticMappingsJson()
 */

using System;
using System.Runtime.InteropServices;

namespace tact_csharp2
{
    /// <summary>
    /// Wrapper class for the bHaptics SDK native library functions (bhaptics_library.dll).
    /// Provides P/Invoke signatures to interact with the bHaptics haptic feedback system.
    /// </summary>
    public class BhapticsSDK2Wrapper
    {
        /// <summary>
        /// Name of the native library module (without file extension) to load via DllImport.
        /// </summary>
        private const string ModuleName = "bhaptics_library";

        #region Initialization and Connection

        /// <summary>
        /// Registers the SDK client and initializes the connection using default host.
        /// </summary>
        /// <param name="sdkAPIKey">Your bHaptics SDK API key.</param>
        /// <param name="workspaceId">Workspace identifier used to segregate sessions.</param>
        /// <param name="initData">Initialization parameters in JSON or serialized format.</param>
        /// <returns>True if registration and initialization succeed; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool registryAndInit(string sdkAPIKey, string workspaceId, string initData);

        /// <summary>
        /// Registers and initializes the SDK client, specifying a custom host URL.
        /// Useful when running against a self-hosted bHaptics server or staging environment.
        /// </summary>
        /// <param name="sdkAPIKey">Your bHaptics SDK API key.</param>
        /// <param name="workspaceId">Workspace identifier used to segregate sessions.</param>
        /// <param name="initData">Initialization parameters in JSON or serialized format.</param>
        /// <param name="url">Custom WebSocket server URL (e.g., ws://localhost:9000).</param>
        /// <returns>True if registration and initialization succeed; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool registryAndInitHost(string sdkAPIKey, string workspaceId, string initData, string url);

        /// <summary>
        /// Checks whether the WebSocket connection to the bHaptics service is currently established.
        /// </summary>
        /// <returns>True if connected; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool wsIsConnected();

        /// <summary>
        /// Closes the WebSocket connection to the bHaptics service.
        /// </summary>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void wsClose();

        /// <summary>
        /// Re-initializes the message channel without restarting the entire SDK.
        /// Can be used to refresh authentication or workspace parameters.
        /// </summary>
        /// <param name="sdkAPIKey">Your bHaptics SDK API key.</param>
        /// <param name="workspaceId">Workspace identifier used to segregate sessions.</param>
        /// <param name="initData">Initialization parameters in JSON or serialized format.</param>
        /// <returns>True if re-initialization succeeded; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool reInitMessage(string sdkAPIKey, string workspaceId, string initData);

        #endregion

        #region Playback Controls

        /// <summary>
        /// Plays a pre-defined haptic event by key/name.
        /// </summary>
        /// <param name="key">Identifier for the haptic pattern (event key).</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int play(string key);

        /// <summary>
        /// Plays a haptic pattern using only position index (legacy overload).
        /// </summary>
        /// <param name="key">Identifier for the haptic pattern (event key).</param>
        /// <param name="position">Device position index.</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int playPos(string key, int position);

        /// <summary>
        /// Plays a haptic pattern at a specified position with custom parameters.
        /// </summary>
        /// <param name="key">Identifier for the haptic pattern (event key).</param>
        /// <param name="position">Device position index (e.g., vest, arm, etc.).</param>
        /// <param name="intensity">Intensity multiplier (0.0 to 1.0).</param>
        /// <param name="duration">Duration multiplier (seconds).</param>
        /// <param name="angleX">Rotation angle around X axis for spatial mapping.</param>
        /// <param name="offsetY">Vertical offset for spatial mapping.</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int playPosParam(string key, int position, float intensity, float duration, float angleX, float offsetY);

        /// <summary>
        /// Plays a haptic pattern on a glove device. Intended for haptic glove products.
        /// </summary>
        /// <param name="key">Identifier for the haptic pattern (event key).</param>
        /// <param name="timeout">Timeout in milliseconds for the glove response.</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int playGlove(string key, int timeout);

        /// <summary>
        /// Plays a dot pattern: activates specific motors for a given duration.
        /// </summary>
        /// <param name="position">Device position index.</param>
        /// <param name="durationMillis">Duration of each dot in milliseconds.</param>
        /// <param name="motors">Array of motor indices to activate.</param>
        /// <param name="size">Length of the motors array.</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int playDot(int position, int durationMillis, int[] motors, int size);

        /// <summary>
        /// Plays a waveform pattern by specifying motor intensities, play times, and shape values.
        /// </summary>
        /// <param name="position">Device position index.</param>
        /// <param name="motorValues">Array of intensity values per motor.</param>
        /// <param name="playTimeValues">Array of play durations per motor.</param>
        /// <param name="shapeValues">Array specifying waveform shape parameters.</param>
        /// <param name="motorLen">Length of the motor arrays.</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int playWaveform(int position, int[] motorValues, int[] playTimeValues, int[] shapeValues, int motorLen);

        /// <summary>
        /// Plays a path-based haptic effect by specifying x/y coordinates and intensities.
        /// </summary>
        /// <param name="position">Device position index.</param>
        /// <param name="xValues">Array of X-axis coordinates for each point.</param>
        /// <param name="yValues">Array of Y-axis coordinates for each point.</param>
        /// <param name="intensityValues">Array of intensity values for each point.</param>
        /// <param name="Len">Length of the coordinate and intensity arrays.</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int playPath(int position, float[] xValues, float[] yValues, int[] intensityValues, int Len);

        /// <summary>
        /// Plays a looping haptic pattern with specified interval and maximum loop count.
        /// </summary>
        /// <param name="key">Identifier for the haptic pattern (event key).</param>
        /// <param name="intensity">Intensity multiplier (0.0 to 1.0).</param>
        /// <param name="duration">Duration multiplier (seconds).</param>
        /// <param name="angleX">Rotation angle around X axis for spatial mapping.</param>
        /// <param name="offsetY">Vertical offset for spatial mapping.</param>
        /// <param name="interval">Milliseconds between loop iterations.</param>
        /// <param name="maxCount">Maximum number of loops (0 for infinite).</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int playLoop(string key, float intensity, float duration, float angleX, float offsetY, int interval, int maxCount);

        /// <summary>
        /// Plays a pattern without returning a result code.
        /// Useful for fire-and-forget patterns where you don't need the request ID.
        /// </summary>
        /// <param name="key">Identifier for the haptic pattern (event key).</param>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void playWithoutResult(string key);

        /// <summary>
        /// Stops a specific haptic playback by request ID.
        /// </summary>
        /// <param name="key">Request ID returned from play/playPosParam.</param>
        /// <returns>True if playback stopped; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool stop(int key);

        /// <summary>
        /// Stops playback of a haptic event by its event identifier (string key).
        /// </summary>
        /// <param name="eventId">Event key/name to stop.</param>
        /// <returns>True if playback stopped; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool stopByEventId(string eventId);

        /// <summary>
        /// Stops all active haptic feedback on the device.
        /// </summary>
        /// <returns>True if all playback stopped; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool stopAll();

        /// <summary>
        /// Checks if any haptic feedback is currently playing.
        /// </summary>
        /// <returns>True if any pattern is playing; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool isPlaying();

        /// <summary>
        /// Checks if a specific request ID is still playing.
        /// </summary>
        /// <param name="key">Request ID to query.</param>
        /// <returns>True if still playing; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool isPlayingByRequestId(int key);

        /// <summary>
        /// Checks if a haptic event identified by string key is playing.
        /// </summary>
        /// <param name="eventId">Event key/name to query.</param>
        /// <returns>True if still playing; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool isPlayingByEventId(string eventId);

        #endregion

        #region Device and Connectivity Utilities

        /// <summary>
        /// Verifies whether a bHaptics device is connected at the given position index.
        /// </summary>
        /// <param name="position">Device position index (e.g., vest, arms, etc.).</param>
        /// <returns>True if device at that position is connected; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool isbHapticsConnected(int position);

        /// <summary>
        /// Sends a ping to a specific bHaptics device.
        /// </summary>
        /// <param name="address">bHaptics device to ping.</param>
        /// <returns>True if device responded; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool ping(string address);
        
        /// <summary>
        /// Broadcasts a ping to all known bHaptics devices.
        /// </summary>
        /// <returns>True if at least one device responded; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool pingAll();

        /// <summary>
        /// Swaps the primary and secondary device positions (e.g., left/right swap) for a given address.
        /// </summary>
        /// <param name="address">Device network address to apply swap.</param>
        /// <returns>True if swap succeeded; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool swapPosition(string address);

        /// <summary>
        /// Retrieves connected device information as a JSON string.
        /// </summary>
        /// <returns>Pointer to a null-terminated C string containing device info in JSON format.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr getDeviceInfoJson();

        #endregion

        #region Player Application Controls

        /// <summary>
        /// Checks if the bHaptics Player application is installed on the system.
        /// </summary>
        /// <returns>True if the Player is installed; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool isPlayerInstalled();

        /// <summary>
        /// Checks if the bHaptics Player application is currently running.
        /// </summary>
        /// <returns>True if the Player is running; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool isPlayerRunning();

        /// <summary>
        /// Launches the bHaptics Player application.
        /// </summary>
        /// <param name="launch">True to launch the Player.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool launchPlayer(bool launch);

        #endregion

        #region Advanced Message & Mapping Retrieval

        /// <summary>
        /// Retrieves the latest haptic messages from the bHaptics server for the given application.
        /// </summary>
        /// <param name="apiKey">User's bHaptics API key.</param>
        /// <param name="appId">Application identifier.</param>
        /// <param name="lastVersion">Version number of the last message received.</param>
        /// <param name="status">Output status code (e.g., HTTP-like status).</param>
        /// <returns>Pointer to JSON string of new haptic messages.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr bHapticsGetHapticMessage(
            string apiKey,
            string appId,
            int lastVersion,
            out int status);

        /// <summary>
        /// Retrieves custom haptic event mappings from the bHaptics server for the given application.
        /// </summary>
        /// <param name="apiKey">User's bHaptics API key.</param>
        /// <param name="appId">Application identifier.</param>
        /// <param name="lastVersion">Version number of the last mapping received.</param>
        /// <param name="status">Output status code.</param>
        /// <returns>Pointer to JSON string of new haptic mappings.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr bHapticsGetHapticMappings(
            string apiKey,
            string appId,
            int lastVersion,
            out int status);

        /// <summary>
        /// Retrieves the event timing metadata for a given event identifier.
        /// </summary>
        /// <param name="eventId">Event key/name to query timing for.</param>
        /// <returns>Integer representing timing data (e.g., duration in ms).</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int getEventTime(string eventId);

        /// <summary>
        /// Retrieves the full JSON payload of haptic mappings from the native library.
        /// </summary>
        /// <returns>Pointer to a null-terminated C string containing all mappings in JSON format.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr getHapticMappingsJson();

        #endregion
    }
}