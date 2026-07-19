using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RblxExecutorUI
{
    // ============================================================
    //  DATA MODELS
    // ============================================================

    public class ScriptItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
    }

    public class BloxScriptItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string ScriptId { get; set; }
        public string Views { get; set; }
        public string Author { get; set; }
        public string RawScript { get; set; }
    }

    // ScriptBlox API response models
    public class BloxApiResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("data")]
        public BloxApiData Data { get; set; }
    }

    public class BloxApiData
    {
        [JsonPropertyName("scripts")]
        public List<BloxApiScript> Scripts { get; set; }

        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("currentPage")]
        public int CurrentPage { get; set; }
    }

    public class BloxApiScript
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("views")]
        public int Views { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("script")]
        public string Script { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("game")]
        public string Game { get; set; }
    }

    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        const uint MEM_COMMIT = 0x1000;
        const uint MEM_RELEASE = 0x8000;
        const uint PAGE_READWRITE = 0x04;

        private DispatcherTimer _notificationTimer;
        public ObservableCollection<ScriptItem> ScriptsList { get; set; } = new ObservableCollection<ScriptItem>();
        public ObservableCollection<BloxScriptItem> BloxScriptsList { get; set; } = new ObservableCollection<BloxScriptItem>();

        private bool _isExecuting = false;
        private bool _isBloxView = false;
        private int _bloxCurrentPage = 1;
        private string _bloxSearchQuery = "";
        private readonly HttpClient _httpClient;
        private bool _isAttached = false;

        public MainWindow()
        {
            InitializeComponent();

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SyntaxExecutor/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(15);

            this.Loaded += MainWindow_Loaded;

            _notificationTimer = new DispatcherTimer();
            _notificationTimer.Interval = TimeSpan.FromSeconds(3);
            _notificationTimer.Tick += (s, e) => CloseNotification();

            ScriptHubList.ItemsSource = ScriptsList;
            BloxScriptsListView.ItemsSource = BloxScriptsList;
            LoadScriptsFolder();

            try
            {
                string xshdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lua.xshd");
                if (File.Exists(xshdPath))
                {
                    using (XmlTextReader reader = new XmlTextReader(xshdPath))
                    {
                        Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
            }
            catch { }
        }

        private void LoadScriptsFolder()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                ScriptsList.Clear();
                var files = Directory.GetFiles(path, "*.*")
                                     .Where(s => s.EndsWith(".lua") || s.EndsWith(".luau") || s.EndsWith(".txt"));

                foreach (var file in files)
                {
                    ScriptsList.Add(new ScriptItem
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file
                    });
                }

                DashScriptCount.Text = ScriptsList.Count.ToString();
            }
            catch (Exception ex)
            {
                App.LogException(ex, "LoadScriptsFolder");
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var luaDef = HighlightingManager.Instance.GetDefinition("Lua");
                if (luaDef != null)
                {
                    Editor.SyntaxHighlighting = luaDef;
                }
                Editor.Text = "-- Syntax Executor Premium\n-- Version: 1.0.0\n\nlocal player = game.Players.LocalPlayer\nprint(\"Hello, \" .. player.Name)";

                InitializeCore();
                AnimateWindowIn();
            }
            catch (Exception ex)
            {
                InjectionStatus.Text = "INIT ERROR";
                InjectionStatus.Foreground = Brushes.Orange;
                App.LogException(ex, "MainWindow_Loaded");
            }
        }

        private void AnimateWindowIn()
        {
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuarticEase() { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InitializeCore()
        {
            try
            {
                bool init = RblxCore.Initialize();
                if (init)
                {
                    UpdateStatus("NOT ATTACHED", "#475569", "#334155");
                }
                else
                {
                    UpdateStatus("SYSCALL FAILED", "#FF3B5C", "#FF3B5C");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("INIT ERROR", "#FF8C00", "#FF8C00");
                App.LogException(ex, "InitializeCore");
            }
        }

        private void UpdateStatus(string text, string colorHex, string dotHex)
        {
            InjectionStatus.Text = text;
            InjectionStatus.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(colorHex);
            StatusIndicator.Fill = (SolidColorBrush)new BrushConverter().ConvertFrom(dotHex);
        }

        // Window controls
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        // Tabs
        private void Tab_Editor(object sender, RoutedEventArgs e) => SwitchTab(ViewEditor);
        private void Tab_ScriptHub(object sender, RoutedEventArgs e) => SwitchTab(ViewScriptHub);
        private void Tab_Clients(object sender, RoutedEventArgs e) => SwitchTab(ViewClients);
        private void Tab_Settings(object sender, RoutedEventArgs e) => SwitchTab(ViewSettings);
        private void Tab_Dashboard(object sender, RoutedEventArgs e) => SwitchTab(ViewDashboard);

        // Quick nav from dashboard
        private void Tab_Editor_Quick(object sender, RoutedEventArgs e) => SwitchTab(ViewEditor);
        private void Tab_ScriptHub_Quick(object sender, RoutedEventArgs e) => SwitchTab(ViewScriptHub);

        private void SwitchTab(Grid target)
        {
            if (ViewEditor == null) return;
            ViewEditor.Visibility = Visibility.Collapsed;
            ViewScriptHub.Visibility = Visibility.Collapsed;
            ViewClients.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Collapsed;
            ViewDashboard.Visibility = Visibility.Collapsed;

            target.Visibility = Visibility.Visible;
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            target.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        // Notification System
        private void ShowNotification(string message, bool isError = false)
        {
            NotificationText.Text = message;

            if (isError)
            {
                NotificationBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 59, 92));
                NotificationIcon.Data = Geometry.Parse("M 2,14 L 14,14 L 8,2 Z M 8,11 L 8,12 M 8,6 L 8,9");
                NotificationIcon.Stroke = new SolidColorBrush(Color.FromRgb(255, 59, 92));
            }
            else
            {
                NotificationBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 229, 255));
                NotificationIcon.Data = Geometry.Parse("M 8,2 A 6,6 0 1 1 7.9,2.1 Z M 6,8 L 8,10 L 12,5");
                NotificationIcon.Stroke = new SolidColorBrush(Color.FromRgb(0, 229, 255));
            }

            NotificationBox.Visibility = Visibility.Visible;

            TranslateTransform transform = new TranslateTransform(50, 0);
            NotificationBox.RenderTransform = transform;

            DoubleAnimation slideIn = new DoubleAnimation(50, 0, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new QuarticEase() { EasingMode = EasingMode.EaseOut }
            };
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));

            transform.BeginAnimation(TranslateTransform.XProperty, slideIn);
            NotificationBox.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            _notificationTimer.Stop();
            _notificationTimer.Start();
        }

        private void CloseNotification()
        {
            _notificationTimer.Stop();

            TranslateTransform transform = new TranslateTransform(0, 0);
            NotificationBox.RenderTransform = transform;

            DoubleAnimation slideOut = new DoubleAnimation(0, 20, TimeSpan.FromMilliseconds(200));
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));

            fadeOut.Completed += (s, e) => NotificationBox.Visibility = Visibility.Collapsed;

            transform.BeginAnimation(TranslateTransform.XProperty, slideOut);
            NotificationBox.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        // ============================================================
        //  SCRIPTBLOX INTEGRATION
        // ============================================================

        private async void ScriptBlox_Refresh(object sender, RoutedEventArgs e)
        {
            await FetchBloxScripts();
        }

        private void ShowLocalScripts(object sender, RoutedEventArgs e)
        {
            _isBloxView = false;
            LocalScriptsPanel.Visibility = Visibility.Visible;
            BloxScriptsPanel.Visibility = Visibility.Collapsed;
            BtnLocalScripts.Style = (Style)FindResource("BtnExecute");
            BtnBloxScripts.Style = (Style)FindResource("ToolBtn");
            LoadScriptsFolder();
        }

        private void ShowBloxScripts(object sender, RoutedEventArgs e)
        {
            _isBloxView = true;
            LocalScriptsPanel.Visibility = Visibility.Collapsed;
            BloxScriptsPanel.Visibility = Visibility.Visible;
            BtnLocalScripts.Style = (Style)FindResource("ToolBtn");
            BtnBloxScripts.Style = (Style)FindResource("BtnExecute");

            if (BloxScriptsList.Count == 0)
            {
                _ = FetchBloxScripts();
            }
        }

        private async Task FetchBloxScripts(string search = "", int page = 1)
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = "Fetching ScriptBlox scripts...";

                string url;
                if (!string.IsNullOrEmpty(search))
                {
                    url = $"https://scriptblox.com/api/script/search?q={Uri.EscapeDataString(search)}&page={page}&sort=most-recent";
                }
                else
                {
                    url = $"https://scriptblox.com/api/scripts?page={page}&sort=most-recent";
                }

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    ShowNotification($"ScriptBlox API error: {response.StatusCode}", true);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<BloxApiResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse == null || !apiResponse.Success)
                {
                    ShowNotification("Failed to fetch ScriptBlox scripts", true);
                    return;
                }

                BloxScriptsList.Clear();

                if (apiResponse.Data?.Scripts != null)
                {
                    foreach (var script in apiResponse.Data.Scripts)
                    {
                        BloxScriptsList.Add(new BloxScriptItem
                        {
                            Title = script.Title ?? "Untitled",
                            Description = script.Description ?? "No description",
                            ScriptId = script.Id,
                            Views = script.Views > 0 ? $"{script.Views:N0}" : "0",
                            Author = script.Author ?? "Unknown",
                            RawScript = script.Script ?? ""
                        });
                    }
                }

                DashScriptCount.Text = BloxScriptsList.Count.ToString();
                ShowNotification($"Loaded {BloxScriptsList.Count} scripts from ScriptBlox", false);
            }
            catch (HttpRequestException ex)
            {
                ShowNotification($"Network error: {ex.Message}", true);
            }
            catch (TaskCanceledException)
            {
                ShowNotification("Request timed out", true);
            }
            catch (Exception ex)
            {
                ShowNotification($"ScriptBlox error: {ex.Message}", true);
                App.LogException(ex, "FetchBloxScripts");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void BloxLoad_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag == null) return;

            string scriptId = btn.Tag.ToString();
            await LoadBloxScriptById(scriptId);
        }

        private async void BloxCopy_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag == null) return;

            string scriptId = btn.Tag.ToString();
            var script = BloxScriptsList.FirstOrDefault(s => s.ScriptId == scriptId);
            if (script != null && !string.IsNullOrEmpty(script.RawScript))
            {
                Clipboard.SetText(script.RawScript);
                ShowNotification("Script copied to clipboard!", false);
            }
            else
            {
                // Fetch raw script first
                await LoadBloxScriptById(scriptId, true);
            }
        }

        private async Task LoadBloxScriptById(string scriptId, bool copyOnly = false)
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = "Loading script...";

                string url = $"https://scriptblox.com/api/script/{scriptId}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    ShowNotification($"Failed to load script (HTTP {response.StatusCode})", true);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<BloxApiResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Data?.Scripts == null || apiResponse.Data.Scripts.Count == 0)
                {
                    ShowNotification("Script not found", true);
                    return;
                }

                var scriptData = apiResponse.Data.Scripts[0];
                string rawScript = scriptData.Script ?? "-- No script content";

                // Update the cached item
                var cached = BloxScriptsList.FirstOrDefault(s => s.ScriptId == scriptId);
                if (cached != null)
                {
                    cached.RawScript = rawScript;
                }

                if (copyOnly)
                {
                    Clipboard.SetText(rawScript);
                    ShowNotification("Script copied to clipboard!", false);
                }
                else
                {
                    // Load into editor and switch to it
                    Editor.Text = rawScript;
                    SwitchTab(ViewEditor);
                    ShowNotification($"Loaded: {scriptData.Title}", false);
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"Error loading script: {ex.Message}", true);
                App.LogException(ex, "LoadBloxScriptById");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _bloxSearchQuery = SearchBox.Text;

            // Debounce search
            await Task.Delay(500);
            if (_bloxSearchQuery != SearchBox.Text) return;

            if (!string.IsNullOrEmpty(_bloxSearchQuery) && _isBloxView)
            {
                await FetchBloxScripts(_bloxSearchQuery);
            }
        }

        // ============================================================
        //  EXECUTION
        // ============================================================

        private void Execute_Click(object sender, RoutedEventArgs e)
        {
            if (_isExecuting) return;
            _isExecuting = true;
            try
            {
                string script = Editor.Text;
                if (string.IsNullOrWhiteSpace(script))
                {
                    ShowNotification("No script to execute", true);
                    _isExecuting = false;
                    return;
                }

                Console.WriteLine($"[C# UI] Executing EDITOR script ({script.Length} chars)...");
                ShowNotification("Executing Script...", false);

                int result = RblxCore.ExecuteScript(script, script.Length);
                e.Handled = true;

                if (result == 0)
                {
                    ShowNotification("Executed Successfully!", false);
                }
                else
                {
                    string error = RblxCore.GetLastError();
                    ShowNotification($"Error: {error}", true);
                }
            }
            catch (Exception ex)
            {
                ShowNotification("Execution Exception", true);
                App.LogException(ex, "Execute_Click");
            }
            finally
            {
                _isExecuting = false;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Editor.Text = "";
            ShowNotification("Editor Cleared", false);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts");
                if (!Directory.Exists(scriptPath)) Directory.CreateDirectory(scriptPath);

                var dialog = new Microsoft.Win32.SaveFileDialog()
                {
                    InitialDirectory = scriptPath,
                    Filter = "Lua Scripts (*.lua;*.luau;*.txt)|*.lua;*.luau;*.txt|All Files (*.*)|*.*",
                    DefaultExt = "lua"
                };

                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllText(dialog.FileName, Editor.Text);
                    ShowNotification("Script Saved Successfully", false);
                    LoadScriptsFolder();
                }
            }
            catch (Exception)
            {
                ShowNotification("Failed to save file", true);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts");
                if (!Directory.Exists(scriptPath)) Directory.CreateDirectory(scriptPath);

                Process.Start("explorer.exe", scriptPath);
                ShowNotification("Opened Scripts Folder", false);
            }
            catch (Exception)
            {
                ShowNotification("Failed to open folder", true);
            }
        }

        private void ExecuteScriptHub_Click(object sender, RoutedEventArgs e)
        {
            if (_isExecuting) return;
            _isExecuting = true;
            try
            {
                var btn = sender as Button;
                if (btn != null && btn.Tag != null)
                {
                    string path = btn.Tag.ToString();
                    if (File.Exists(path))
                    {
                        string content = File.ReadAllText(path);
                        Console.WriteLine($"[C# UI] Executing HUB script: {Path.GetFileName(path)} ({content.Length} chars)...");
                        ShowNotification($"Executing {Path.GetFileName(path)}...", false);
                        RblxCore.ExecuteScript(content, content.Length);
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowNotification("Hub script error!", true);
                App.LogException(ex, "ExecuteScriptHub_Click");
            }
            finally
            {
                _isExecuting = false;
            }
        }

        private void CopyScriptHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var btn = sender as Button;
                if (btn != null && btn.Tag != null)
                {
                    string path = btn.Tag.ToString();
                    if (File.Exists(path))
                    {
                        Clipboard.SetText(File.ReadAllText(path));
                        ShowNotification("Code Copied!", false);
                    }
                }
            }
            catch (Exception)
            {
                ShowNotification("Copy Failed!", true);
            }
        }

        private void DeleteScriptHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var btn = sender as Button;
                if (btn != null && btn.Tag != null)
                {
                    string path = btn.Tag.ToString();
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        ShowNotification("Script Deleted!", false);
                        LoadScriptsFolder();
                    }
                }
            }
            catch (Exception)
            {
                ShowNotification("Delete Failed!", true);
            }
        }

        private void KillRoblox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var procs = Process.GetProcessesByName("RobloxPlayerBeta");
                foreach (var p in procs) { p.Kill(); }
                ShowNotification("Roblox Terminated", false);
            }
            catch { ShowNotification("No Roblox to Kill", true); }
        }

        private void StartProcessMonitor(uint pid)
        {
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += (s, ev) =>
            {
                try
                {
                    var proc = Process.GetProcessById((int)pid);
                    if (proc.HasExited) throw new Exception();
                }
                catch
                {
                    timer.Stop();
                    _isAttached = false;
                    UpdateStatus("NOT ATTACHED", "#475569", "#334155");
                    ClientPidText.Text = "PID: —  ·  Place: —";
                    ClientStatusBadge.Text = "DISCONNECTED";
                    ClientStatusBadge.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#475569");
                    ClientStatusDot.Fill = (SolidColorBrush)new BrushConverter().ConvertFrom("#475569");
                    ClientBadgeBg.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#111827");
                    ClientAccountName.Text = "Not Connected";
                    ClientAvatarImage.Source = null;
                    RblxCore.Disconnect();
                }
            };
            timer.Start();
        }

        private void StartClientDataPoller(uint pid)
        {
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(2);
            int attempts = 0;

            timer.Tick += (s, ev) =>
            {
                attempts++;
                if (attempts > 15)
                {
                    timer.Stop();
                    return;
                }

                var sb = new System.Text.StringBuilder(512);
                if (RblxCore.GetClientInfo(sb, sb.Capacity))
                {
                    string body = sb.ToString();
                    var parts = body.Split('|');
                    if (parts.Length >= 4 && parts[0] != "Unknown")
                    {
                        timer.Stop();

                        string accountName = parts[0];
                        string userId = parts[1];
                        string jobId = parts[2];
                        string placeId = parts[3];

                        Task.Run(async () =>
                        {
                            string finalPlaceName = $"Place: {placeId}";
                            try
                            {
                                using (var client = new HttpClient())
                                {
                                    client.DefaultRequestHeaders.Add("User-Agent", "Roblox/WinInet");

                                    if (placeId != "0")
                                    {
                                        string apiRes = await client.GetStringAsync($"https://economy.roblox.com/v2/assets/{placeId}/details");
                                        var match = System.Text.RegularExpressions.Regex.Match(apiRes, "\"Name\"\\s*:\\s*\"([^\"]+)\"");
                                        if (match.Success) finalPlaceName = match.Groups[1].Value;
                                    }

                                    string avatarMetaUrl = $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=150x150&format=Png&isCircular=false";
                                    string metaRes = await client.GetStringAsync(avatarMetaUrl);
                                    string actualImgUrl = "";

                                    var imgMatch = System.Text.RegularExpressions.Regex.Match(metaRes, "\"imageUrl\"\\s*:\\s*\"([^\"]+)\"");
                                    if (imgMatch.Success) actualImgUrl = imgMatch.Groups[1].Value;

                                    byte[] imgBytes = null;
                                    if (!string.IsNullOrEmpty(actualImgUrl))
                                    {
                                        imgBytes = await client.GetByteArrayAsync(actualImgUrl);
                                    }

                                    Dispatcher.Invoke(() =>
                                    {
                                        ClientAccountName.Text = accountName;
                                        ClientPidText.Text = $"PID: {pid}  ·  {finalPlaceName}";

                                        if (imgBytes != null)
                                        {
                                            try
                                            {
                                                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                                                bmp.BeginInit();
                                                bmp.StreamSource = new MemoryStream(imgBytes);
                                                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                                                bmp.EndInit();
                                                ClientAvatarImage.Source = bmp;
                                            }
                                            catch { }
                                        }
                                    });
                                }
                            }
                            catch
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    ClientAccountName.Text = accountName;
                                    ClientPidText.Text = $"PID: {pid}  ·  Place: {placeId}";
                                });
                            }
                        });
                    }
                }
            };
            timer.Start();
        }

        private async void Attach_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowNotification("Attaching to Roblox...", false);
                await Task.Run(() =>
                {
                    uint pid = RblxCore.FindRobloxProcess();

                    if (pid == 0)
                    {
                        Dispatcher.Invoke(() => ShowNotification("Roblox not found!", true));
                        return;
                    }

                    Dispatcher.Invoke(() => ShowNotification($"Connecting to PID {pid}...", false));
                    bool connected = RblxCore.Connect(pid);

                    Dispatcher.Invoke(() =>
                    {
                        if (connected)
                        {
                            _isAttached = true;
                            ShowNotification("Successfully attached to game!", false);

                            UpdateStatus("STABLE", "#00F593", "#00F593");
                            StatusDot.Fill = (SolidColorBrush)new BrushConverter().ConvertFrom("#00F593");
                            StatusDot.Effect = new System.Windows.Media.Effects.DropShadowEffect
                            {
                                Color = Color.FromRgb(0, 245, 147),
                                BlurRadius = 8,
                                ShadowDepth = 0,
                                Opacity = 0.8
                            };

                            ClientPidText.Text = $"PID: {pid}  ·  Place: Active";
                            ClientStatusBadge.Text = "ACTIVE SESSION";
                            ClientStatusBadge.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#00F593");
                            ClientStatusDot.Fill = (SolidColorBrush)new BrushConverter().ConvertFrom("#00F593");
                            ClientBadgeBg.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#0A1A10");

                            StartClientDataPoller(pid);
                            StartProcessMonitor(pid);
                        }
                        else
                        {
                            ShowNotification("Connect failed!", true);
                            UpdateStatus("FAILED", "#FF3B5C", "#FF3B5C");
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                ShowNotification("Attach Exception", true);
                App.LogException(ex, "Attach_Click");
            }
        }

        private void ToggleConsole_Checked(object sender, RoutedEventArgs e)
        {
            var handle = GetConsoleWindow();
            if (handle == IntPtr.Zero)
            {
                AllocConsole();
                RblxCore.RedirConsole();
                Console.WriteLine("[C# UI] Console allocated on user request.");
                handle = GetConsoleWindow();
            }

            if (handle != IntPtr.Zero) ShowWindow(handle, SW_SHOW);
        }

        private void ToggleConsole_Unchecked(object sender, RoutedEventArgs e)
        {
            var handle = GetConsoleWindow();
            if (handle != IntPtr.Zero) ShowWindow(handle, SW_HIDE);
        }

        private void TabClose_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for tab close functionality
        }
    }
}