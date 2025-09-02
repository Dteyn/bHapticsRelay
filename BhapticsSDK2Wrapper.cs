/*
 * BhapticsSDK2Wrapper.cs
 * Copyright bHaptics Inc. All rights reserved
 *
 * Original Source: https://github.com/bhaptics/tact-csharp2/blob/master/tact-csharp2/tact-csharp2/BhapticsSDK2Wrapper.cs
 *
 * This is a thoroughly commented and documented version of the above source. 
 * 
 * Available Exports (SDK 2.0.0):
 *   // Initialization & Connection
 *   bool   registryAndInit(string sdkAPIKey, string workspaceId, string initData)
 *   bool   registryAndInitHost(string sdkAPIKey, string workspaceId, string initData, string url)
 *   bool   wsIsConnected()
 *   void   wsClose()
 *   bool   reInitMessage(string sdkAPIKey, string workspaceId, string initData)
 *
 *   // Playback Controls (event-centric with request IDs)
 *   int    play(string eventId)
 *   int    playParam(string eventId, int requestId, float intensity, float duration, float angleX, float offsetY)
 *   void   playWithStartTime(string eventId, int requestId, int startMillis, float intensity, float duration, float angleX, float offsetY)
 *   int    playLoop(string eventId, int requestId, float intensity, float duration, float angleX, float offsetY, int interval, int maxCount)
 *   int    pause(string eventId)
 *   bool   resume(string eventId)
 *   bool   stop(int requestId)
 *   bool   stopByEventId(string eventId)
 *   bool   stopAll()
 *   bool   isPlaying()
 *   bool   isPlayingByRequestId(int requestId)
 *   bool   isPlayingByEventId(string eventId)
 *
 *   // Low-Level Pattern Playback (requestId-first)
 *   int    playDot(int requestId, int position, int durationMillis, int[] motors, int size)
 *   int    playWaveform(int requestId, int position, int[] motorValues, int[] playTimeValues, int[] shapeValues, int motorLen)
 *   int    playPath(int requestId, int position, float[] xValues, float[] yValues, int[] intensityValues, int Len)
 *
 *   // Device & Connectivity Utilities
 *   bool   isbHapticsConnected(int position)
 *   bool   ping(string address)
 *   bool   pingAll()
 *   bool   swapPosition(string address)
 *   bool   setDeviceVsm(string address, int vsm)
 *   IntPtr getDeviceInfoJson()
 *
 *   // Player Application Controls
 *   bool   isPlayerInstalled()
 *   bool   isPlayerRunning()
 *   bool   launchPlayer(bool tryLaunch)
 *
 *   // Mapping & Timing Retrieval
 *   int    getEventTime(string eventId)
 *   IntPtr getHapticMappingsJson()
 *
 * Notes on removed legacy exports (kept here for reference only):
 *   - playPos, playPosParam -> replaced by playParam(eventId, requestId, ...)
 *   - playGlove -> removed upstream
 *   - playWithoutResult -> removed upstream
 *   - bHapticsGetHapticMessage / bHapticsGetHapticMappings -> replaced by getHapticMappingsJson()
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
        /// Re-initializes the SDK connection without restarting the entire SDK.
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
        /// Plays a pre-defined haptic event by its string identifier.
        /// </summary>
        /// <param name="eventId">Identifier for the haptic pattern (event id/key).</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int play(string eventId);

        /// <summary>
        /// Plays a haptic pattern with custom parameters (intensity/duration/rotation/offset).
        /// </summary>
        /// <param name="eventId">Identifier for the haptic pattern.</param>
        /// <param name="requestId">Caller-assigned request identifier used for tracking/stop calls.</param>
        /// <param name="intensity">Intensity multiplier (0.0 to 1.0).</param>
        /// <param name="duration">Duration multiplier (seconds).</param>
        /// <param name="angleX">Rotation angle around X axis for spatial mapping.</param>
        /// <param name="offsetY">Vertical offset for spatial mapping.</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int playParam(string eventId, int requestId, float intensity, float duration, float angleX, float offsetY);

        /// <summary>
        /// Plays a haptic pattern starting at a specific time offset, with custom parameters.
        /// </summary>
        /// <param name="eventId">Identifier for the haptic pattern.</param>
        /// <param name="requestId">Caller-assigned request identifier.</param>
        /// <param name="startMillis">Start offset in milliseconds from the beginning of the pattern.</param>
        /// <param name="intensity">Intensity multiplier (0.0 to 1.0).</param>
        /// <param name="duration">Duration multiplier (seconds).</param>
        /// <param name="angleX">Rotation angle around X axis for spatial mapping.</param>
        /// <param name="offsetY">Vertical offset for spatial mapping.</param>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void playWithStartTime(string eventId, int requestId, int startMillis, float intensity, float duration, float angleX, float offsetY);

        /// <summary>
        /// Plays a looping haptic pattern with specified interval and maximum loop count.
        /// </summary>
        /// <param name="eventId">Identifier for the haptic pattern.</param>
        /// <param name="requestId">Caller-assigned request identifier.</param>
        /// <param name="intensity">Intensity multiplier (0.0 to 1.0).</param>
        /// <param name="duration">Duration multiplier (seconds).</param>
        /// <param name="angleX">Rotation angle around X axis for spatial mapping.</param>
        /// <param name="offsetY">Vertical offset for spatial mapping.</param>
        /// <param name="interval">Milliseconds between loop iterations.</param>
        /// <param name="maxCount">Maximum number of loops (0 for infinite).</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int playLoop(string eventId, int requestId, float intensity, float duration, float angleX, float offsetY, int interval, int maxCount);

        /// <summary>
        /// Pauses a currently playing haptic event by its identifier.
        /// </summary>
        /// <param name="eventId">Identifier for the haptic pattern to pause.</param>
        /// <returns>Status or remaining time depending on native implementation.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pause(string eventId);

        /// <summary>
        /// Resumes a previously paused haptic event by its identifier.
        /// </summary>
        /// <param name="eventId">Identifier for the haptic pattern to resume.</param>
        /// <returns>True if the event was resumed; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool resume(string eventId);

        /// <summary>
        /// Stops a specific haptic playback by request ID.
        /// </summary>
        /// <param name="requestId">Request ID returned from play/playParam/playLoop.</param>
        /// <returns>True if playback stopped; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool stop(int requestId);

        /// <summary>
        /// Stops playback of a haptic event by its event identifier (string id).
        /// </summary>
        /// <param name="eventId">Event id/key to stop.</param>
        /// <returns>True if playback stopped; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool stopByEventId(string eventId);

        /// <summary>
        /// Stops all active haptic feedback.
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
        /// <param name="requestId">Request ID to query.</param>
        /// <returns>True if still playing; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool isPlayingByRequestId(int requestId);

        /// <summary>
        /// Checks if a haptic event identified by string id is playing.
        /// </summary>
        /// <param name="eventId">Event id/key to query.</param>
        /// <returns>True if still playing; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool isPlayingByEventId(string eventId);

        #endregion

        #region Low-Level Pattern Playback

        /// <summary>
        /// Plays a dot pattern: activates specific motors for a given duration.
        /// </summary>
        /// <param name="requestId">Caller-assigned request identifier.</param>
        /// <param name="position">Device position index.</param>
        /// <param name="durationMillis">Duration of each dot in milliseconds.</param>
        /// <param name="motors">Array of motor indices to activate.</param>
        /// <param name="size">Length of the motors array.</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int playDot(int requestId, int position, int durationMillis, int[] motors, int size);

        /// <summary>
        /// Plays a waveform pattern by specifying motor intensities, play times, and shape values.
        /// </summary>
        /// <param name="requestId">Caller-assigned request identifier.</param>
        /// <param name="position">Device position index.</param>
        /// <param name="motorValues">Array of intensity values per motor.</param>
        /// <param name="playTimeValues">Array of play durations per motor.</param>
        /// <param name="shapeValues">Array specifying waveform shape parameters.</param>
        /// <param name="motorLen">Length of the motor arrays.</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int playWaveform(int requestId, int position, int[] motorValues, int[] playTimeValues, int[] shapeValues, int motorLen);

        /// <summary>
        /// Plays a path-based haptic effect by specifying x/y coordinates and intensities.
        /// </summary>
        /// <param name="requestId">Caller-assigned request identifier.</param>
        /// <param name="position">Device position index.</param>
        /// <param name="xValues">Array of X-axis coordinates for each point.</param>
        /// <param name="yValues">Array of Y-axis coordinates for each point.</param>
        /// <param name="intensityValues">Array of intensity values for each point.</param>
        /// <param name="Len">Length of the coordinate and intensity arrays.</param>
        /// <returns>Request ID (>0) if playback started; otherwise, negative or zero.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int playPath(int requestId, int position, float[] xValues, float[] yValues, int[] intensityValues, int Len);

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
        /// <param name="address">Device address to ping.</param>
        /// <returns>True if device responded; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool ping(string address);
        
        /// <summary>
        /// Sends a ping to all known bHaptics devices.
        /// </summary>
        /// <returns>True if at least one device responded; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool pingAll();

        /// <summary>
        /// Swaps the primary and secondary device positions (e.g., left/right swap) for a given address.
        /// </summary>
        /// <param name="address">Device address to apply swap.</param>
        /// <returns>True if swap succeeded; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool swapPosition(string address);

        /// <summary>
        /// Sets the VSM (vibration sequence mode) for a device.
        /// </summary>
        /// <param name="address">Device address.</param>
        /// <param name="vsm">VSM setting value.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool setDeviceVsm(string address, int vsm);

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
        /// <param name="tryLaunch">True to launch the Player.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        [DllImport(ModuleName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool launchPlayer(bool tryLaunch);

        #endregion

        #region Mapping & Timing Retrieval

        /// <summary>
        /// Retrieves the event timing metadata for a given event identifier.
        /// </summary>
        /// <param name="eventId">Event id/key to query timing for.</param>
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
