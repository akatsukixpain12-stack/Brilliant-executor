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
    // ================================================================
    //  DATA MODELS
    // ================================================================
    public class ScriptItem
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
    }

    public class BloxScriptItem
    {
        public string Title       { get; set; } = "";
        public string Description { get; set; } = "";
        public string ScriptId    { get; set; } = "";
        public string Views       { get; set; } = "0";
        public string Author      { get; set; } = "";
        public string RawScript   { get; set; } = "";
    }

    // ScriptBlox API models
    public class BloxApiResponse
    {
        [JsonPropertyName("result")] public BloxApiResult? Result { get; set; }
    }
    public class BloxApiResult
    {
        [JsonPropertyName("scripts")] public BloxApiScriptsBlock? Scripts { get; set; }
    }
    public class BloxApiScriptsBlock
    {
        [JsonPropertyName("data")]       public List<BloxApiScript>? Data       { get; set; }
        [JsonPropertyName("totalPages")] public int                  TotalPages { get; set; }
    }
    public class BloxApiScript
    {
        [JsonPropertyName("_id")]        public string?  Id          { get; set; }
        [JsonPropertyName("title")]      public string?  Title       { get; set; }
        [JsonPropertyName("game")]       public BloxGame? Game       { get; set; }
        [JsonPropertyName("views")]      public int      Views       { get; set; }
        [JsonPropertyName("owner")]      public BloxOwner? Owner     { get; set; }
        [JsonPropertyName("script")]     public string?  Script      { get; set; }
        [JsonPropertyName("slug")]       public string?  Slug        { get; set; }
        [JsonPropertyName("createdAt")]  public string?  CreatedAt   { get; set; }
        [JsonPropertyName("isPatched")]  public bool     IsPatched   { get; set; }
    }
    public class BloxGame
    {
        [JsonPropertyName("name")]    public string? Name    { get; set; }
        [JsonPropertyName("imageUrl")]public string? ImageUrl{ get; set; }
    }
    public class BloxOwner
    {
        [JsonPropertyName("username")] public string? Username { get; set; }
    }

    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
        [DllImport("kernel32.dll")] static extern bool AllocConsole();
        [DllImport("user32.dll")]   static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        const int SW_HIDE = 0, SW_SHOW = 5;

        private readonly DispatcherTimer _notifTimer;
        private readonly HttpClient      _http;
        private bool _isExecuting = false;
        private bool _isBloxView  = false;
        private int  _bloxPage    = 1;
        private int  _bloxTotalPages = 1;
        private string _bloxQuery = "";

        public ObservableCollection<ScriptItem>     ScriptsList    { get; } = new();
        public ObservableCollection<BloxScriptItem> BloxScriptsList{ get; } = new();

        // ================================================================
        //  CONSTRUCTOR
        // ================================================================
        public MainWindow()
        {
            InitializeComponent();  // ALL named elements exist after this line

            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("User-Agent", "BrilliantExecutor/1.0");
            _http.Timeout = TimeSpan.FromSeconds(15);

            _notifTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _notifTimer.Tick += (s, e) => CloseNotification();

            // Safe to access named elements now
            ScriptHubList.ItemsSource       = ScriptsList;
            BloxScriptsListView.ItemsSource = BloxScriptsList;

            // Populate Dashboard updates list
            UpdatesList.ItemsSource = new List<string>
            {
                "Added ScriptBlox full script library integration",
                "Added Settings with General, Integrations, Startup, Appearance, Window tabs",
                "Improved attach stability and module hijacking",
                "Fixed Roblox client breaking while attaching",
                "Fixed Roblox modules failing to load while attaching",
                "Improved attaching speed and teleport handling",
                "Improved execution & loadstring pipeline",
                "Added RSB1 + BLAKE3 signed bytecode encoding",
                "Improved UNC sandbox compatibility"
            };

            LoadScriptsFolder();
            this.Loaded += MainWindow_Loaded;

            // Placeholder visibility toggle for search box
            SearchBox.TextChanged += (s, e) => SearchPlaceholder.Visibility =
                string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        // ================================================================
        //  LOADED
        // ================================================================
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load syntax highlighting
                string xshdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lua.xshd");
                if (File.Exists(xshdPath))
                {
                    using var r = new XmlTextReader(xshdPath);
                    Editor.SyntaxHighlighting = HighlightingLoader.Load(r, HighlightingManager.Instance);
                }
                else
                {
                    var luaDef = HighlightingManager.Instance.GetDefinition("Lua");
                    if (luaDef != null) Editor.SyntaxHighlighting = luaDef;
                }

                Editor.Text = "-- Brilliant Executor v1.0\n-- ScriptBlox Integration Active\n\nlocal player = game.Players.LocalPlayer\nprint(\"Hello, \" .. player.Name)";

                // Add default script tab
                AddScriptTab("Script 1");

                InitializeCore();

                // Animate in
                this.Opacity = 0;
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350))
                    { EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut } };
                this.BeginAnimation(OpacityProperty, fade);
            }
            catch (Exception ex)
            {
                // Don't show INIT ERROR — just keep NOT ATTACHED
                SetStatus("NOT ATTACHED", "#444444");
                try
                {
                    string log = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                    System.IO.File.AppendAllText(log, $"[Loaded] {ex.InnerException?.Message ?? ex.Message}\n");
                }
                catch { }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InitializeCore()
        {
            try
            {
                // Initialize() handles DLL-not-found internally and sets IsDllAvailable.
                // If DLL is absent, just show NOT ATTACHED — no alarming message.
                bool ok = RblxCore.Initialize();

                if (!RblxCore.IsDllAvailable())
                {
                    SetStatus("NOT ATTACHED", "#444444");
                    return;
                }

                SetStatus(ok ? "NOT ATTACHED" : "SYSCALL FAILED",
                          ok ? "#444444" : "#CC3333");
            }
            catch (Exception ex)
            {
                SetStatus("NOT ATTACHED", "#444444");
                // Log silently — don't popup on missing DLL
                try
                {
                    string log = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                    System.IO.File.AppendAllText(log, $"[InitializeCore] {ex.Message}\n");
                }
                catch { }
            }
        }

        private void SetStatus(string text, string hex)
        {
            InjectionStatus.Text = text;
            InjectionStatus.Foreground = (Brush)new BrushConverter().ConvertFrom(hex)!;
            StatusIndicator.Fill       = (Brush)new BrushConverter().ConvertFrom(hex)!;
        }

        // ================================================================
        //  SCRIPT TABS
        // ================================================================
        private int _tabCounter = 1;
        private RadioButton? _activeTab = null;
        private readonly Dictionary<RadioButton, string> _tabContent = new();

        private void AddScriptTab(string name)
        {
            var tab = new RadioButton
            {
                Style   = (Style)FindResource("ScriptTab"),
                Content = name,
                Tag     = name,
                GroupName = "ScriptTabGroup"
            };
            tab.Checked += ScriptTab_Checked;
            TabBarPanel.Children.Add(tab);
            tab.IsChecked = true;
            _tabContent[tab] = "";
        }

        private void ScriptTab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton tab) return;
            // Save current content to old tab
            if (_activeTab != null && _activeTab != tab)
                _tabContent[_activeTab] = Editor.Text;
            _activeTab = tab;
            // Restore content from new tab
            if (_tabContent.TryGetValue(tab, out var content))
                Editor.Text = content;
        }

        private void TabClose_Click(object sender, RoutedEventArgs e)
        {
            if (TabBarPanel.Children.Count <= 1) return;
            // Find parent RadioButton
            var btn = sender as Button;
            RadioButton? target = null;
            foreach (RadioButton rb in TabBarPanel.Children)
            {
                // The close button fires from inside; find which rb contains it
                if (rb.Tag?.ToString() == btn?.Tag?.ToString()) { target = rb; break; }
            }
            // Fallback: remove last non-checked
            target ??= TabBarPanel.Children.OfType<RadioButton>()
                        .FirstOrDefault(rb => rb.IsChecked != true);
            if (target == null) return;

            _tabContent.Remove(target);
            TabBarPanel.Children.Remove(target);
            if (target == _activeTab)
            {
                _activeTab = null;
                var first = TabBarPanel.Children.OfType<RadioButton>().FirstOrDefault();
                if (first != null) first.IsChecked = true;
            }
        }

        // ================================================================
        //  LOAD SCRIPTS FOLDER
        // ================================================================
        private void LoadScriptsFolder()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                ScriptsList.Clear();
                foreach (var file in Directory.GetFiles(path, "*.*")
                    .Where(f => f.EndsWith(".lua") || f.EndsWith(".luau") || f.EndsWith(".txt")))
                {
                    ScriptsList.Add(new ScriptItem
                        { Name = Path.GetFileName(file), FullPath = file });
                }
            }
            catch (Exception ex) { App.LogException(ex, "LoadScriptsFolder"); }
        }

        // ================================================================
        //  WINDOW CHROME
        // ================================================================
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Maximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        // ================================================================
        //  PAGE NAVIGATION
        // ================================================================
        private void Tab_Dashboard(object sender, RoutedEventArgs e) { if (ViewDashboard != null) SwitchPage(ViewDashboard); }
        private void Tab_Editor(object sender, RoutedEventArgs e)     { if (ViewEditor    != null) SwitchPage(ViewEditor);    }
        private void Tab_ScriptHub(object sender, RoutedEventArgs e)  { if (ViewScriptHub != null) SwitchPage(ViewScriptHub); }
        private void Tab_Clients(object sender, RoutedEventArgs e)    { if (ViewClients   != null) SwitchPage(ViewClients);   }
        private void Tab_Settings(object sender, RoutedEventArgs e)   { if (ViewSettings  != null) SwitchPage(ViewSettings);  }

        private void SwitchPage(Grid? target)
        {
            // Guard: called during XAML init before named elements exist
            if (target == null) return;
            if (ViewEditor == null) return; // not yet initialized

            foreach (var g in new[] { ViewDashboard, ViewEditor, ViewScriptHub, ViewClients, ViewSettings })
                if (g != null) g.Visibility = Visibility.Collapsed;

            target.Visibility = Visibility.Visible;
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
            target.BeginAnimation(OpacityProperty, fade);
        }

        // ================================================================
        //  NOTIFICATION TOAST
        // ================================================================
        private void ShowNotification(string msg, bool isError = false)
        {
            NotificationText.Text = msg;
            var col = isError ? Color.FromRgb(180, 50, 50) : Color.FromRgb(80, 180, 100);
            NotificationBox.BorderBrush = new SolidColorBrush(col);
            NotificationIcon.Stroke = new SolidColorBrush(col);
            NotificationIcon.Data = isError
                ? Geometry.Parse("M 8,2 L 14,14 L 2,14 Z M 8,8 L 8,11 M 8,12 L 8,13")
                : Geometry.Parse("M 2,8 A 6,6 0 1,0 2.1,7.9 Z M 5,8 L 7,10 L 11,6");

            NotificationBox.Visibility = Visibility.Visible;
            var t = new TranslateTransform(40, 0);
            NotificationBox.RenderTransform = t;
            t.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(280))
                    { EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut } });
            NotificationBox.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)));

            _notifTimer.Stop();
            _notifTimer.Start();
        }

        private void CloseNotification()
        {
            _notifTimer.Stop();
            var t = new TranslateTransform(0, 0);
            NotificationBox.RenderTransform = t;
            t.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(0, 20, TimeSpan.FromMilliseconds(200)));
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fade.Completed += (s, e) => NotificationBox.Visibility = Visibility.Collapsed;
            NotificationBox.BeginAnimation(OpacityProperty, fade);
        }

        // ================================================================
        //  EXECUTOR ACTIONS
        // ================================================================
        private void Execute_Click(object sender, RoutedEventArgs e)
        {
            if (_isExecuting) return;
            _isExecuting = true;
            try
            {
                string script = Editor.Text;
                if (string.IsNullOrWhiteSpace(script))
                { ShowNotification("No script to execute", true); return; }

                ShowNotification("Executing script...");
                int result = RblxCore.ExecuteScript(script, script.Length);
                e.Handled = true;

                if (result == 0)
                    ShowNotification("Executed successfully!");
                else
                    ShowNotification($"Error: {RblxCore.GetLastError()}", true);
            }
            catch (Exception ex)
            { ShowNotification("Execution error", true); App.LogException(ex, "Execute_Click"); }
            finally { _isExecuting = false; }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Editor.Text = "";
            if (_activeTab != null) _tabContent[_activeTab] = "";
            ShowNotification("Editor cleared");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    InitialDirectory = dir,
                    Filter = "Lua Scripts|*.lua;*.luau;*.txt|All Files|*.*",
                    DefaultExt = "lua"
                };
                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, Editor.Text);
                    ShowNotification("Script saved!");
                    LoadScriptsFolder();
                }
            }
            catch { ShowNotification("Failed to save", true); }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Lua Scripts|*.lua;*.luau;*.txt|All Files|*.*"
                };
                if (dlg.ShowDialog() == true)
                {
                    Editor.Text = File.ReadAllText(dlg.FileName);
                    // Update tab name
                    if (_activeTab != null)
                    {
                        _activeTab.Content = Path.GetFileName(dlg.FileName);
                        _tabContent[_activeTab] = Editor.Text;
                    }
                    ShowNotification("File opened");
                }
            }
            catch { ShowNotification("Failed to open file", true); }
        }

        private void KillRoblox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var procs = Process.GetProcessesByName("RobloxPlayerBeta");
                if (procs.Length == 0) { ShowNotification("Roblox not running", true); return; }
                foreach (var p in procs) p.Kill();
                ShowNotification("Roblox terminated");
            }
            catch { ShowNotification("Kill failed", true); }
        }

        // ================================================================
        //  ATTACH
        // ================================================================
        private async void Attach_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowNotification("Attaching to Roblox...");
                await Task.Run(() =>
                {
                    uint pid = RblxCore.FindRobloxProcess();
                    if (pid == 0)
                    { Dispatcher.Invoke(() => ShowNotification("Roblox not found!", true)); return; }

                    Dispatcher.Invoke(() => ShowNotification($"Connecting to PID {pid}..."));
                    bool ok = RblxCore.Connect(pid);

                    Dispatcher.Invoke(() =>
                    {
                        if (ok)
                        {
                            ShowNotification("Attached successfully!");
                            SetStatus("STABLE", "#00AA44");
                            SetSideAttachAttached(true);
                            ClientPidText.Text = $"PID: {pid}  ·  Place: Active";
                            ClientStatusBadge.Text = "ACTIVE SESSION";
                            ClientStatusBadge.Foreground = (Brush)new BrushConverter().ConvertFrom("#00AA44")!;
                            ClientStatusDot.Fill = (Brush)new BrushConverter().ConvertFrom("#00AA44")!;
                            StartClientDataPoller(pid);
                            StartProcessMonitor(pid);
                        }
                        else
                        {
                            ShowNotification("Attach failed!", true);
                            SetStatus("FAILED", "#CC3333");
                        }
                    });
                });
            }
            catch (Exception ex)
            { ShowNotification("Attach exception", true); App.LogException(ex, "Attach_Click"); }
        }

        private void SetSideAttachAttached(bool attached)
        {
            if (attached)
            {
                SideAttachBtn.Background    = (Brush)new BrushConverter().ConvertFrom("#0F2A0F")!;
                SideAttachBtn.Foreground    = (Brush)new BrushConverter().ConvertFrom("#00FF77")!;
                SideAttachBtn.BorderBrush   = (Brush)new BrushConverter().ConvertFrom("#00AA44")!;
                SideAttachBtn.IsEnabled     = false;
            }
            else
            {
                SideAttachBtn.Background    = (Brush)new BrushConverter().ConvertFrom("#0F1F0F")!;
                SideAttachBtn.Foreground    = (Brush)new BrushConverter().ConvertFrom("#00CC66")!;
                SideAttachBtn.BorderBrush   = (Brush)new BrushConverter().ConvertFrom("#1A3A1A")!;
                SideAttachBtn.IsEnabled     = true;
            }
        }

        private void StartProcessMonitor(uint pid)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, ev) =>
            {
                try { var p = Process.GetProcessById((int)pid); if (p.HasExited) throw new Exception(); }
                catch
                {
                    timer.Stop();
                    SetStatus("NOT ATTACHED", "#444444");
                    SetSideAttachAttached(false);
                    ClientPidText.Text = "PID: —  ·  Place: —";
                    ClientStatusBadge.Text = "DISCONNECTED";
                    ClientStatusBadge.Foreground = (Brush)new BrushConverter().ConvertFrom("#444444")!;
                    ClientStatusDot.Fill = (Brush)new BrushConverter().ConvertFrom("#333333")!;
                    ClientAccountName.Text = "Not Connected";
                    ClientAvatarImage.Source = null;
                    RblxCore.Disconnect();
                }
            };
            timer.Start();
        }

        private void StartClientDataPoller(uint pid)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            int attempts = 0;
            timer.Tick += (s, ev) =>
            {
                if (++attempts > 15) { timer.Stop(); return; }
                var sb = new System.Text.StringBuilder(512);
                if (RblxCore.GetClientInfo(sb, sb.Capacity))
                {
                    var parts = sb.ToString().Split('|');
                    if (parts.Length >= 4 && parts[0] != "Unknown")
                    {
                        timer.Stop();
                        string name = parts[0], uid = parts[1], placeId = parts[3];
                        Task.Run(async () =>
                        {
                            string placeName = $"Place: {placeId}";
                            byte[]? img = null;
                            try
                            {
                                using var cl = new HttpClient();
                                cl.DefaultRequestHeaders.Add("User-Agent", "Roblox/WinInet");
                                if (placeId != "0")
                                {
                                    var jr = await cl.GetStringAsync($"https://economy.roblox.com/v2/assets/{placeId}/details");
                                    var m = System.Text.RegularExpressions.Regex.Match(jr, "\"Name\"\\s*:\\s*\"([^\"]+)\"");
                                    if (m.Success) placeName = m.Groups[1].Value;
                                }
                                var meta = await cl.GetStringAsync($"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={uid}&size=150x150&format=Png&isCircular=false");
                                var im = System.Text.RegularExpressions.Regex.Match(meta, "\"imageUrl\"\\s*:\\s*\"([^\"]+)\"");
                                if (im.Success) img = await cl.GetByteArrayAsync(im.Groups[1].Value);
                            }
                            catch { }
                            Dispatcher.Invoke(() =>
                            {
                                ClientAccountName.Text = name;
                                ClientPidText.Text = $"PID: {pid}  ·  {placeName}";
                                if (img != null)
                                {
                                    try
                                    {
                                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                                        bmp.BeginInit();
                                        bmp.StreamSource = new MemoryStream(img);
                                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                                        bmp.EndInit();
                                        ClientAvatarImage.Source = bmp;
                                    }
                                    catch { }
                                }
                            });
                        });
                    }
                }
            };
            timer.Start();
        }

        // ================================================================
        //  SCRIPTHUB — LOCAL
        // ================================================================
        private void ShowLocalScripts(object sender, RoutedEventArgs e)
        {
            _isBloxView = false;
            LocalScriptsPanel.Visibility  = Visibility.Visible;
            BloxScriptsPanel.Visibility   = Visibility.Collapsed;
            PaginationBar.Visibility      = Visibility.Collapsed;
            LoadScriptsFolder();
        }

        private void ShowBloxScripts(object sender, RoutedEventArgs e)
        {
            _isBloxView = true;
            LocalScriptsPanel.Visibility  = Visibility.Collapsed;
            BloxScriptsPanel.Visibility   = Visibility.Visible;
            PaginationBar.Visibility      = Visibility.Visible;
            if (BloxScriptsList.Count == 0)
                _ = FetchBloxScripts();
        }

        private void ExecuteScriptHub_Click(object sender, RoutedEventArgs e)
        {
            if (_isExecuting) return;
            _isExecuting = true;
            try
            {
                var btn = sender as Button;
                if (btn?.Tag != null)
                {
                    string path = btn.Tag.ToString()!;
                    if (File.Exists(path))
                    {
                        string content = File.ReadAllText(path);
                        ShowNotification($"Executing {Path.GetFileName(path)}...");
                        RblxCore.ExecuteScript(content, content.Length);
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            { ShowNotification("Script error!", true); App.LogException(ex, "ExecuteScriptHub_Click"); }
            finally { _isExecuting = false; }
        }

        private void CopyScriptHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var btn = sender as Button;
                if (btn?.Tag != null)
                {
                    string path = btn.Tag.ToString()!;
                    if (File.Exists(path)) { Clipboard.SetText(File.ReadAllText(path)); ShowNotification("Copied!"); }
                }
            }
            catch { ShowNotification("Copy failed!", true); }
        }

        private void DeleteScriptHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var btn = sender as Button;
                if (btn?.Tag != null)
                {
                    string path = btn.Tag.ToString()!;
                    if (File.Exists(path)) { File.Delete(path); ShowNotification("Deleted!"); LoadScriptsFolder(); }
                }
            }
            catch { ShowNotification("Delete failed!", true); }
        }

        // ================================================================
        //  SCRIPTBLOX API
        // ================================================================
        private void ScriptBlox_Refresh(object sender, RoutedEventArgs e)
        {
            if (_isBloxView)
                _ = FetchBloxScripts(_bloxQuery, _bloxPage);
            else
                LoadScriptsFolder();
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = SearchBox.Text;
            _bloxQuery = q;
            if (!_isBloxView) return;
            await Task.Delay(450);
            if (_bloxQuery != SearchBox.Text) return;
            _bloxPage = 1;
            await FetchBloxScripts(_bloxQuery, 1);
        }

        private async Task FetchBloxScripts(string search = "", int page = 1)
        {
            try
            {
                BloxLoadingText.Visibility = Visibility.Visible;
                BloxLoadingText.Text = "Loading scripts from ScriptBlox...";

                // ScriptBlox v2 API endpoint
                string url = string.IsNullOrWhiteSpace(search)
                    ? $"https://scriptblox.com/api/script/fetch?page={page}&max=20"
                    : $"https://scriptblox.com/api/script/search?q={Uri.EscapeDataString(search)}&page={page}&max=20";

                var json = await _http.GetStringAsync(url);

                // Try to parse with flexible options
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // ScriptBlox returns: { "result": { "scripts": { "data": [...], "totalPages": N } } }
                BloxApiResponse? resp = null;
                try { resp = JsonSerializer.Deserialize<BloxApiResponse>(json, opts); }
                catch { }

                BloxScriptsList.Clear();
                int total = 1;

                if (resp?.Result?.Scripts?.Data != null)
                {
                    total = resp.Result.Scripts.TotalPages;
                    foreach (var s in resp.Result.Scripts.Data)
                    {
                        BloxScriptsList.Add(new BloxScriptItem
                        {
                            Title       = s.Title       ?? "Untitled",
                            Description = s.Game?.Name  ?? s.Owner?.Username ?? "No description",
                            ScriptId    = s.Id          ?? "",
                            Views       = $"{s.Views:N0}",
                            Author      = s.Owner?.Username ?? "Unknown",
                            RawScript   = s.Script ?? ""
                        });
                    }
                }

                _bloxTotalPages = Math.Max(1, total);
                PageLabel.Text = $"Page {page} of {_bloxTotalPages}";
                ShowNotification($"Loaded {BloxScriptsList.Count} scripts");
            }
            catch (Exception ex)
            {
                ShowNotification($"ScriptBlox error: {ex.Message}", true);
            }
            finally
            {
                BloxLoadingText.Visibility = Visibility.Collapsed;
            }
        }

        private async void BloxLoad_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag == null) return;
            string id = btn.Tag.ToString()!;
            await LoadBloxById(id, false);
        }

        private async void BloxCopy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag == null) return;
            string id = btn.Tag.ToString()!;
            await LoadBloxById(id, true);
        }

        private async Task LoadBloxById(string id, bool copyOnly)
        {
            // Check cached
            var cached = BloxScriptsList.FirstOrDefault(s => s.ScriptId == id);
            if (cached != null && !string.IsNullOrEmpty(cached.RawScript))
            {
                if (copyOnly) { Clipboard.SetText(cached.RawScript); ShowNotification("Copied!"); }
                else { Editor.Text = cached.RawScript; SwitchPage(ViewEditor); ShowNotification($"Loaded: {cached.Title}"); }
                return;
            }

            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = "Fetching script...";

                string url = $"https://scriptblox.com/api/script/{id}";
                var json = await _http.GetStringAsync(url);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var resp = JsonSerializer.Deserialize<BloxApiResponse>(json, opts);
                var raw  = resp?.Result?.Scripts?.Data?.FirstOrDefault()?.Script ?? "";

                if (string.IsNullOrEmpty(raw))
                {
                    // Try alternate: raw script endpoint
                    try { raw = await _http.GetStringAsync($"https://rawscripts.net/raw/{id}"); } catch { }
                }

                if (cached != null) cached.RawScript = raw;

                if (copyOnly) { Clipboard.SetText(raw); ShowNotification("Copied!"); }
                else { Editor.Text = raw; SwitchPage(ViewEditor); ShowNotification("Loaded into editor"); }
            }
            catch (Exception ex)
            { ShowNotification($"Load error: {ex.Message}", true); }
            finally { LoadingOverlay.Visibility = Visibility.Collapsed; }
        }

        private void BloxPrevPage(object sender, RoutedEventArgs e)
        {
            if (_bloxPage <= 1) return;
            _bloxPage--;
            _ = FetchBloxScripts(_bloxQuery, _bloxPage);
        }

        private void BloxNextPage(object sender, RoutedEventArgs e)
        {
            if (_bloxPage >= _bloxTotalPages) return;
            _bloxPage++;
            _ = FetchBloxScripts(_bloxQuery, _bloxPage);
        }

        // ================================================================
        //  SETTINGS — SUB-TABS
        // ================================================================
        private void HideAllSettingsPanels()
        {
            if (SettingsGeneral == null) return; // guard against init-time calls
            SettingsGeneral.Visibility      = Visibility.Collapsed;
            SettingsIntegrations.Visibility = Visibility.Collapsed;
            SettingsStartup.Visibility      = Visibility.Collapsed;
            SettingsAppearance.Visibility   = Visibility.Collapsed;
            SettingsWindow.Visibility       = Visibility.Collapsed;
        }
        private void SettingsTab_General(object sender, RoutedEventArgs e)
            { HideAllSettingsPanels(); if (SettingsGeneral != null) SettingsGeneral.Visibility = Visibility.Visible; }
        private void SettingsTab_Integrations(object sender, RoutedEventArgs e)
            { HideAllSettingsPanels(); if (SettingsIntegrations != null) SettingsIntegrations.Visibility = Visibility.Visible; }
        private void SettingsTab_Startup(object sender, RoutedEventArgs e)
            { HideAllSettingsPanels(); if (SettingsStartup != null) SettingsStartup.Visibility = Visibility.Visible; }
        private void SettingsTab_Appearance(object sender, RoutedEventArgs e)
            { HideAllSettingsPanels(); if (SettingsAppearance != null) SettingsAppearance.Visibility = Visibility.Visible; }
        private void SettingsTab_Window(object sender, RoutedEventArgs e)
            { HideAllSettingsPanels(); if (SettingsWindow != null) SettingsWindow.Visibility = Visibility.Visible; }

        // ================================================================
        //  SETTINGS — GENERAL ACTIONS
        // ================================================================
        private void OpenWorkspace_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start("explorer.exe", path);
            ShowNotification("Workspace folder opened");
        }

        private void OpenAutoexec_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autoexec");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start("explorer.exe", path);
            ShowNotification("Autoexec folder opened");
        }

        private void RestartApp_Click(object sender, RoutedEventArgs e)
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (exe != null) { Process.Start(exe); Close(); }
        }

        private void AlwaysOnTop_Checked(object sender, RoutedEventArgs e) => Topmost = true;
        private void AlwaysOnTop_Unchecked(object sender, RoutedEventArgs e) => Topmost = false;

        private void SetDefaultPage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                ShowNotification($"Default page set to {btn.Tag}");
        }

        // ================================================================
        //  SETTINGS — APPEARANCE
        // ================================================================
        private void WordWrap_Checked(object sender, RoutedEventArgs e)
            => Editor.WordWrap = true;
        private void WordWrap_Unchecked(object sender, RoutedEventArgs e)
            => Editor.WordWrap = false;
        private void LineNumbers_Checked(object sender, RoutedEventArgs e)
            => Editor.ShowLineNumbers = true;
        private void LineNumbers_Unchecked(object sender, RoutedEventArgs e)
            => Editor.ShowLineNumbers = false;

        // ================================================================
        //  SETTINGS — CONSOLE
        // ================================================================
        private void ToggleConsole_Checked(object sender, RoutedEventArgs e)
        {
            var h = GetConsoleWindow();
            if (h == IntPtr.Zero) { AllocConsole(); RblxCore.RedirConsole(); h = GetConsoleWindow(); }
            if (h != IntPtr.Zero) ShowWindow(h, SW_SHOW);
        }
        private void ToggleConsole_Unchecked(object sender, RoutedEventArgs e)
        {
            var h = GetConsoleWindow();
            if (h != IntPtr.Zero) ShowWindow(h, SW_HIDE);
        }

        // ================================================================
        //  DASHBOARD QUICK ACTIONS
        // ================================================================
        private void OpenWebsite_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://github.com") { UseShellExecute = true }); }
            catch { ShowNotification("Could not open browser", true); }
        }

        private void DownloadLatest_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://github.com") { UseShellExecute = true }); }
            catch { ShowNotification("Could not open browser", true); }
        }

        private void JoinDiscord_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://discord.com") { UseShellExecute = true }); }
            catch { ShowNotification("Could not open browser", true); }
        }
    }
}
