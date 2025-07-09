using Fleck;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using tact_csharp2;

namespace bHapticsRelay
{

    public partial class MainWindow : Window, IDisposable
    {
        private const string AboutInfo = "bHapticsRelay v0.2.0 by Dteyn";

        // services & config
        IConfigurationRoot? _cfg;
        FileSystemWatcher? _watcher;
        WebSocketServer? _wsServer;
        private System.Timers.Timer? _statusTimer;
        private System.Timers.Timer? _logPollTimer;

        // active WS clients
        private readonly ConcurrentBag<IWebSocketConnection> _clients = new();

        // tweakable constants
        private const int STATUS_TIMER_INTERVAL_MS = 1000;
        private const int LOG_POLL_INTERVAL_MS = 250;

        string? _appTitle, _appVersion;
        string? _mode, _logFile, _testEvent;
        string? _apiKey, _appId;
        int _port;

        private readonly object _tailSync = new();
        long _lastPos = 0;

        public MainWindow()
        {
            InitializeComponent();
            AboutText.Text = AboutInfo;
            this.Loaded += Window_Loaded;

            // Create Logs folder if it doesn't exist
            Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Logs"));
            // Serilog setup
            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Debug()  // DEBUG LOGGING
#else
                .MinimumLevel.Information()  // RELEASE LOGGING
#endif
                .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "Logs", "bhaptics-relay-.txt"), rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Log File to Tail",
                Filter = "Log Files (*.log)|*.log|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                _logPollTimer?.Stop();
                _logPollTimer?.Dispose();
                _logPollTimer = null;
                _logFile = dlg.FileName;
                LogFileTextBox.Text = _logFile;
                // Save to config
                UpdateConfigLogFile(_logFile);

                _watcher?.Dispose(); // Stop watching old file
                _watcher = null;
                lock (_tailSync)
                {
                    _lastPos = new FileInfo(_logFile).Length; // Start tailing from the end
                }

                StartTailing();

                Log.Information("Switched to new log file: {LogFile}", _logFile);
            }
        }

        // Required keys in the config file which are NOT optional and must be present
        private static readonly string[] _requiredKeys = new[]
        {
        "Settings:Title",
        "Settings:Version",
        "Settings:Mode",
        "bHaptics:ApiKey",
        "bHaptics:AppId"
        };

        private bool ValidateConfig(IConfigurationRoot cfg, out string error)
        {
            foreach (var key in _requiredKeys)
            {
                if (string.IsNullOrWhiteSpace(cfg[key]))
                {
                    error = $"Required setting \"{key}\" is missing or blank.";
                    return false;
                }
            }

            // Mode must be either Tail or Websocket
            var mode = cfg["Settings:Mode"]?.Trim();
            if (mode is null || !(mode.Equals("Tail", StringComparison.OrdinalIgnoreCase) ||
                                  mode.Equals("Websocket", StringComparison.OrdinalIgnoreCase)))
            {
                error = "Settings:Mode must be either \"Tail\" or \"Websocket\".";
                return false;
            }

            error = "";
            return true;
        }
        private void UpdateConfigLogFile(string logFile)
        {
            var cfgPath = Path.Combine(AppContext.BaseDirectory, "config.cfg");
            if (!File.Exists(cfgPath))
            {
                Log.Error("Cannot update LogFile – config.cfg not found at {Path}", cfgPath);
                return;
            }

            var lines = File.ReadAllLines(cfgPath).ToList();
            bool replaced = false;

            for (int i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("LogFile", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"LogFile={logFile}";
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                // Append under [Settings] section, or at end if not found
                int idx = lines.FindIndex(l => l.Trim().Equals("[Settings]", StringComparison.OrdinalIgnoreCase));
                if (idx < 0) idx = lines.Count - 1;
                lines.Insert(idx + 1, $"LogFile={logFile}");
            }

            File.WriteAllLines(cfgPath, lines);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure bHaptics Player is installed, if not, bail
            if (!BhapticsSDK2Wrapper.isPlayerInstalled())
            {
                MessageBox.Show(
                    "bHaptics Player is not installed.\n\nPlease install bHaptics Player and try again.",
                    "bHaptics Player Not Installed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                this.Close();
                return;
            }

            // Update the connection status indicator
            UpdateIndicator();
            
            // Load config.cfg file
            try
            {
                _cfg = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddIniFile("config.cfg", optional: false, reloadOnChange: false)
                    .Build();

                // Validate the config options and make sure required options are present
                if (!ValidateConfig(_cfg, out var cfgError))
                {
                    Log.Fatal("Invalid configuration: {Err}", cfgError);
                    MessageBox.Show($"Invalid configuration:\n{cfgError}", "bHapticsRelay",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }
            }
            catch (Exception ex)
            {
                var expected = Path.Combine(AppContext.BaseDirectory, "config.cfg");
                Log.Fatal(ex, "Failed to load config.cfg");
                MessageBox.Show($"Failed to load configuration at:\n{expected}\n\n{ex.Message}",
                                "bHapticsRelay", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            string? Trimmed(string? s) => s?.Trim();

            // SECTION: Settings
            // Get app title and version
            _appTitle = _cfg["Settings:Title"]?.Trim();
            _appVersion = _cfg["Settings:Version"]?.Trim();
            if (string.IsNullOrEmpty(_appTitle)) _appTitle = "The Win";
            if (string.IsNullOrEmpty(_appVersion)) _appVersion = "1.2.3";

            // Update the title bar and label
            this.Title = $"bHaptics for {_appTitle} v{_appVersion}";
            AppTitleText.Text = $"bHaptics for {_appTitle}";

            // Get Mode, LogFile, Port and TestEvent settings
            _logFile = _cfg["Settings:LogFile"];
            // Make sure Port is a number and in valid range
            if (!int.TryParse(_cfg["Settings:Port"], out _port) ||
                _port <= 0 || _port > 65535)
            {
                Log.Warning("Invalid or missing Port in config – using 0 (disabled)");
                _port = 0;
            }
            _testEvent = _cfg["Settings:TestEvent"] ?? "HeartBeat";

            // Set up UI accordingly for either Tail or Websocket mode
            _mode = _cfg["Settings:Mode"];
            if (_mode != null && _mode.Equals("Websocket", StringComparison.OrdinalIgnoreCase))
            {
                TailPanel.Visibility = Visibility.Collapsed;
                WebsocketPanel.Visibility = Visibility.Visible;

                // Set port/address display
                WsAddressText.Text = $"ws://127.0.0.1:{_port}";
            }
            else
            {
                TailPanel.Visibility = Visibility.Visible;
                WebsocketPanel.Visibility = Visibility.Collapsed;
            }

            // SECTION: bHaptics
            // Get Api Key, App ID and default config
            _apiKey = Trimmed(_cfg["bHaptics:ApiKey"]) ?? "";
            _appId = Trimmed(_cfg["bHaptics:AppId"]) ?? "";

            // Read the Default Config into a JSON object
            string? defaultConfigName = _cfg["bHaptics:DefaultConfig"]?.Trim();
            string _offlineConfigJson  = null!;
            if (!string.IsNullOrWhiteSpace(defaultConfigName))
            {
                var cfgPath = Path.Combine(AppContext.BaseDirectory, defaultConfigName);
                bool exists = File.Exists(cfgPath);
                if (exists)
                {
                    _offlineConfigJson  = File.ReadAllText(cfgPath);
                }
                else
                {
                    Log.Warning("Offline fallback file '{Path}' not found – fallback disabled", cfgPath);
                }
            }
            else
            {
                Log.Warning("DefaultConfig not set in config file.");
            }

            // Debug logging of settings loaded
            Log.Debug("Mode={Mode}, LogFile={LogFile}, Port={Port}, TestEvent={TestEvent}, ApiKey={ApiKey}, AppId={AppId}, DefaultConfig={DefaultConfig}",
            _mode, _logFile, _port, _testEvent, _apiKey, _appId, defaultConfigName);

            // Make sure bHaptics API key and App ID are set
            if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_appId))
            {
                Log.Error("bHaptics ApiKey or AppId not set! Check your config file.");
                MessageBox.Show("bHaptics ApiKey or AppId not set!\nCheck your config file.", "Config Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            // Update logfile text box
            LogFileTextBox.Text = _logFile;
            Log.Information("Starting in {Mode}", _mode);

            // 2) Init bHaptics SDK using API Key and App ID, use default config if offline
            // Try the cloud first
            bool ok = BhapticsSDK2Wrapper.registryAndInit(_apiKey!, _appId!, string.Empty);

            if (ok)
            {
                // Ask the server for mapping data 
                int mapStatus;
                IntPtr ptr = BhapticsSDK2Wrapper.bHapticsGetHapticMappings(_apiKey!, _appId!, 0, out mapStatus);

                // string? serverMappings = Marshal.PtrToStringUTF8(ptr);  // replaced with our PtrToUtf8
                string? serverMappings = PtrToUtf8(ptr);

                // If mapStatus returned is 0 AND there is at least one mapping, we are connected via cloud
                if (mapStatus != 0 || string.IsNullOrWhiteSpace(serverMappings) || serverMappings == "[]")
                {
                    Log.Warning("Cloud init failed, no server mappings. Reverting to Default Config JSON.");
                    ok = false;   // we are not okay. force fallback below
                }

            }

            if (!ok)  
            {
                // If we get here, cloud init failed, or we got no mappings from it. Register using offline JSON.
                if (string.IsNullOrEmpty(_offlineConfigJson))
                {
                    MessageBox.Show("No Default Config JSON available, and online auth failed. Cannot proceed.",
                                "bHapticsRelay", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                // Disable validation in the blob - this is required, or else the player will reject us
                _offlineConfigJson = PrepareOfflineJson(_offlineConfigJson);

                // Use reInitMessage to try and register the offline JSON
                ok = BhapticsSDK2Wrapper.reInitMessage(_apiKey!, _appId!, _offlineConfigJson);
            }

            if (!ok)
            {
                // If we get here, it means bHaptics Player didn't allow us to register online or offline. Womp womp.
                MessageBox.Show("bHaptics API initialize failed (cloud and offline). Cannot proceed.",
                                "bHapticsRelay", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            // Wait until bHaptics Player websocket connection is active
            Log.Information("Waiting for bHaptics Player Websocket connection...");
            await Task.Run(() =>
                SpinWait.SpinUntil(() => BhapticsSDK2Wrapper.wsIsConnected()));
            Log.Information("Connected to bHaptics Player.");

            // 3) Start status poller
            _statusTimer = new System.Timers.Timer(STATUS_TIMER_INTERVAL_MS);
            _statusTimer.Elapsed += (_, __) => Dispatcher.Invoke(UpdateIndicator);
            _statusTimer.Start();

            // 4) Start input mode
            if (_mode == null) return;
            if (_mode.Equals("Tail", StringComparison.OrdinalIgnoreCase))
                StartTailing();
            else
                StartWebSocket();
        }

        private void UpdateIndicator()
        {
            try
            {
                bool con = BhapticsSDK2Wrapper.wsIsConnected();
                bool running = BhapticsSDK2Wrapper.isPlayerRunning();

                // Update color of connection indicator
                ConnIndicator.Fill = con
                  ? Brushes.LimeGreen
                  : Brushes.Red;

                // Update connection status text
                ConnStatusText.Text = con ? "(connected)" : "(not connected)";

                // Show Launch Player button when not connected and player isn't running
                LaunchPlayerButton.Visibility = (!con && !running) ? Visibility.Visible : Visibility.Collapsed;

                // Enable/disable Test button based on connection
                if (TestButton != null)
                    TestButton.IsEnabled = con;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "UpdateIndicator failed");
                ConnIndicator.Fill = Brushes.Gray;
                ConnStatusText.Text = "(not connected)";
                LaunchPlayerButton.Visibility = Visibility.Visible;
                if (TestButton != null)
                    TestButton.IsEnabled = false;
            }
        }

        private void LaunchPlayerButton_Click(object sender, RoutedEventArgs e)
        {
            bool result = BhapticsSDK2Wrapper.launchPlayer(true);
            Log.Information("LaunchPlayerButton clicked: launchPlayer(true) => {Res}", result);
            if (result)
            {
                LaunchPlayerButton.Content = "Launching...";
            }
            else
            {
                MessageBox.Show("Failed to launch bHaptics Player.", "bHapticsRelay", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            // Optionally force UI update after clicking
            UpdateIndicator();
        }

        private void StartTailing()
        {
            if (string.IsNullOrWhiteSpace(_logFile))
            {
                Log.Error("LogFile not set");
                MessageBox.Show("No log-file configured for tailing.\nUse the Browse… button or set one in config.cfg.",
                                "bHapticsRelay", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
                // Build an absolute path to the log file
                var filePath = Path.IsPathRooted(_logFile)
                ? _logFile!
                : Path.Combine(AppContext.BaseDirectory, _logFile!);

            // Check if log exists
            if (!File.Exists(filePath))
            {
                Log.Error("Log file not found: {FilePath}", filePath);
                MessageBox.Show($"Log file not found:\n{filePath}", "bHapticsRelay", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
            }

            // Get path for log file
            var dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir))
            {
                // fallback to app folder
                dir = AppContext.BaseDirectory;
            }
            var fileName = Path.GetFileName(filePath);

            Log.Debug("Tailing filePath={FilePath}, dir={Dir}, fileName={FileName}", filePath, dir, fileName);

            // Initialize last-position at end of file
            lock (_tailSync)
            {
                _lastPos = new FileInfo(filePath).Length;
            }

            // Watch for rotation / deletion of log file
            _watcher?.Dispose();
            _watcher = new FileSystemWatcher(dir!, fileName)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime
            };
            _watcher.Renamed += (_, __) => Dispatcher.Invoke(() =>
            {
                Log.Information("Log rotated – restarting tail.");
                lock (_tailSync) _lastPos = 0;
            });
            _watcher.Deleted += (_, __) => Dispatcher.Invoke(() =>
            {
                Log.Warning("Log deleted – stopping tail.");
                _logPollTimer?.Stop();
            });
            _watcher.Created += (_, __) => Dispatcher.Invoke(() =>
            {
                Log.Information("Log file created. Starting tail.");
                lock (_tailSync) _lastPos = 0;
            });
            _watcher.Changed += (_, __) => Dispatcher.Invoke(() =>
            {
                // Optional: Only reset if file size is smaller than _lastPos
                var info = new FileInfo(Path.Combine(dir, fileName));
                long last;
                lock (_tailSync) last = _lastPos;
                if (info.Exists && info.Length < last)
                {
                    Log.Information("Log file changed (truncated/rotated). Restarting tail.");
                    lock (_tailSync) _lastPos = 0;
                }
            });
            _watcher.EnableRaisingEvents = true;

            // Start tailing the log and process new lines as they are written
            _logPollTimer?.Stop();
            _logPollTimer = new System.Timers.Timer(LOG_POLL_INTERVAL_MS);
            _logPollTimer.Elapsed += (s, e) =>
            {
                var batch = ReadNewLines(filePath).ToArray();
                foreach (var line in batch)
                {
                    var m = _bhTag.Match(line);  // look for [bHaptics] entry in log file
                    if (m.Success)
                    {
                        string cmd = m.Groups[1].Value.TrimStart(',', ' ', '\t');
                        Log.Debug("  -> Matched bHaptics cmd: {Cmd}", cmd);
                        ProcessLine(cmd);
                    }
                }
            };
            _logPollTimer.Start();

            Log.Information("Now tailing {FilePath}", filePath);
        }

        private IEnumerable<string> ReadNewLines(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long pos;
            lock (_tailSync) pos = _lastPos;
            fs.Seek(pos, SeekOrigin.Begin);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                lock (_tailSync) _lastPos = fs.Position;
                yield return line;
            }
        }

        private void StartWebSocket()
        {
            if (_port == 0)
            {
                MessageBox.Show("WebSocket mode enabled but Port = 0.\nSet a valid port in config.cfg.",
                                "bHapticsRelay", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _wsServer = new WebSocketServer($"ws://0.0.0.0:{_port}");
                _wsServer.Start(sock =>
                {
                    _clients.Add(sock);
                    // Dispatcher.Invoke(UpdateWsClientsCount);  // UpdateWsClientsCount calls Dispatcher.Invoke
                    UpdateWsClientsCount();

                    sock.OnOpen = () => Log.Information("WS client connected");
                    sock.OnClose = () =>
                    {
                        Log.Information("WS client disconnected");
                        _clients.TryTake(out _);
                        // Dispatcher.Invoke(UpdateWsClientsCount);  // UpdateWsClientsCount calls Dispatcher.Invoke
                        UpdateWsClientsCount();
                    };
                    sock.OnMessage = msg => ProcessLine(msg, sock);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start WebSocket server on port {Port}", _port);
                MessageBox.Show($"Cannot start WebSocket server on port {_port}:\n{ex.Message}",
                                "bHapticsRelay", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Parses a single CSV‐formatted command line (from file tail or WebSocket) and invokes
        /// the matching BhapticsSDK2Wrapper export. See BHapticsSDKWrapper.cs for more information.
        /// CultureInfo.InvariantCulture is used to prevent locale issues when parsing floats.
        /// </summary>
        /// <param name="line">
        /// A comma‐delimited string where the first token is the method name and subsequent tokens
        /// are its parameters (e.g. “play,Explosion” or “playDot,1,40,0;1;5”).
        /// </param>
        private void ProcessLine(string line, IWebSocketConnection? sock = null)
        {
            var parts = line.Split(',');

            // ignore blank lines / whitespace
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            {
                Log.Debug("Skipping empty line.");
                return;
            }

            // Update Last command label on UI thread
            Dispatcher.Invoke(() => {
                LastCommandText.Text = $"Last command: {line.Trim()}";
            });

            try
            {
                switch (parts[0])
                {
                    // play(string key)
                    //   Plays a pre-defined haptic event by key/name.
                    // Parameters:
                    //   string key         Identifier for the haptic pattern (event key).
                    // Returns:
                    //   int                Request ID (>0) if playback started; otherwise, negative or zero.
                    // Example:
                    //   play,Explosion     ⇒ Log.Debug("play({Key}) => requestId {Req}", "Explosion", req);
                    case "play":
                        int req = BhapticsSDK2Wrapper.play(parts[1]);
                        Log.Debug("play({Key}) => requestId {Req}", parts[1], req);
                        sock?.Send(req.ToString());  // websocket reply if needed
                        break;

                    // playPos(string key, int position)
                    //   Legacy overload: plays a haptic pattern using only the position index.
                    // Parameters:
                    //   string key         Identifier for the haptic pattern.
                    //   int    position    Device position index.
                    // Returns:
                    //   int                Request ID (>0) if playback started; otherwise, negative or zero.
                    // Example:
                    //   playPos,HeartBeat,1 ⇒ Log.Debug("playPos({Key},{Pos}) => requestId {Req}", "HeartBeat", 1, posReq);
                    case "playPos":
                        if (parts.Length < 3)
                        {
                            Log.Warning("playPos needs 2 params (key,position): {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        if (!int.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out int posIdx))
                        {
                            Log.Warning("playPos invalid position: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        int posReq = BhapticsSDK2Wrapper.playPos(parts[1], posIdx);
                        Log.Debug("playPos({Key},{Pos}) => requestId {Req}", parts[1], parts[2], posReq);
                        sock?.Send(posReq.ToString());  // websocket reply if needed
                        break;

                    // playPosParam(string key, int position, float intensity, float duration, float angleX, float offsetY)
                    //   Plays a pattern at a specific position with custom parameters.
                    // Parameters:
                    //   string key         Identifier for the haptic pattern.
                    //   int    position    Device position index.
                    //   float  intensity   Intensity multiplier (0.0–1.0).
                    //   float  duration    Duration multiplier (seconds).
                    //   float  angleX      Rotation angle around X axis (spatial mapping).
                    //   float  offsetY     Vertical offset (spatial mapping).
                    // Returns:
                    //   int                Request ID (>0) if playback started; otherwise, negative or zero.
                    // Example:
                    //   playPosParam,Bump,2,0.8,1.2,45,10
                    case "playPosParam":
                        if (parts.Length < 7)
                        {
                            Log.Warning("playPosParam needs 6 params (key,pos,intensity,duration,angleX,offsetY): {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        if (!int.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out int ppPos) ||
                            !float.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out float ppInt) ||
                            !float.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out float ppDur) ||
                            !float.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out float ppAng) ||
                            !float.TryParse(parts[6], NumberStyles.Any, CultureInfo.InvariantCulture, out float ppOff))
                        {
                            Log.Warning("playPosParam bad numeric param: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        int posParamReq = BhapticsSDK2Wrapper.playPosParam(parts[1], ppPos, ppInt, ppDur, ppAng, ppOff);
                        Log.Debug("playPosParam({Key},{Pos},{Int},{Dur},{Ang},{Off}) => requestId {Req}",
                                  parts[1], ppPos, ppInt, ppDur, ppAng, ppOff, posParamReq);
                        sock?.Send(posParamReq.ToString());
                        break;

                    // playGlove(string key, int timeout)
                    //   Plays a haptic pattern on a glove device (haptic glove support).
                    // Parameters:
                    //   string key         Identifier for the haptic pattern.
                    //   int    timeout     Timeout in milliseconds for glove response.
                    // Returns:
                    //   int                Request ID (>0) if playback started; otherwise, negative or zero.
                    // Example:
                    //   playGlove,GripPulse,500 ⇒ Log.Debug("playGlove({Key},{Timeout}) => requestId {Req}", "GripPulse", 500, gloveReq);
                    case "playGlove":
                        if (parts.Length < 3)
                        {
                            Log.Warning("playGlove needs 2 params (key,timeoutMs): {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        if (!int.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out int gloveTimeout))
                        {
                            Log.Warning("playGlove bad timeout value: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        int gloveReq = BhapticsSDK2Wrapper.playGlove(parts[1], gloveTimeout);
                        Log.Debug("playGlove({Key},{Timeout}) => requestId {Req}", parts[1], gloveTimeout, gloveReq);
                        sock?.Send(gloveReq.ToString());
                        break;

                    // playDot(int position, int durationMillis, int[] motors, int size)
                    //   Plays a dot pattern: activates specific motors for a given duration.
                    // Parameters:
                    //   int    position        Device position index.
                    //   int    durationMillis  Duration of each dot in ms.
                    //   int[]  motors          Array of motor indices to activate.
                    //   int    size            Length of the motors array.
                    // Returns:
                    //   int                   Request ID (>0) if playback started; otherwise, negative or zero.
                    // Example:
                    //   playDot,1,40,0;1;5;7 ⇒ motors={0,1,5,7}
                    case "playDot":
                        if (parts.Length < 4)
                        {
                            Log.Warning("playDot needs 3 params (pos,duration,motors): {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        if (!int.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out int dotPos) ||
                            !int.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out int dotDur))
                        {
                            Log.Warning("playDot bad pos/duration: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        string[] motorTokens = parts[3].Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                        int[] motorsArr = new int[motorTokens.Length];
                        bool dotBad = false;
                        for (int i = 0; i < motorTokens.Length; i++)
                        {
                            if (!int.TryParse(motorTokens[i], NumberStyles.Any, CultureInfo.InvariantCulture, out motorsArr[i]))
                            {
                                dotBad = true; break;
                            }
                        }
                        if (dotBad)
                        {
                            Log.Warning("playDot bad motor list: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        int dotReq = BhapticsSDK2Wrapper.playDot(dotPos, dotDur, motorsArr, motorsArr.Length);
                        Log.Debug("playDot pos={Pos} motors={Count} => requestId {Req}", dotPos, motorsArr.Length, dotReq);
                        sock?.Send(dotReq.ToString());
                        break;

                    // playWaveform(int position, int[] motorValues, int[] playTimeValues, int[] shapeValues, int motorLen)
                    //   Plays a waveform pattern by specifying motor intensities and timing.
                    // Parameters:
                    //   int    position        Device position index.
                    //   int[]  motorValues     Array of intensity values per motor.
                    //   int[]  playTimeValues  Array of play durations per motor.
                    //   int[]  shapeValues     Array of waveform shape parameters.
                    //   int    motorLen        Length of the motor arrays.
                    // Returns:
                    //   int                   Request ID (>0) if playback started; otherwise, negative or zero.
                    // Example:
                    //   playWaveform,2,100;80;60,10;10;10,0;0;0
                    case "playWaveform":
                        if (parts.Length < 5)
                        {
                            Log.Warning("playWaveform needs 4 array params: {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        if (!int.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out int wfPos))
                        {
                            Log.Warning("playWaveform bad position: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        bool TryParseIntArray(string s, out int[] arr)
                        {
                            var toks = s.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                            arr = new int[toks.Length];
                            for (int i = 0; i < toks.Length; i++)
                                if (!int.TryParse(toks[i], NumberStyles.Any, CultureInfo.InvariantCulture, out arr[i]))
                                    return false;
                            return true;
                        }
                        if (!TryParseIntArray(parts[2], out int[] motorVals) ||
                            !TryParseIntArray(parts[3], out int[] playTimes) ||
                            !TryParseIntArray(parts[4], out int[] shapeVals))
                        {
                            Log.Warning("playWaveform bad array element: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        if (motorVals.Length != playTimes.Length || playTimes.Length != shapeVals.Length)
                        {
                            Log.Warning("playWaveform array length mismatch: {Line}", line);
                            sock?.Send("ERR:length_mismatch");
                            break;
                        }
                        int wfReq = BhapticsSDK2Wrapper.playWaveform(wfPos, motorVals, playTimes, shapeVals, motorVals.Length);
                        Log.Debug("playWaveform pos={Pos} len={Len} => requestId {Req}", wfPos, motorVals.Length, wfReq);
                        sock?.Send(wfReq.ToString());
                        break;

                    // playPath(int position, float[] xValues, float[] yValues, int[] intensityValues, int Len)
                    //   Plays a path-based haptic effect using X/Y coordinates and intensities.
                    // Parameters:
                    //   int      position        Device position index.
                    //   float[]  xValues         Array of X-axis coordinates.
                    //   float[]  yValues         Array of Y-axis coordinates.
                    //   int[]    intensityValues Array of intensity values per point.
                    //   int      Len             Length of the coordinate/intensity arrays.
                    // Returns:
                    //   int                     Request ID (>0) if playback started; otherwise, negative or zero.
                    // Example:
                    //   playPath,3,0.1;0.5;0.8,0.0;0.2;0.4,30;60;90
                    case "playPath":
                        if (parts.Length < 5)
                        {
                            Log.Warning("playPath needs 4 array params: {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        if (!int.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out int pathPos))
                        {
                            Log.Warning("playPath bad position: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        bool TryParseFloatArray(string s, out float[] arr)
                        {
                            var toks = s.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                            arr = new float[toks.Length];
                            for (int i = 0; i < toks.Length; i++)
                                if (!float.TryParse(toks[i], NumberStyles.Any, CultureInfo.InvariantCulture, out arr[i]))
                                    return false;
                            return true;
                        }
                        if (!TryParseFloatArray(parts[2], out float[] xs) ||
                            !TryParseFloatArray(parts[3], out float[] ys) ||
                            !TryParseIntArray(parts[4], out int[] ints))
                        {
                            Log.Warning("playPath bad array element: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        if (xs.Length != ys.Length || ys.Length != ints.Length)
                        {
                            Log.Warning("playPath array length mismatch: {Line}", line);
                            sock?.Send("ERR:length_mismatch");
                            break;
                        }
                        int pathReq = BhapticsSDK2Wrapper.playPath(pathPos, xs, ys, ints, xs.Length);
                        Log.Debug("playPath pos={Pos} pts={Len} => requestId {Req}", pathPos, xs.Length, pathReq);
                        sock?.Send(pathReq.ToString());
                        break;

                    // playLoop(string key, float intensity, float duration, float angleX, float offsetY, int interval, int maxCount)
                    //   Plays a looping haptic pattern with interval and max loop count.
                    // Parameters:
                    //   string key         Identifier for the haptic pattern.
                    //   float  intensity   Intensity multiplier (0.0–1.0).
                    //   float  duration    Duration multiplier (seconds).
                    //   float  angleX      Rotation angle around X axis.
                    //   float  offsetY     Vertical offset.
                    //   int    interval    Milliseconds between loops.
                    //   int    maxCount    Maximum loops (0 = infinite).
                    // Returns:
                    //   int               Request ID (>0) if playback started; otherwise, negative or zero.
                    // Example:
                    //   playLoop,Pulse,1.0,0.5,0,0,200,0
                    case "playLoop":
                        if (parts.Length < 8)
                        {
                            Log.Warning("playLoop needs 7 params (key,intensity,duration,angleX,offsetY,interval,maxCount): {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        if (!float.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out float lpInt) ||
                            !float.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out float lpDur) ||
                            !float.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out float lpAng) ||
                            !float.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out float lpOff) ||
                            !int.TryParse(parts[6], NumberStyles.Any, CultureInfo.InvariantCulture, out int lpIntv) ||
                            !int.TryParse(parts[7], NumberStyles.Any, CultureInfo.InvariantCulture, out int lpCnt))
                        {
                            Log.Warning("playLoop bad numeric param: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        int loopReq = BhapticsSDK2Wrapper.playLoop(parts[1], lpInt, lpDur, lpAng, lpOff, lpIntv, lpCnt);
                        Log.Debug("playLoop({Key}) => requestId {Req}", parts[1], loopReq);
                        sock?.Send(loopReq.ToString());
                        break;

                    // playWithoutResult(string key)
                    //   Plays a pattern without returning a result code (fire-and-forget).
                    // Parameters:
                    //   string key         Identifier for the haptic pattern.
                    // Returns:
                    //   void
                    // Example:
                    //   playWithoutResult,Click
                    case "playWithoutResult":
                        if (parts.Length < 2)
                        {
                            Log.Warning("playWithoutResult needs key param: {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        BhapticsSDK2Wrapper.playWithoutResult(parts[1]);
                        sock?.Send("playWithoutResult sent");
                        break;

                    // stop(int key)
                    //   Stops a specific haptic playback by request ID.
                    // Parameters:
                    //   int key            Request ID returned from play/playPosParam.
                    // Returns:
                    //   bool               True if playback stopped; otherwise, false.
                    // Example:
                    //   stop,1234
                    case "stop":
                        if (parts.Length < 2)
                        {
                            Log.Warning("stop needs requestId param: {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        if (!int.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out int stopReqId))
                        {
                            Log.Warning("stop invalid requestId: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        BhapticsSDK2Wrapper.stop(stopReqId);
                        break;

                    // stopByEventId(string eventId)
                    //   Stops playback of a haptic event by its event key.
                    // Parameters:
                    //   string eventId     Event key/name to stop.
                    // Returns:
                    //   bool               True if playback stopped; otherwise, false.
                    // Example:
                    //   stopByEventId,Explosion
                    case "stopByEventId":
                        if (parts.Length < 2)
                        {
                            Log.Warning("stopByEventId needs key param: {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        bool stoppedEvt = BhapticsSDK2Wrapper.stopByEventId(parts[1]);
                        Log.Debug("stopByEventId({Key}) => {Res}", parts[1], stoppedEvt);
                        sock?.Send(stoppedEvt.ToString());  // websocket reply if needed
                        break;

                    // stopAll()
                    //   Stops all active haptic feedback on the device.
                    // Parameters:
                    //   (none)
                    // Returns:
                    //   bool               True if all playback stopped; otherwise, false.
                    // Example:
                    //   stopAll
                    case "stopAll":
                        BhapticsSDK2Wrapper.stopAll();
                        break;

                    // isPlaying()
                    //   Checks if any haptic feedback is currently playing.
                    // Parameters:
                    //   (none)
                    // Returns:
                    //   bool               True if any pattern is playing; otherwise, false.
                    // Example:
                    //   isPlaying
                    case "isPlaying":
                        bool playing = BhapticsSDK2Wrapper.isPlaying();
                        Log.Information("isPlaying: {0}", playing);
                        sock?.Send(playing.ToString());  // websocket reply if needed
                        break;

                    // isPlayingByRequestId(int key)
                    //   Checks if a specific request ID is still playing.
                    // Parameters:
                    //   int key            Request ID to query.
                    // Returns:
                    //   bool               True if still playing; otherwise, false.
                    // Example:
                    //   isPlayingByRequestId,1234
                    case "isPlayingByRequestId":
                        if (parts.Length < 2)
                        {
                            Log.Warning("isPlayingByRequestId needs requestId param: {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        if (!int.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out int playReqId))
                        {
                            Log.Warning("isPlayingByRequestId invalid requestId: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        bool byReq = BhapticsSDK2Wrapper.isPlayingByRequestId(playReqId);
                        Log.Information("isPlayingByRequestId({Req}) => {Res}", playReqId, byReq);
                        sock?.Send(byReq.ToString());
                        break;

                    // isPlayingByEventId(string eventId)
                    //   Checks if a haptic event identified by key is playing.
                    // Parameters:
                    //   string eventId     Event key/name to query.
                    // Returns:
                    //   bool               True if still playing; otherwise, false.
                    // Example:
                    //   isPlayingByEventId,HeartBeat
                    case "isPlayingByEventId":
                        if (parts.Length < 2)
                        {
                            Log.Warning("isPlayingByEventId needs key param: {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        bool byEvt = BhapticsSDK2Wrapper.isPlayingByEventId(parts[1]);
                        Log.Information("isPlayingByEventId({Key}) => {Res}", parts[1], byEvt);
                        sock?.Send(byEvt.ToString());  // websocket reply if needed
                        break;

                    // isbHapticsConnected(int position)
                    //   Checks whether a bHaptics device is connected at the specified index.
                    // Parameters:
                    //   int position    Device position index.
                    // Returns:
                    //   bool            True if device is connected; otherwise, false.
                    // Example:
                    //   isbHapticsConnected,1
                    case "isbHapticsConnected":
                        if (parts.Length < 2)
                        {
                            Log.Warning("isbHapticsConnected needs position param: {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        if (!int.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out int devPos))
                        {
                            Log.Warning("isbHapticsConnected invalid position: {Line}", line);
                            sock?.Send("ERR:bad_number");
                            break;
                        }
                        bool devConn = BhapticsSDK2Wrapper.isbHapticsConnected(devPos);
                        Log.Information("isbHapticsConnected({Pos}) => {Res}", devPos, devConn);
                        sock?.Send(devConn.ToString());
                        break;

                    // ping(string address)
                    //   Sends a ping to a specific bHaptics device.
                    // Parameters:
                    //   string address  bHaptics device to ping.
                    // Returns:
                    //   bool            True if device responded; otherwise, false.
                    // Example:
                    //   ping,00:11:22:33:44:55
					// NOTE: must test if we use MAC address here or something else
                    case "ping":
                        if (parts.Length < 2)
                        {
                            Log.Warning("ping needs address param: {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        bool pingRes = BhapticsSDK2Wrapper.ping(parts[1]);
                        Log.Information("ping({Addr}) => {Res}", parts[1], pingRes);
                        sock?.Send(pingRes.ToString());
                        break;

                    // pingAll()
                    //   Broadcasts a ping to all known bHaptics devices.
                    // Parameters:
                    //   (none)
                    // Returns:
                    //   bool   True if at least one device responded; otherwise, false.
                    // Example:
                    //   pingAll ⇒ Log.Information("pingAll: {0}", pinged);
                    case "pingAll":
                        bool pinged = BhapticsSDK2Wrapper.pingAll();
                        Log.Information("pingAll: {0}", pinged);
                        sock?.Send(pinged.ToString());  // websocket reply if needed
                        break;

                    // swapPosition(string address)
                    //   Swaps primary/secondary positions for the device at the given address.
                    // Parameters:
                    //   string address  Device address to swap.
                    // Returns:
                    //   bool            True if swap succeeded; otherwise, false.
                    // Example:
                    //   swapPosition,00:11:22:33:44:55
                    case "swapPosition":
                        if (parts.Length < 2)
                        {
                            Log.Warning("swapPosition needs address param: {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        bool swap = BhapticsSDK2Wrapper.swapPosition(parts[1]);
                        Log.Information("swapPosition({Addr}) => {Res}", parts[1], swap);
                        sock?.Send(swap.ToString());
                        break;

                    // getDeviceInfoJson()
                    //   Retrieves connected device information as JSON.
                    // Parameters:
                    //   (none)
                    // Returns:
                    //   string          JSON payload describing devices.
                    // Example:
                    //   getDeviceInfoJson
                    case "getDeviceInfoJson":
                        {
                            IntPtr ptr = BhapticsSDK2Wrapper.getDeviceInfoJson();
                            string json = PtrToUtf8(ptr);
                            Log.Debug("getDeviceInfoJson: payload: {Json}", json);
                            sock?.Send(json);
                            break;
                        }

                    // isPlayerInstalled()
                    //   Determines if bHaptics Player is installed on the system.
                    // Parameters:
                    //   (none)
                    // Returns:
                    //   bool            True if installed; otherwise, false.
                    // Example:
                    //   isPlayerInstalled
                    case "isPlayerInstalled":
                        bool installed = BhapticsSDK2Wrapper.isPlayerInstalled();
                        Log.Information("isPlayerInstalled => {Res}", installed);
                        sock?.Send(installed.ToString());
                        break;

                    // isPlayerRunning()
                    //   Determines if bHaptics Player is currently running.
                    // Parameters:
                    //   (none)
                    // Returns:
                    //   bool            True if running; otherwise, false.
                    // Example:
                    //   isPlayerRunning
                    case "isPlayerRunning":
                        bool running = BhapticsSDK2Wrapper.isPlayerRunning();
                        Log.Information("isPlayerRunning => {Res}", running);
                        sock?.Send(running.ToString());
                        break;

                    // launchPlayer(bool launch)
                    //   Launches bHaptics Player.
                    // Parameters:
                    //   bool launch      True = launch player
                    // Returns:
                    //   bool             True if operation succeeded; otherwise, false.
                    // Example:
                    //   launchPlayer,true
                    case "launchPlayer":
                        if (parts.Length < 2)
                        {
                            Log.Warning("launchPlayer needs launch param: {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        bool launchFlag;
                        // accept "true/false" or "1/0"
                        if (!bool.TryParse(parts[1], out launchFlag))
                        {
                            if (int.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out int intFlag))
                                launchFlag = intFlag != 0;
                            else
                            {
                                Log.Warning("launchPlayer invalid bool: {Line}", line);
                                sock?.Send("ERR:bad_bool");
                                break;
                            }
                        }
                        bool launchRes = BhapticsSDK2Wrapper.launchPlayer(launchFlag);
                        Log.Information("launchPlayer({Flag}) => {Res}", launchFlag, launchRes);
                        sock?.Send(launchRes.ToString());
                        break;

                    // bHapticsGetHapticMessage(int lastVersion?)
                    //   Fetches new haptic messages from the server.
                    // Parameters (optional):
                    //   int lastVersion  Last version number received (-1 to fetch all).
                    // Returns:
                    //   string           "<status> <json>" – status code followed by JSON payload.
                    // Example:
                    //   bHapticsGetHapticMessage,-1
                    case "bHapticsGetHapticMessage":
                        {
                            int lastVer = -1;
                            if (parts.Length >= 2 && !int.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out lastVer))
                            {
                                Log.Warning("bHapticsGetHapticMessage invalid version: {Line}", line);
                                sock?.Send("ERR:bad_number");
                                break;
                            }
                            int status;
                            IntPtr ptr = BhapticsSDK2Wrapper.bHapticsGetHapticMessage(
                                _apiKey!, _appId!, lastVer, out status);
                            string json = PtrToUtf8(ptr);
                            Log.Debug("bHapticsGetHapticMessage: status {Status}, payload: {Json}", status, json);
                            sock?.Send($"{status} {json}");
                            break;
                        }

                    // bHapticsGetHapticMappings(string apiKey, string appId, int lastVersion, out int status)
                    //   Retrieves custom haptic event mappings from the bHaptics server for the given application.
                    // Parameters:
                    //   string apiKey      User’s bHaptics API key.
                    //   string appId       Application identifier.
                    //   int    lastVersion Version number of the last mapping received (use -1 to fetch all).
                    //   out int status     Output status code from the server.
                    // Returns:
                    //   IntPtr             Pointer to a null-terminated UTF8 JSON string containing new mappings.
                    // Example:
                    //   bHapticsGetHapticMappings,myKey,myApp,-1,out status ⇒ ptr  
                    //   string json = PtrToUtf8(ptr);  
                    //   Log.Debug("bHapticsGetHapticMappings: status {Status}, payload: {Json}", status, json);
                    case "bHapticsGetHapticMappings":
                        {
                            int status;
                            IntPtr ptr = BhapticsSDK2Wrapper.bHapticsGetHapticMappings(
                                _apiKey!,
                                _appId!,
                                -1,      // lastVersion = -1 to get all
                                out status);

                            string json = PtrToUtf8(ptr);
                            Log.Debug("bHapticsGetHapticMappings: status {Status}, payload: {Json}", status, json);
                            sock?.Send(status.ToString() + " " + json.ToString());  // websocket reply if needed
                            break;
                        }

                    // getEventTime(string eventId)
                    //   Retrieves timing metadata (e.g., duration) for a specific event.
                    // Parameters:
                    //   string eventId   Event key/name.
                    // Returns:
                    //   int              Timing information (implementation-defined).
                    // Example:
                    //   getEventTime,HeartBeat
                    case "getEventTime":
                        if (parts.Length < 2)
                        {
                            Log.Warning("getEventTime needs eventId param: {Line}", line);
                            sock?.Send("ERR:invalid_params");
                            break;
                        }
                        int evtTime = BhapticsSDK2Wrapper.getEventTime(parts[1]);
                        Log.Information("getEventTime({Key}) => {Time}", parts[1], evtTime);
                        sock?.Send(evtTime.ToString());
                        break;

                    // getHapticMappingsJson()
                    //   Retrieves the full JSON payload of haptic mappings from the native library.
                    // Returns:
                    //   IntPtr   Pointer to a null-terminated UTF8 JSON string containing all mappings.
                    // Example:
                    //   IntPtr ptr = getHapticMappingsJson();
                    //   string json = PtrToUtf8(ptr);
                    //   Log.Debug("getHapticMappingsJson: payload: {Json}", json);
                    case "getHapticMappingsJson":
                        {
                            IntPtr ptr = BhapticsSDK2Wrapper.getHapticMappingsJson();
                            string json = PtrToUtf8(ptr);
                            Log.Debug("getHapticMappingsJson: payload: {Json}", json);
                            sock?.Send(json);
                            break;
                        }

                    default:
                        Log.Warning("Unknown command: {0}", line);
                        sock?.Send("ERR:unknown_command");  // websocket reply if needed
                        break;
                }

                Log.Information("Processed command: {0}", line);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed processing line: {0}", line);
                sock?.Send($"ERR:{ex.Message}");  // websocket reply if needed
            }
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (_testEvent == null) return;
            BhapticsSDK2Wrapper.play(_testEvent);
            Log.Information("Tested {Event}", _testEvent);
        }
                private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Dispose();
            Application.Current.Shutdown();
        }

        #region IDisposable pattern
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _logPollTimer?.Stop();
                _logPollTimer?.Dispose();

                _watcher?.Dispose();

                BhapticsSDK2Wrapper.wsClose();   // close native WS first
                _wsServer?.Dispose();

                _statusTimer?.Stop();
                _statusTimer?.Dispose();
            }
            finally
            {
                Log.CloseAndFlush();
                _disposed = true;
            }
        }
        #endregion

        // Helper functions

        // If Default Config is used (offline mode), disable validation of the blob so the player will accept it
       static string PrepareOfflineJson(string json)
        {
            var doc = JsonNode.Parse(json)!.AsObject();
            doc["disableValidation"] = true;
            return doc.ToJsonString();
        }

        /// <summary>
        /// Converts a C-style UTF8 null-terminated IntPtr into a managed string.
        /// </summary>
        private static string PtrToUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return string.Empty;

            int len = 0;
            while (Marshal.ReadByte(ptr, len) != 0)
                len++;

            if (len == 0) return string.Empty;

            byte[] buffer = new byte[len];
            Marshal.Copy(ptr, buffer, 0, len);
            return Encoding.UTF8.GetString(buffer);
        }

        // Regex for matching [bHaptics] commands in log files
        private static readonly Regex _bhTag =
            new Regex(@"\[bHaptics\]\s*(.*)", RegexOptions.Compiled);

        // ---------- safe-parse helpers ----------
        private static bool TryParseInt(string s, out int v) =>
            int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);

        private static bool TryParseFloat(string s, out float v) =>
            float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);

        private void UpdateWsClientsCount()
        {
            int count = _clients.Count;
            Dispatcher.Invoke(() => WsClientsText.Text = $"WS Clients Connected: {count}");
        }
    }
}