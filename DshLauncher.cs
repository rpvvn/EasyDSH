using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DshLauncherWpf
{
    public class MainWindow : Window
    {
        private const string WebUrl = "http://127.0.0.1:3080";
        private const string PackageName = "@deepseek-ai/dsh";
        internal const string LauncherVersion = "v2.2.2";

        // 颜色
        private static readonly Color PanelBackColor = Color.FromRgb(246, 248, 250);
        private static readonly Brush PanelBackBrush = new SolidColorBrush(PanelBackColor);
        private static readonly Brush DotGreenBrush = new SolidColorBrush(Color.FromRgb(46, 204, 113));
        private static readonly Brush DotRedBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54));
        private static readonly Brush DotGrayBrush = new SolidColorBrush(Color.FromRgb(158, 158, 158));

        // 状态卡片引用
        private Ellipse _dotNode;
        private Ellipse _dotNpm;
        private Ellipse _dotDsh;
        private Ellipse _dotSvc;
        private TextBlock _txtNode;
        private TextBlock _txtNpm;
        private TextBlock _txtDsh;
        private TextBlock _txtSvc;

        private TextBox _txtLog;
        private Button _btnStart;
        private Button _btnRestart;
        private Button _btnBrowser;
        private Button _btnInstall;
        private Button _btnStop;
        private Button _btnMirror;
        private Button _btnCheck;
        private Button _btnProxy;
        private Button _btnRestore;
        private Button _btnAbout;
        private TextBlock _lblStatus;

        private Process _dshProcess;

        private bool _nodeOk;
        private bool _npmOk;
        private bool _dshGlobal;
        private bool _dshCached;
        private bool _serviceRunning;
        private bool _restarting;
        private string _nodeVersion = "";
        private string _npmVersion = "";
        private string _dshVersion = "";
        private string _npmPrefix = "";
        private string _npmCache = "";
        private string _dshCmd = "";

        private static readonly Regex AnsiRegex = new Regex("\x1b\\[[0-9;?]*[ -/]*[@-~]");

        private const string ProxyProfileCode =
            "# ===== Local proxy helper: ep / dp / pstat =====\r\n" +
            "$script:proxyAddr = \"http://127.0.0.1:7890\"\r\n" +
            "$script:noProxy   = \"localhost,127.0.0.1\"\r\n" +
            "$script:proxyPort = 7890\r\n" +
            "\r\n" +
            "function pstat {\r\n" +
            "    $envP = if ($env:HTTP_PROXY) { $env:HTTP_PROXY } else { \"null\" }\r\n" +
            "    $gitP = git config --global --get http.proxy\r\n" +
            "    if (-not $gitP) { $gitP = \"null\" }\r\n" +
            "    Write-Host (\"Env: {0} | Git: {1}\" -f $envP, $gitP)\r\n" +
            "    $c = New-Object Net.Sockets.TCPClient\r\n" +
            "    try { $c.Connect(\"127.0.0.1\", $script:proxyPort); Write-Host \"Port $($script:proxyPort): ok\" }\r\n" +
            "    catch { Write-Host \"Port $($script:proxyPort): fail\" }\r\n" +
            "    finally { $c.Close() }\r\n" +
            "}\r\n" +
            "\r\n" +
            "function ep {\r\n" +
            "    $env:HTTP_PROXY  = $script:proxyAddr\r\n" +
            "    $env:HTTPS_PROXY = $script:proxyAddr\r\n" +
            "    $env:NO_PROXY    = $script:noProxy\r\n" +
            "    git config --global http.proxy  $script:proxyAddr\r\n" +
            "    git config --global https.proxy $script:proxyAddr\r\n" +
            "    pstat\r\n" +
            "}\r\n" +
            "\r\n" +
            "function dp {\r\n" +
            "    Remove-Item Env:HTTP_PROXY, Env:HTTPS_PROXY, Env:NO_PROXY -ErrorAction SilentlyContinue\r\n" +
            "    git config --global --unset http.proxy  2>$null\r\n" +
            "    git config --global --unset https.proxy 2>$null\r\n" +
            "    pstat\r\n" +
            "}\r\n" +
            "\r\n" +
            "Write-Host \"ep=enable | dp=disable | pstat=status\"";

        // 服务占用冲突识别：运行时捕捉 DSH 原始报错并重新提示
        private static readonly Regex CollisionOriginalRegex = new Regex(
            @"service\s+""(?<service>[^""]+)""\s+has\s+been\s+registered\s+at\s+<(?<owner>[^>]+)>",
            RegexOptions.Compiled);
        private static readonly Regex CollisionImprovedRegex = new Regex(
            @"service\s+""(?<service>[^""]+)""\s+is\s+already\s+provided\s+by\s+""(?<owner>[^""]+)""",
            RegexOptions.Compiled);
        private static readonly Regex LoaderEntryRegex = new Regex(
            @"failed\s+to\s+apply\s+loader\s+entry\s+(?<id>[^\s(]+)(?:\s*\((?<name>[^)]*)\))?",
            RegexOptions.Compiled);

        private readonly object _collisionLock = new object();
        private CollisionInfo _collision;
        private int _collisionPrompted;

        public MainWindow()
        {
            BuildUi();
        }

        // ------------------------------------------------------------------
        // UI 构建
        // ------------------------------------------------------------------
        private void BuildUi()
        {
            Title = "DSH 一键启动器";
            Width = 700;
            Height = 440;
            ResizeMode = ResizeMode.CanMinimize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = PanelBackBrush;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 13;

            SetWindowIcon();

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(84) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });

            // 标题栏：左侧标题 + 右侧环境/关于按钮
            var titleBar = new DockPanel { Margin = new Thickness(12, 6, 12, 4) };

            _btnRestore = MakeSmallButton("一键还原", 80, Color.FromRgb(198, 60, 60));
            _btnAbout = MakeSmallButton("什么玩意", 64, Color.FromRgb(142, 68, 173));
            _btnCheck = MakeSmallButton("调查梁子", 80, Color.FromRgb(0, 122, 204));
            _btnProxy = MakeSmallButton("插件下载慢", 80, Color.FromRgb(0, 150, 90));
            _btnMirror = MakeSmallButton("NPM镜像源", 80, Color.FromRgb(230, 145, 56));
            _btnRestore.VerticalAlignment = VerticalAlignment.Center;
            _btnAbout.VerticalAlignment = VerticalAlignment.Center;
            _btnMirror.VerticalAlignment = VerticalAlignment.Center;
            _btnCheck.VerticalAlignment = VerticalAlignment.Center;
            _btnProxy.VerticalAlignment = VerticalAlignment.Center;
            _btnMirror.ToolTip = "下载缓慢？切换国内 npm 镜像源";
            _btnCheck.ToolTip = "狠狠调查大肥鱼有没有更新";
            _btnProxy.ToolTip = "写入 PowerShell 代理函数（ep/dp）";
            _btnRestore.ToolTip = "卸载 / 一键还原 DSH";
            _btnAbout.ToolTip = "点进来看看这是什么东东";
            bool aboutLongPressed = false;
            DispatcherTimer aboutHoldTimer = null;
            _btnAbout.PreviewMouseDown += delegate
            {
                aboutLongPressed = false;
                if (aboutHoldTimer == null)
                {
                    aboutHoldTimer = new DispatcherTimer();
                    aboutHoldTimer.Interval = TimeSpan.FromSeconds(1.9);
                    aboutHoldTimer.Tick += delegate
                    {
                        aboutHoldTimer.Stop();
                        aboutLongPressed = true;
                        var dlg = new NodeJsInstallWindow();
                        dlg.Owner = this;
                        dlg.ShowDialog();
                    };
                }
                aboutHoldTimer.Start();
            };
            _btnAbout.PreviewMouseUp += delegate
            {
                if (aboutHoldTimer != null) aboutHoldTimer.Stop();
            };
            _btnAbout.Click += delegate (object s, RoutedEventArgs e)
            {
                if (aboutLongPressed)
                {
                    aboutLongPressed = false;
                    return;
                }
                BtnAbout_Click(s, e);
            };
            _btnRestore.Click += BtnRestore_Click;
            _btnMirror.Click += BtnMirror_Click;
            bool checkLongPressed = false;
            DispatcherTimer checkHoldTimer = null;
            _btnCheck.PreviewMouseDown += delegate
            {
                checkLongPressed = false;
                if (checkHoldTimer == null)
                {
                    checkHoldTimer = new DispatcherTimer();
                    checkHoldTimer.Interval = TimeSpan.FromSeconds(1.8);
                    checkHoldTimer.Tick += delegate
                    {
                        checkHoldTimer.Stop();
                        checkLongPressed = true;
                        ShowCollisionDebugDialog();
                    };
                }
                checkHoldTimer.Start();
            };
            _btnCheck.PreviewMouseUp += delegate
            {
                if (checkHoldTimer != null) checkHoldTimer.Stop();
            };
            _btnCheck.Click += delegate (object s, RoutedEventArgs e)
            {
                if (checkLongPressed)
                {
                    checkLongPressed = false;
                    return;
                }
                BtnCheckUpdate_Click(s, e);
            };
            _btnProxy.Click += BtnProxy_Click;
            DockPanel.SetDock(_btnRestore, Dock.Right);
            DockPanel.SetDock(_btnAbout, Dock.Right);
            DockPanel.SetDock(_btnCheck, Dock.Right);
            DockPanel.SetDock(_btnProxy, Dock.Right);
            DockPanel.SetDock(_btnMirror, Dock.Right);
            titleBar.Children.Add(_btnRestore);
            titleBar.Children.Add(_btnAbout);
            titleBar.Children.Add(_btnProxy);
            titleBar.Children.Add(_btnCheck);
            titleBar.Children.Add(_btnMirror);

            var title = new TextBlock
            {
                Text = "Vibe Coding 轻而易举啊！",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 17,
                FontWeight = FontWeights.Bold
            };
            titleBar.Children.Add(title);

            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            // 环境状态区（2x2 卡片）
            var envGrid = new UniformGrid
            {
                Columns = 2,
                Rows = 2,
                Margin = new Thickness(10, 8, 10, 8)
            };
            _dotNode = NewDot();
            _txtNode = NewCardText();
            envGrid.Children.Add(MakeCard(_dotNode, _txtNode));
            _dotDsh = NewDot();
            _txtDsh = NewCardText();
            envGrid.Children.Add(MakeCard(_dotDsh, _txtDsh));
            _dotNpm = NewDot();
            _txtNpm = NewCardText();
            envGrid.Children.Add(MakeCard(_dotNpm, _txtNpm));
            _dotSvc = NewDot();
            _txtSvc = NewCardText();
            envGrid.Children.Add(MakeCard(_dotSvc, _txtSvc));
            Grid.SetRow(envGrid, 1);
            root.Children.Add(envGrid);

            // 日志
            _txtLog = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap,
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 32)),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 224, 230)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4)
            };
            Grid.SetRow(_txtLog, 2);
            root.Children.Add(_txtLog);

            // 状态栏
            _lblStatus = new TextBlock
            {
                Text = "就绪",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 12, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(80, 84, 92))
            };
            Grid.SetRow(_lblStatus, 3);
            root.Children.Add(_lblStatus);

            // 按钮区
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _btnStart = MakeButton("一键启动", 112, Color.FromRgb(0, 122, 204));
            _btnRestart = MakeButton("重启服务", 96, Color.FromRgb(230, 145, 56));
            _btnBrowser = MakeButton("打开浏览器", 96, Color.FromRgb(88, 88, 96));
            _btnInstall = MakeButton("捕获大肥鱼", 96, Color.FromRgb(0, 150, 90));
            _btnStop = MakeButton("大肥鱼停下！", 96, Color.FromRgb(198, 60, 60));
            _btnStart.Click += BtnStart_Click;
            _btnRestart.Click += BtnRestart_Click;
            _btnBrowser.Click += BtnBrowser_Click;
            _btnInstall.Click += BtnInstall_Click;
            _btnStop.Click += BtnStop_Click;
            btnPanel.Children.Add(_btnStart);
            btnPanel.Children.Add(_btnRestart);
            btnPanel.Children.Add(_btnBrowser);
            btnPanel.Children.Add(_btnInstall);
            btnPanel.Children.Add(_btnStop);
            Grid.SetRow(btnPanel, 4);
            root.Children.Add(btnPanel);

            Content = root;

            _btnStop.IsEnabled = false;

            Loaded += delegate
            {
                AppendLog("启动器 " + LauncherVersion + "版 已就绪，正在巡视领地...");
                Thread t = new Thread(DetectEnvironment);
                t.IsBackground = true;
                t.Start();
            };
        }

        private void SetWindowIcon()
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                {
                    if (icon != null)
                    {
                        IntPtr hicon = icon.Handle;
                        Icon = Imaging.CreateBitmapSourceFromHIcon(
                            hicon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    }
                }
            }
            catch { }
        }

        private static Ellipse NewDot()
        {
            return new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = DotGrayBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static TextBlock NewCardText()
        {
            return new TextBlock
            {
                Text = "检测中...",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(40, 44, 52))
            };
        }

        private static Border MakeCard(Ellipse dot, TextBlock text)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(dot);
            sp.Children.Add(text);

            var border = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(6),
                BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 230)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 0, 8, 0),
                Margin = new Thickness(4, 3, 4, 3),
                Child = sp
            };
            return border;
        }

        internal static Button MakeButton(string text, double width, Color normal)
        {
            return new Button
            {
                Content = text,
                Width = width,
                Height = 34,
                Margin = new Thickness(0, 0, 6, 0),
                Foreground = Brushes.White,
                FontSize = 13,
                Style = CreateButtonStyle(normal, Lighten(normal, 0.18), Lighten(normal, 0.55))
            };
        }

        private static Button MakeSmallButton(string text, double width, Color normal)
        {
            var b = MakeButton(text, width, normal);
            b.Height = 26;
            b.FontSize = 12;
            return b;
        }

        private static Style CreateButtonStyle(Color normal, Color hover, Color disabled)
        {
            var style = new Style(typeof(Button));

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "bd";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(normal));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hover), "bd"));
            template.Triggers.Add(hoverTrigger);

            var pressTrigger = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Lighten(normal, 0.28)), "bd"));
            template.Triggers.Add(pressTrigger);

            var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabledTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(disabled), "bd"));
            template.Triggers.Add(disabledTrigger);

            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            style.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(Button.FocusVisualStyleProperty, null));
            return style;
        }

        private static Color Lighten(Color c, double amount)
        {
            return Color.FromRgb(
                (byte)(c.R + (255 - c.R) * amount),
                (byte)(c.G + (255 - c.G) * amount),
                (byte)(c.B + (255 - c.B) * amount));
        }

        // ------------------------------------------------------------------
        // 环境状态刷新
        // ------------------------------------------------------------------
        private void RefreshEnvCards()
        {
            string nodeVal = _nodeOk ? _nodeVersion : "你没下呢老铁";
            string npmVal = _npmOk ? _npmVersion : "你没下呢老铁";
            string dshVal = _dshGlobal ? "全局已装" : (_dshCached ? "已缓存" : "未安装");
            string svcVal = _serviceRunning ? "蓝色大肥鱼正在享用你的Token" : "蓝色大肥鱼已休息";

            _dotNode.Fill = _nodeOk ? DotGreenBrush : DotRedBrush;
            _txtNode.Text = "Node.js: " + nodeVal;
            _dotNpm.Fill = _npmOk ? DotGreenBrush : DotRedBrush;
            _txtNpm.Text = "npm: " + npmVal;
            _dotDsh.Fill = (_dshGlobal || _dshCached) ? DotGreenBrush : DotRedBrush;
            _txtDsh.Text = "DSH: " + dshVal;
            _dotSvc.Fill = _serviceRunning ? DotGreenBrush : DotGrayBrush;
            _txtSvc.Text = "状态: " + svcVal;
        }

        // ------------------------------------------------------------------
        // 环境检测
        // ------------------------------------------------------------------
        private void DetectEnvironment()
        {
            string nodeOut, npmOut;
            _nodeOk = RunSync("cmd.exe", "/c node -v", out nodeOut) == 0;
            _npmOk = RunSync("cmd.exe", "/c npm -v", out npmOut) == 0;
            _nodeVersion = _nodeOk ? nodeOut.Trim() : "";
            _npmVersion = _npmOk ? npmOut.Trim() : "";

            _npmPrefix = "";
            _npmCache = "";
            _dshCmd = "";
            _dshGlobal = false;
            _dshCached = false;

            if (_npmOk)
            {
                string prefix, cache;
                if (RunSync("cmd.exe", "/c npm config get prefix", out prefix) == 0)
                {
                    _npmPrefix = prefix.Trim().Trim('"');
                    if (_npmPrefix.Length > 0)
                    {
                        _dshCmd = System.IO.Path.Combine(_npmPrefix, "dsh.cmd");
                        _dshGlobal = File.Exists(_dshCmd);
                    }
                }
                if (RunSync("cmd.exe", "/c npm config get cache", out cache) == 0)
                {
                    _npmCache = cache.Trim().Trim('"');
                }
            }

            _dshCached = HasNpxCache();

            // 检测 DSH 版本（dsh --version，或 npx 兜底）
            _dshVersion = "";
            string dshVerOut;
            if (_dshGlobal)
            {
                if (RunSync("cmd.exe", "/c \"" + _dshCmd + "\" --version", out dshVerOut) == 0)
                    _dshVersion = dshVerOut.Trim();
            }
            else if (_npmOk)
            {
                if (RunSync("cmd.exe", "/c npx --yes " + PackageName + " --version", out dshVerOut) == 0)
                    _dshVersion = dshVerOut.Trim();
            }

            _serviceRunning = IsPortOpen(3080);

            RunOnUi(delegate
            {
                RefreshEnvCards();
                _btnInstall.IsEnabled = _npmOk && !_dshGlobal;
                _btnInstall.Content = _dshGlobal ? "大肥鱼已安装" : "安装大肥鱼";
                _btnStop.IsEnabled = _serviceRunning;

                AppendLog("调查完毕：Node=" + (_nodeOk ? "OK" : "缺失") +
                          "，npm=" + (_npmOk ? "OK" : "缺失") +
                          "，DSH=" + (_dshGlobal ? "全局" : (_dshCached ? "缓存" : "未安装")) +
                          "，DSH版本=" + (string.IsNullOrEmpty(_dshVersion) ? "未知" : _dshVersion) +
                          "，蓝色大肥鱼=" + (_serviceRunning ? "已在工位" : "没上岗"));

                if (_serviceRunning)
                    SetStatus("蓝色大肥鱼正在浏览器等待Token投喂");
                else if (_nodeOk && _npmOk)
                    SetStatus("报告长官！蓝色大肥鱼已就绪，请求启动");
                else
                    SetStatus("请先安装 Node.js");

                if (!_nodeOk)
                {
                    var dlg = new NodeJsInstallWindow();
                    dlg.Owner = this;
                    dlg.ShowDialog();
                }
            });
        }

        // ------------------------------------------------------------------
        // 按钮事件
        // ------------------------------------------------------------------
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_dshProcess != null && !_dshProcess.HasExited)
            {
                OpenBrowser();
                AppendLog("蓝色大肥鱼已在工位，已经传送工位。");
                return;
            }
            if (_serviceRunning)
            {
                OpenBrowser();
                AppendLog("蓝色大肥鱼已在工位，已经传送工位。");
                return;
            }
            if (!_nodeOk || !_npmOk)
            {
                MessageBox.Show("未检测到 Node.js / npm。\r\n\r\n请先安装 Node.js（自带 npm）后重试。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Thread t = new Thread(StartDshThread);
            t.IsBackground = true;
            t.Start();
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            if (_restarting)
            {
                AppendLog("正在重启中，请稍候...");
                return;
            }
            if (!_nodeOk || !_npmOk)
            {
                MessageBox.Show("未检测到 Node.js / npm。\r\n\r\n请先安装 Node.js（自带 npm）后重试。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _restarting = true;
            Thread t = new Thread(RestartDshThread);
            t.IsBackground = true;
            t.Start();
        }

        private void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_dshGlobal)
            {
                AppendLog("DSH 已全局加载，无需重复安装。");
                return;
            }
            if (!_npmOk)
            {
                MessageBox.Show("未检测到 npm，请先安装 Node.js。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Thread t = new Thread(InstallDshThread);
            t.IsBackground = true;
            t.Start();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            List<int> pids = CollectDshPids();
            if (pids.Count == 0)
            {
                AppendLog("蓝色大肥鱼 服务 失踪了！！！");
                SetStatus("蓝色大肥鱼 进程 失踪了！！！");
                return;
            }

            AppendLog("扫描到 " + pids.Count + " 条 蓝色大肥鱼 相关进程，开始强制停止...");
            ForceStopAll(pids);

            RunOnUi(delegate
            {
                _dshProcess = null;
                _serviceRunning = false;
                _btnStart.IsEnabled = true;
                _btnStop.IsEnabled = false;
                RefreshEnvCards();
            });
            SetStatus("蓝色大肥鱼已放生大自然");
        }

        private void BtnBrowser_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser();
        }

        private void BtnMirror_Click(object sender, RoutedEventArgs e)
        {
            Thread t = new Thread(delegate ()
            {
                string outp;
                int code = RunSync("cmd.exe", "/c npm config set registry https://registry.npmmirror.com", out outp);
                RunOnUi(delegate
                {
                    if (code == 0)
                    {
                        ShowInfo("提示", "已切换至国内镜像源：\r\nhttps://registry.npmmirror.com");
                        AppendLog("npm 镜像源已切换为 https://registry.npmmirror.com");
                    }
                    else
                    {
                        ShowInfo("错误", "切换镜像源失败：" + outp);
                    }
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            Thread t = new Thread(delegate ()
            {
                string outp;
                int code = RunSync("cmd.exe", "/c npm view @deepseek-ai/dsh version", out outp);
                string latest = code == 0 ? outp.Trim() : "";
                RunOnUi(delegate
                {
                    if (string.IsNullOrEmpty(latest))
                        ShowInfo("检查更新", "获取最新版本失败，没网络了哥。");
                    else if (string.IsNullOrEmpty(_dshVersion))
                        ShowInfo("检查更新", "最新版本：" + latest + "\r\n当前版本：未知");
                    else if (string.Equals(latest, _dshVersion, StringComparison.OrdinalIgnoreCase))
                        ShowInfo("检查更新", "已是最新版本（" + latest + "）");
                    else
                    {
                        var dlg = new UpdatePromptWindow(latest, _dshVersion);
                        dlg.Owner = this;
                        dlg.ShowDialog();
                    }
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void BtnProxy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string profile = GetPowerShellProfilePath();
                string dir = System.IO.Path.GetDirectoryName(profile);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(profile) && File.ReadAllText(profile).Contains("function ep"))
                {
                    ShowInfo("本地代理", "本地代理函数（ep/dp）已存在于配置文件中。\r\n\r\n" +
                        "使用方法：终端输入 ep 开启代理、dp 关闭代理。\r\n\r\n" +
                        "删除方法：PowerShell 输入 notepad $PROFILE，删除对应的 ep/dp 函数后保存。");
                    return;
                }

                File.AppendAllText(profile, (File.Exists(profile) ? "\r\n" : "") + ProxyProfileCode + "\r\n", Encoding.UTF8);

                ShowInfo("本地代理", "已写入本地代理配置：\r\n" + profile + "\r\n\r\n" +
                    "使用方法（重新打开终端后生效）：\r\n" +
                    "  · 输入 ep 开启代理（127.0.0.1:7890）\r\n" +
                    "  · 输入 dp 关闭代理\r\n\r\n" +
                    "删除方法：\r\n" +
                    "  PowerShell 输入 notepad $PROFILE，删除对应的 ep/dp 函数代码后保存即可。");
            }
            catch (Exception ex)
            {
                ShowInfo("错误", "写入失败：" + ex.Message);
            }
        }

        private static string GetPowerShellProfilePath()
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return System.IO.Path.Combine(docs, "WindowsPowerShell", "Microsoft.PowerShell_profile.ps1");
        }

        private void ShowInfo(string title, string message)
        {
            var dlg = new InfoWindow(title, message);
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        private void ShowCollisionDebugDialog()
        {
            var info = new CollisionInfo
            {
                Service = "dsh:example.service",
                OwnerId = "owner-entry",
                OwnerName = "@test/owner-plugin",
                ClaimantId = "claimant-entry",
                ClaimantName = "@test/claimant-plugin"
            };
            string msg = "服务名 \"dsh:example.service\" 被两个插件重复注册：\r\n\r\n" +
                "  · 后注册占用：@test/owner-plugin\r\n" +
                "  · 本地冲突插件：@test/claimant-plugin\r\n\r\n" +
                "请选择卸载其中一方（卸载后需重启服务）：\r\n" +
                "  · 「卸载占用方」将移除 @test/owner-plugin\r\n" +
                "  · 「卸载冲突方」将移除 @test/claimant-plugin";
            ShowCollisionDialog(info, msg, "@test/owner-plugin");
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new UninstallWindow();
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        internal void ShowConfirm(string title, string message, Action onConfirm)
        {
            var dlg = new ConfirmWindow(title, message, onConfirm);
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        public void StartDshUninstall(bool full)
        {
            Thread t = new Thread(() => UninstallDshThread(full));
            t.IsBackground = true;
            t.Start();
        }

        private void UninstallDshThread(bool full)
        {
            AppendLog("开始卸载 DSH" + (full ? "（完全卸载）" : "（仅卸载本体）") + " ...");
            string outp;
            int code = RunSync("cmd.exe", "/c npm uninstall -g @deepseek-ai/dsh", out outp);
            if (!string.IsNullOrEmpty(outp))
            {
                foreach (string line in outp.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    AppendLog(line);
            }
            AppendLog("npm 全局卸载" + (code == 0 ? "完成" : "退出码 " + code));

            AppendLog("清理 npm/npx 缓存中的 DSH...");
            CleanNpxDshCache();

            if (full)
            {
                string dshDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");
                AppendLog("删除用户配置目录：" + dshDir);
                try
                {
                    if (Directory.Exists(dshDir))
                    {
                        Directory.Delete(dshDir, true);
                        AppendLog("已删除 .dsh 配置目录。");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("删除 .dsh 配置目录失败：" + ex.Message);
                }
            }

            AppendLog("卸载流程" + (full ? "（完全）" : "完成") + "，正在重新检测环境...");
            Thread t = new Thread(DetectEnvironment);
            t.IsBackground = true;
            t.Start();
        }

        private void CleanNpxDshCache()
        {
            try
            {
                string root = _npmCache;
                if (string.IsNullOrEmpty(root))
                    root = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "npm-cache");
                string npx = System.IO.Path.Combine(root, "_npx");
                if (!Directory.Exists(npx)) return;
                foreach (string dir in Directory.GetDirectories(npx))
                {
                    string dshDir = System.IO.Path.Combine(dir, "node_modules", "@deepseek-ai", "dsh");
                    if (Directory.Exists(dshDir))
                    {
                        try
                        {
                            Directory.Delete(dshDir, true);
                            AppendLog("已清理缓存：" + dshDir);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public void StartDshUpdate()
        {
            Thread t = new Thread(UpdateDshThread);
            t.IsBackground = true;
            t.Start();
        }

        private void UpdateDshThread()
        {
            AppendLog("开始更新 DSH：npm install -g @deepseek-ai/dsh@latest");
            var psi = BuildPsi("cmd.exe", "/c npm install -g @deepseek-ai/dsh@latest");
            try
            {
                var p = new Process { StartInfo = psi };
                p.Start();
                ReadLinesUtf8Async(p.StandardOutput.BaseStream, delegate (string line) { AppendLog(CleanAnsi(line)); });
                ReadLinesUtf8Async(p.StandardError.BaseStream, delegate (string line) { AppendLog(CleanAnsi(line)); });
                p.WaitForExit();
                int code = p.ExitCode;
                if (code == 0)
                {
                    AppendLog("DSH 更新完成，正在重新检测环境...");
                    Thread t = new Thread(DetectEnvironment);
                    t.IsBackground = true;
                    t.Start();
                }
                else
                {
                    AppendLog("DSH 更新失败，退出码 " + code + "，请查看上方日志。");
                }
            }
            catch (Exception ex)
            {
                AppendLog("DSH 更新出错：" + ex.Message);
            }
        }

        public void StartNodeJsInstall(string versionArg)
        {
            Thread t = new Thread(() => InstallNodeJsThread(versionArg));
            t.IsBackground = true;
            t.Start();
        }

        private void InstallNodeJsThread(string versionArg)
        {
            bool isLatest = string.IsNullOrEmpty(versionArg);
            string wingetCmd = isLatest
                ? "winget install OpenJS.NodeJS.LTS --accept-package-agreements --accept-source-agreements"
                : "winget install OpenJS.NodeJS.LTS --version " + versionArg +
                  " --accept-package-agreements --accept-source-agreements";
            AppendLog("开始安装 Node.js：" + (isLatest ? "最新版" : "版本 " + versionArg));
            AppendLog("执行：" + wingetCmd);

            var psi = BuildPsi("cmd.exe", "/c " + wingetCmd);
            try
            {
                var p = new Process { StartInfo = psi };
                p.Start();
                ReadLinesUtf8Async(p.StandardOutput.BaseStream, delegate (string line) { AppendLog(CleanAnsi(line)); });
                ReadLinesUtf8Async(p.StandardError.BaseStream, delegate (string line) { AppendLog(CleanAnsi(line)); });
                p.WaitForExit();
                int code = p.ExitCode;
                if (code == 0)
                    AppendLog("Node.js 安装完成，正在重新检测环境...");
                else
                    AppendLog("Node.js 安装未完成（退出码 " + code + "），请查看上方日志。");

                Thread t = new Thread(DetectEnvironment);
                t.IsBackground = true;
                t.Start();
            }
            catch (Exception ex)
            {
                AppendLog("Node.js 安装出错：" + ex.Message);
            }
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AboutWindow();
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        // ------------------------------------------------------------------
        // 启动 / 重启 / 安装（后台线程）
        // ------------------------------------------------------------------
        private void StartDshThread()
        {
            lock (_collisionLock) { _collision = null; }
            Interlocked.Exchange(ref _collisionPrompted, 0);
            RunOnUi(delegate
            {
                _btnStart.IsEnabled = false;
                _btnStop.IsEnabled = false;
            });
            SetStatus("正在启动 DSH...");

            // DSH 新版本（dsh web）会自行打开默认浏览器；这里显式传 --no-open，
            // 关闭 DSH 自带的浏览器弹出，只保留下方 OpenBrowserWhenReady 的一次自动打开，
            // 避免每次启动弹出两个浏览器标签页。
            string args = _dshGlobal
                ? "/c \"" + _dshCmd + "\" web --no-open"
                : "/c npx --yes " + PackageName + " web --no-open";
            AppendLog("启动命令：" + (_dshGlobal ? (_dshCmd + " web --no-open") : ("npx " + PackageName + " web --no-open")));

            var psi = BuildPsi("cmd.exe", args);
            Process p = null;
            try
            {
                p = new Process { StartInfo = psi };
                p.Start();
                ReadLinesUtf8Async(p.StandardOutput.BaseStream, delegate (string line) { CaptureOutput(CleanAnsi(line)); });
                ReadLinesUtf8Async(p.StandardError.BaseStream, delegate (string line) { CaptureOutput(CleanAnsi(line)); });
                _dshProcess = p;

                RunOnUi(delegate { _btnStop.IsEnabled = true; });
                AppendLog("正在通话中（PID " + p.Id + "），等待蓝色大肥鱼上岗...");
                SetStatus("蓝色大肥鱼已被录用，正在买票准备报道...");

                OpenBrowserWhenReady();

                p.WaitForExit();
                int code = p.ExitCode;
                AppendLog("蓝色大肥鱼已被放生大自然，退出码 " + code);

                RunOnUi(delegate
                {
                    _dshProcess = null;
                    _serviceRunning = false;
                    _btnStart.IsEnabled = true;
                    _btnStop.IsEnabled = false;
                    RefreshEnvCards();
                });
                SetStatus("蓝色大肥鱼已经放生大自然");
            }
            catch (Exception ex)
            {
                AppendLog("启动失败：" + ex.Message);
                RunOnUi(delegate
                {
                    _btnStart.IsEnabled = true;
                    _btnStop.IsEnabled = false;
                    RefreshEnvCards();
                });
                SetStatus("启动失败");
            }
        }

        private void RestartDshThread()
        {
            try
            {
                RunOnUi(delegate
                {
                    _btnStart.IsEnabled = false;
                    _btnStop.IsEnabled = false;
                });
                SetStatus("发布招聘信息...");

                List<int> pids = CollectDshPids();
                if (pids.Count > 0)
                {
                    AppendLog("重启：先强制停止当前蓝色大肥鱼（" + pids.Count + " 条进程）...");
                    ForceStopAll(pids);
                }
                else
                {
                    AppendLog("重启：当前无在岗蓝色大肥鱼，正在捕捉...");
                }

                AppendLog("等待端口释放...");
                for (int i = 0; i < 15; i++)
                {
                    Thread.Sleep(1000);
                    if (!IsPortOpen(3080)) break;
                }

                if (IsPortOpen(3080))
                    AppendLog("警告：端口仍被占用，可能重启失败，继续尝试启动...");

                RunOnUi(delegate
                {
                    _dshProcess = null;
                    _serviceRunning = false;
                    RefreshEnvCards();
                });
            }
            finally
            {
                _restarting = false;
            }

            AppendLog("重新启用蓝色大肥鱼...");
            StartDshThread();
        }

        private void InstallDshThread()
        {
            RunOnUi(delegate { _btnInstall.IsEnabled = false; });
            SetStatus("正在捕捉 蓝色大肥鱼...");
            AppendLog("开始执行捕捉：npm install -g " + PackageName);

            var psi = BuildPsi("cmd.exe", "/c npm install -g " + PackageName);
            try
            {
                var p = new Process { StartInfo = psi };
                p.Start();
                ReadLinesUtf8Async(p.StandardOutput.BaseStream, delegate (string line) { AppendLog(CleanAnsi(line)); });
                ReadLinesUtf8Async(p.StandardError.BaseStream, delegate (string line) { AppendLog(CleanAnsi(line)); });
                p.WaitForExit();
                int code = p.ExitCode;

                if (code == 0)
                {
                    AppendLog("DSH 捕捉完成，正在重新检测环境...");
                    Thread t = new Thread(DetectEnvironment);
                    t.IsBackground = true;
                    t.Start();
                }
                else
                {
                    AppendLog("步骤失败，退出码 " + code + "，请查看上方日志。");
                    RunOnUi(delegate { _btnInstall.IsEnabled = true; });
                    SetStatus("捕捉失败");
                }
            }
            catch (Exception ex)
            {
                AppendLog("捕捉出错：" + ex.Message);
                RunOnUi(delegate { _btnInstall.IsEnabled = true; });
                SetStatus("捕捉失败");
            }
        }

        private void OpenBrowserWhenReady()
        {
            for (int i = 0; i < 30; i++)
            {
                Thread.Sleep(1000);
                if (IsPortOpen(3080))
                {
                    AppendLog("蓝色大肥鱼已就绪，自动前往工位...");
                    OpenBrowser();
                    RunOnUi(delegate
                    {
                        _serviceRunning = true;
                        RefreshEnvCards();
                    });
                    SetStatus("蓝色大肥鱼工作中...");
                    return;
                }
            }
            AppendLog("等待 30 秒后蓝色大肥鱼仍未就绪，请查看上方日志排查。");
        }

        // ------------------------------------------------------------------
        // 停止服务：扫描 + 强制结束
        // ------------------------------------------------------------------
        private List<int> CollectDshPids()
        {
            var pids = new List<int>();

            var p = _dshProcess;
            if (p != null)
            {
                try { if (!p.HasExited) pids.Add(p.Id); }
                catch { }
            }

            foreach (int id in FindDshNodePids())
                if (!pids.Contains(id)) pids.Add(id);

            foreach (int id in FindListenerPids(3080))
                if (!pids.Contains(id)) pids.Add(id);

            return pids;
        }

        private static List<int> FindDshNodePids()
        {
            var pids = new List<int>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'node.exe'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            string cmd = obj["CommandLine"] as string;
                            if (cmd != null &&
                                cmd.IndexOf("deepseek-ai", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                cmd.IndexOf("dsh", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                pids.Add(Convert.ToInt32(obj["ProcessId"]));
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return pids;
        }

        private static List<int> FindListenerPids(int port)
        {
            var pids = new List<int>();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                using (var proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    foreach (string line in output.Split('\n'))
                    {
                        string t = line.Trim();
                        if (t.StartsWith("TCP", StringComparison.OrdinalIgnoreCase) &&
                            t.IndexOf("LISTENING", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            t.Contains(":" + port))
                        {
                            string[] parts = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 5)
                            {
                                int pid;
                                if (int.TryParse(parts[parts.Length - 1], out pid))
                                    pids.Add(pid);
                            }
                        }
                    }
                }
            }
            catch { }
            return pids;
        }

        private void ForceStopAll(List<int> pids)
        {
            foreach (int id in pids)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = "/F /T /PID " + id,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    AppendLog("开除吃干饭蓝色大肥鱼 PID " + id + "（含子进程）。");
                }
                catch (Exception ex)
                {
                    AppendLog("停止 PID " + id + " 失败：" + ex.Message);
                }
            }
        }

        // ------------------------------------------------------------------
        // 服务冲突诊断：运行时捕捉 DSH 报错并重新提示
        // ------------------------------------------------------------------
        private void CaptureOutput(string line)
        {
            AppendLog(line);
            DetectCollision(line);
        }

        private void DetectCollision(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            bool fire = false;
            lock (_collisionLock)
            {
                if (_collision == null)
                {
                    Match m = CollisionOriginalRegex.Match(line);
                    if (!m.Success) m = CollisionImprovedRegex.Match(line);
                    if (m.Success)
                    {
                        _collision = new CollisionInfo
                        {
                            Service = m.Groups["service"].Value,
                            OwnerId = m.Groups["owner"].Value
                        };
                    }
                }

                if (_collision != null && string.IsNullOrEmpty(_collision.ClaimantName))
                {
                    Match lm = LoaderEntryRegex.Match(line);
                    if (lm.Success)
                    {
                        _collision.ClaimantId = lm.Groups["id"].Value;
                        _collision.ClaimantName = lm.Groups["name"].Value;
                        if (string.IsNullOrEmpty(_collision.ClaimantName))
                            _collision.ClaimantName = _collision.ClaimantId;
                    }
                }

                fire = _collision != null && !string.IsNullOrEmpty(_collision.Service);
            }

            if (fire) ShowCollisionPromptOnce(_collision);
        }

        private void ShowCollisionPromptOnce(CollisionInfo info)
        {
            if (Interlocked.Exchange(ref _collisionPrompted, 1) != 0) return;

            try { info.OwnerName = ResolveEntryPackageName(info.OwnerId); }
            catch { }
            string removableOwner = ResolveRemovableOwner(info.OwnerName);
            // 卸载占用方目标：优先可卸载的直接依赖，否则退回 owner 包名 / 条目 id，保证按钮始终出现
            string removeOwner = !string.IsNullOrEmpty(removableOwner)
                ? removableOwner
                : (!string.IsNullOrEmpty(info.OwnerName) ? info.OwnerName : info.OwnerId);

            string ownerDisplay = string.IsNullOrEmpty(info.OwnerName)
                ? ("条目 " + info.OwnerId)
                : (!string.IsNullOrEmpty(removableOwner) && !string.Equals(removableOwner, info.OwnerName, StringComparison.OrdinalIgnoreCase)
                    ? (info.OwnerName + "（由 " + removableOwner + " 引入）")
                    : info.OwnerName);
            string claimantDisplay = string.IsNullOrEmpty(info.ClaimantName)
                ? (string.IsNullOrEmpty(info.ClaimantId) ? "未知插件" : info.ClaimantId)
                : info.ClaimantName;
            string removeClaimant = string.IsNullOrEmpty(info.ClaimantName) ? info.ClaimantId : info.ClaimantName;

            var sb = new StringBuilder();
            sb.AppendLine("服务名 \"" + info.Service + "\" 被两个插件重复注册：");
            sb.AppendLine();
            sb.AppendLine("  · 后注册占用：" + ownerDisplay);
            sb.AppendLine("  · 本地冲突插件：" + claimantDisplay);
            sb.AppendLine();
            sb.AppendLine("报告长官：Cordis 服务名是全局唯一的，同名服务只能有一个提供者。");
            sb.AppendLine();
            sb.AppendLine("哥们你选一个卸载吧（卸载后要重启服务哦！）：");
            if (!string.IsNullOrEmpty(removeOwner))
                sb.AppendLine("  · 「卸载占用方」将移除 " + removeOwner);
            if (!string.IsNullOrEmpty(removeClaimant))
                sb.AppendLine("  · 「卸载冲突方」将移除 " + removeClaimant);
            string msg = sb.ToString();

            foreach (string l in msg.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                AppendLog("【诊断】" + l);

            RunOnUi(delegate { ShowCollisionDialog(info, msg, removeOwner); });
            SetStatus("启动失败：插件服务冲突");
        }

        private void ShowCollisionDialog(CollisionInfo info, string msg, string removableOwner)
        {
            var dlg = new Window
            {
                Title = "蓝色大肥鱼 启动失败：插件服务冲突",
                Width = 540,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = PanelBackBrush,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13
            };

            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var txt = new TextBlock
            {
                Text = msg,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16),
                Foreground = new SolidColorBrush(Color.FromRgb(40, 44, 52))
            };
            grid.Children.Add(txt);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(btnPanel, 1);
            grid.Children.Add(btnPanel);

            string removeClaimant = string.IsNullOrEmpty(info.ClaimantName) ? info.ClaimantId : info.ClaimantName;
            if (!string.IsNullOrEmpty(removeClaimant))
            {
                var btnClaimant = MakeButton("卸载冲突方", 110, Color.FromRgb(198, 60, 60));
                btnClaimant.Click += delegate
                {
                    dlg.Close();
                    StartRemovePlugin(removeClaimant);
                };
                btnPanel.Children.Add(btnClaimant);
            }

            if (!string.IsNullOrEmpty(removableOwner))
            {
                var btnOwner = MakeButton("卸载占用方", 110, Color.FromRgb(198, 60, 60));
                btnOwner.Click += delegate
                {
                    dlg.Close();
                    StartRemovePlugin(removableOwner);
                };
                btnPanel.Children.Add(btnOwner);
            }

            var btnLater = MakeButton("稍后处理", 96, Color.FromRgb(88, 88, 96));
            btnLater.Click += delegate { dlg.Close(); };
            btnPanel.Children.Add(btnLater);

            dlg.Content = grid;
            dlg.ShowDialog();
        }

        private void StartRemovePlugin(string package)
        {
            if (string.IsNullOrEmpty(package)) return;
            AppendLog("准备卸载：" + package);
            SetStatus("正在卸载 " + package + " ...");
            Thread t = new Thread(() => RemovePluginThread(package));
            t.IsBackground = true;
            t.Start();
        }

        private void RemovePluginThread(string package)
        {
            string args = _dshGlobal
                ? "/c \"" + _dshCmd + "\" plugin --profile web remove " + package
                : "/c npx --yes " + PackageName + " plugin --profile web remove " + package;
            string cmdDisplay = _dshGlobal
                ? (_dshCmd + " plugin --profile web remove " + package)
                : ("npx " + PackageName + " plugin --profile web remove " + package);
            AppendLog("执行：" + cmdDisplay);

            string output;
            int code = RunSync("cmd.exe", args, out output);
            if (!string.IsNullOrEmpty(output))
            {
                foreach (string line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    AppendLog(line);
            }

            if (code == 0)
            {
                AppendLog("已成功卸载 " + package + "。");
                RunOnUi(delegate
                {
                    MessageBoxResult r = MessageBox.Show(
                        "已成功卸载 " + package + "。\r\n\r\n是否立即重启蓝色大肥鱼？",
                        "卸载完成", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (r == MessageBoxResult.Yes)
                    {
                        _restarting = true;
                        Thread t = new Thread(RestartDshThread);
                        t.IsBackground = true;
                        t.Start();
                    }
                    else
                    {
                        SetStatus("已卸载 " + package + "，可点击「重启服务」");
                    }
                });
            }
            else
            {
                // 间接依赖无法直接卸载：从 pnpm 报错解析可用直接依赖并改卸载它
                string alt = FindAlternativeDirectDependency(output, package);
                if (!string.IsNullOrEmpty(alt) && !string.Equals(alt, package, StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog("目标 " + package + " 为间接依赖，改为卸载直接依赖 " + alt + " ...");
                    RemovePluginThread(alt);
                    return;
                }
                AppendLog("卸载失败，退出码 " + code + "，请查看上方日志。");
                SetStatus("卸载失败");
            }
        }

        private string ResolveEntryPackageName(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return null;
            try
            {
                string profileDir = GetWebProfileDir();
                if (profileDir == null) return null;
                string nmDir = System.IO.Path.Combine(profileDir, "node_modules");
                if (!Directory.Exists(nmDir)) return null;

                foreach (string pkgDir in EnumeratePackageDirs(nmDir))
                {
                    string patch = System.IO.Path.Combine(pkgDir, "cordis.patch.yml");
                    if (!File.Exists(patch)) continue;
                    string name = FindEntryNameInPatch(patch, entryId);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
            catch { }
            return null;
        }

        private static string ResolveRemovableOwner(string ownerPackage)
        {
            if (string.IsNullOrEmpty(ownerPackage)) return null;
            try
            {
                string profileDir = GetWebProfileDir();
                if (profileDir == null) return null;
                string manifest = System.IO.Path.Combine(profileDir, "package.json");
                if (!File.Exists(manifest)) return null;

                var directDeps = ReadDependencyNames(File.ReadAllText(manifest));
                if (directDeps.Count == 0) return null;

                if (directDeps.Contains(ownerPackage)) return ownerPackage;

                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string dep in directDeps)
                {
                    visited.Clear();
                    if (DependsOn(profileDir, dep, ownerPackage, visited, 0))
                        return dep;
                }
            }
            catch { }
            return null;
        }

        private static List<string> ReadDependencyNames(string packageJson)
        {
            var result = new List<string>();
            // 同时读取 dependencies / peerDependencies / optionalDependencies
            foreach (Match section in Regex.Matches(packageJson,
                @"[""'](?:dependencies|peerDependencies|optionalDependencies)[""']\s*:\s*\{([\s\S]*?)\}"))
            {
                foreach (Match km in Regex.Matches(section.Groups[1].Value, @"[""']([^""']+)[""']\s*:"))
                    result.Add(km.Groups[1].Value);
            }
            return result;
        }

        private static string FindAlternativeDirectDependency(string output, string package)
        {
            if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(package)) return null;
            Match m = Regex.Match(output, @"Available dependencies:\s*(?<list>[^\r\n]+)");
            if (!m.Success) return null;

            var deps = new List<string>();
            foreach (string d in m.Groups["list"].Value.Split(','))
            {
                string t = d.Trim();
                if (!string.IsNullOrEmpty(t)) deps.Add(t);
            }
            if (deps.Count == 0) return null;

            // 优先匹配同 scope 的直接依赖
            if (package.StartsWith("@", StringComparison.Ordinal))
            {
                int slash = package.IndexOf('/');
                if (slash > 0)
                {
                    string scope = package.Substring(0, slash + 1);
                    foreach (string d in deps)
                        if (d.StartsWith(scope, StringComparison.OrdinalIgnoreCase)) return d;
                }
            }

            // 其次按名字包含关系匹配
            string bare = package;
            int pSlash = package.IndexOf('/');
            if (pSlash > 0) bare = package.Substring(pSlash + 1);
            foreach (string d in deps)
            {
                int dSlash = d.IndexOf('/');
                string dBare = dSlash > 0 ? d.Substring(dSlash + 1) : d;
                if (bare.IndexOf(dBare, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dBare.IndexOf(bare, StringComparison.OrdinalIgnoreCase) >= 0)
                    return d;
            }
            return null;
        }

        private static bool DependsOn(string profileDir, string package, string target, HashSet<string> visited, int depth)
        {
            if (depth > 6 || string.IsNullOrEmpty(package)) return false;
            if (!visited.Add(package)) return false;

            string pkgJson = ResolvePackageJsonPath(profileDir, package);
            if (pkgJson == null || !File.Exists(pkgJson)) return false;

            string json;
            try { json = File.ReadAllText(pkgJson); }
            catch { return false; }

            foreach (string dep in ReadDependencyNames(json))
            {
                if (string.Equals(dep, target, StringComparison.OrdinalIgnoreCase)) return true;
                if (DependsOn(profileDir, dep, target, visited, depth + 1)) return true;
            }
            return false;
        }

        private static string ResolvePackageJsonPath(string profileDir, string package)
        {
            string nmDir = System.IO.Path.Combine(profileDir, "node_modules");
            if (package.StartsWith("@", StringComparison.Ordinal))
            {
                int slash = package.IndexOf('/');
                if (slash > 0)
                    return System.IO.Path.Combine(nmDir, package.Substring(0, slash), package.Substring(slash + 1), "package.json");
            }
            return System.IO.Path.Combine(nmDir, package, "package.json");
        }

        private static string GetWebProfileDir()
        {
            string home = Environment.GetEnvironmentVariable("DSH_HOME");
            if (string.IsNullOrEmpty(home))
                home = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");
            return System.IO.Path.Combine(home, "profiles", "web");
        }

        private static IEnumerable<string> EnumeratePackageDirs(string nodeModulesDir)
        {
            string[] top;
            try { top = Directory.GetDirectories(nodeModulesDir); }
            catch { yield break; }
            foreach (string dir in top)
            {
                string name = System.IO.Path.GetFileName(dir);
                if (name.StartsWith("@", StringComparison.Ordinal))
                {
                    string[] subs;
                    try { subs = Directory.GetDirectories(dir); }
                    catch { continue; }
                    foreach (string sub in subs) yield return sub;
                }
                else
                {
                    yield return dir;
                }
            }
        }

        private static string FindEntryNameInPatch(string patchFile, string entryId)
        {
            string[] lines;
            try { lines = File.ReadAllLines(patchFile); }
            catch { return null; }
            for (int i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i], @"^\s*-\s+id\s*:\s*(['""]?)" + Regex.Escape(entryId) + @"\1\s*$"))
                {
                    for (int j = i + 1; j < lines.Length && j <= i + 4; j++)
                    {
                        if (Regex.IsMatch(lines[j], @"^\s*-\s+id\s*:")) break;
                        Match m = Regex.Match(lines[j], @"^\s*name\s*:\s*['""]?(?<name>[^'""\r\n]+)['""]?\s*$");
                        if (m.Success) return m.Groups["name"].Value.Trim();
                    }
                }
            }
            return null;
        }

        // ------------------------------------------------------------------
        // 工具方法
        // ------------------------------------------------------------------
        internal static int RunSync(string fileName, string args, out string output)
        {
            output = "";
            try
            {
                var psi = BuildPsi(fileName, args);
                using (var p = new Process())
                {
                    p.StartInfo = psi;
                    p.Start();
                    string o = ReadUtf8(p.StandardOutput.BaseStream);
                    string e = ReadUtf8(p.StandardError.BaseStream);
                    p.WaitForExit();
                    output = (o + "\n" + e).Trim();
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return -1;
            }
        }

        private static string ReadUtf8(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static void ReadLinesUtf8Async(Stream stream, Action<string> onLine)
        {
            var t = new Thread(delegate () { ReadLinesUtf8(stream, onLine); });
            t.IsBackground = true;
            t.Start();
        }

        private static void ReadLinesUtf8(Stream stream, Action<string> onLine)
        {
            try
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrEmpty(line))
                            onLine(line);
                    }
                }
            }
            catch { }
        }

        internal static ProcessStartInfo BuildPsi(string fileName, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
            psi.EnvironmentVariables["NO_COLOR"] = "1";
            psi.EnvironmentVariables["FORCE_COLOR"] = "0";
            return psi;
        }

        private bool HasNpxCache()
        {
            try
            {
                string root = _npmCache;
                if (string.IsNullOrEmpty(root))
                    root = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "npm-cache");
                string cache = System.IO.Path.Combine(root, "_npx");
                if (!Directory.Exists(cache))
                    return false;
                foreach (string dir in Directory.GetDirectories(cache))
                {
                    if (Directory.Exists(System.IO.Path.Combine(dir, "node_modules", "@deepseek-ai", "dsh")))
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPortOpen(int port)
        {
            try
            {
                using (var c = new TcpClient())
                {
                    IAsyncResult r = c.BeginConnect("127.0.0.1", port, null, null);
                    if (!r.AsyncWaitHandle.WaitOne(300))
                        return false;
                    c.EndConnect(r);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string CleanAnsi(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            return AnsiRegex.Replace(s, "").TrimEnd('\r');
        }

        private void AppendLog(string line)
        {
            RunOnUi(delegate
            {
                if (_txtLog.Text.Length > 200000)
                    _txtLog.Clear();
                _txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line + "\r\n");
                _txtLog.ScrollToEnd();
            });
        }

        private void SetStatus(string text)
        {
            RunOnUi(delegate { _lblStatus.Text = text; });
        }

        private void RunOnUi(Action action)
        {
            try
            {
                if (Dispatcher.CheckAccess())
                    action();
                else
                    Dispatcher.Invoke((Delegate)action);
            }
            catch { }
        }

        private static void OpenBrowser()
        {
            try { Process.Start(WebUrl); }
            catch { }
        }

        private enum CloseChoice
        {
            Cancel,
            StopAndClose,
            CloseOnly
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            List<int> pids = CollectDshPids();
            if (pids.Count > 0)
            {
                CloseChoice choice = ShowClosePrompt(pids.Count);
                if (choice == CloseChoice.StopAndClose)
                {
                    AppendLog("关闭启动器：正在开除 蓝色大肥鱼（" + pids.Count + " 条进程）...");
                    ForceStopAll(pids);
                }
                else if (choice == CloseChoice.CloseOnly)
                {
                    AppendLog("启动器已关闭，蓝色大肥鱼保持手感中（" + pids.Count + " 条进程）。");
                }
                else
                {
                    e.Cancel = true;
                }
            }
            base.OnClosing(e);
        }

        private CloseChoice ShowClosePrompt(int pidCount)
        {
            CloseChoice result = CloseChoice.Cancel;

            var dlg = new Window
            {
                Title = "关闭启动器",
                Width = 480,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = PanelBackBrush,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13
            };

            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var txt = new TextBlock
            {
                Text = "检测到 蓝色大肥鱼仍在加班（" + pidCount + " 条进程）。\r\n\r\n请选择慰问方式：",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16),
                Foreground = new SolidColorBrush(Color.FromRgb(40, 44, 52))
            };
            grid.Children.Add(txt);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(btnPanel, 1);
            grid.Children.Add(btnPanel);

            var btnStop = MakeButton("你被开除了", 110, Color.FromRgb(198, 60, 60));
            btnStop.Click += delegate { result = CloseChoice.StopAndClose; dlg.Close(); };
            btnPanel.Children.Add(btnStop);

            var btnCloseOnly = MakeButton("我先走了好好干", 110, Color.FromRgb(0, 122, 204));
            btnCloseOnly.Click += delegate { result = CloseChoice.CloseOnly; dlg.Close(); };
            btnPanel.Children.Add(btnCloseOnly);

            var btnCancel = MakeButton("嘿嘿点错了", 80, Color.FromRgb(88, 88, 96));
            btnCancel.Click += delegate { result = CloseChoice.Cancel; dlg.Close(); };
            btnPanel.Children.Add(btnCancel);

            dlg.Content = grid;
            dlg.Owner = this;
            dlg.ShowDialog();

            return result;
        }

        private sealed class CollisionInfo
        {
            public string Service;
            public string OwnerId;
            public string OwnerName;
            public string ClaimantId;
            public string ClaimantName;
        }
    }

    internal class AboutWindow : Window
    {
        public AboutWindow()
        {
            Title = "关于 DSH Launcher";
            Width = 400;
            Height = 210;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            FontFamily = new FontFamily("Microsoft YaHei UI");

            var sp = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };

            sp.Children.Add(new TextBlock
            {
                Text = "DSH Launcher",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            sp.Children.Add(new TextBlock
            {
                Text = MainWindow.LauncherVersion,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 164, 172)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            });

            sp.Children.Add(new TextBlock
            {
                Text = "DSH（DeepSeek Harness）一键启动器\r\n单文件、绿色便携，用于快捷使用 DSH。",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 114, 122)),
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            });

            var btnGithub = MainWindow.MakeButton("前往 GitHub", 140, Color.FromRgb(88, 88, 96));
            btnGithub.Margin = new Thickness(0, 18, 0, 0);
            btnGithub.HorizontalAlignment = HorizontalAlignment.Center;
            btnGithub.Click += delegate
            {
                try { Process.Start("https://github.com/rpvvn/EasyDSH"); }
                catch { }
            };
            sp.Children.Add(btnGithub);

            Content = sp;
        }
    }

    internal class InfoWindow : Window
    {
        public InfoWindow(string title, string message)
        {
            Title = title;
            Width = 440;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            FontFamily = new FontFamily("Microsoft YaHei UI");

            var sp = new StackPanel { Margin = new Thickness(28, 24, 28, 20) };

            var msg = new TextBlock
            {
                Text = message,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(40, 44, 52))
            };
            sp.Children.Add(msg);

            var btnOk = MainWindow.MakeButton("确定", 100, Color.FromRgb(0, 122, 204));
            btnOk.Margin = new Thickness(0, 18, 0, 0);
            btnOk.HorizontalAlignment = HorizontalAlignment.Center;
            btnOk.Click += delegate { Close(); };
            sp.Children.Add(btnOk);

            Content = sp;
        }
    }

    internal class NodeJsInstallWindow : Window
    {
        private WrapPanel _versionPanel;
        private TextBlock _status;

        public NodeJsInstallWindow()
        {
            Title = "\u5b89\u88c5 Node.js";
            Width = 460;
            Height = 480;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            FontFamily = new FontFamily("Microsoft YaHei UI");

            var grid = new Grid { Margin = new Thickness(26, 20, 26, 16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var desc = new TextBlock
            {
                Text = "\u672a\u68c0\u6d4b\u5230 Node.js\uff0c\u70b9\u51fb\u4e0b\u65b9\u7248\u672c\u6309\u94ae\u5373\u53ef\u5b89\u88c5\u5bf9\u5e94\u7248\u672c\uff1a",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(40, 44, 52)),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(desc, 0);
            grid.Children.Add(desc);

            _versionPanel = new WrapPanel();
            var scroller = new ScrollViewer
            {
                Content = _versionPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(scroller, 1);
            grid.Children.Add(scroller);

            _status = new TextBlock
            {
                Text = "\u6b63\u5728\u83b7\u53d6\u53ef\u7528\u7248\u672c...",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 124, 132)),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(_status, 2);
            grid.Children.Add(_status);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var btnLatest = MainWindow.MakeButton("\u5b89\u88c5\u6700\u65b0\u7248", 110, Color.FromRgb(0, 122, 204));
            var btnDownload = MainWindow.MakeButton("\u53bb\u4e0b\u8f7d\u9875", 90, Color.FromRgb(88, 88, 96));
            var btnCancel = MainWindow.MakeButton("\u53d6\u6d88", 80, Color.FromRgb(158, 60, 60));

            btnLatest.Click += delegate
            {
                var owner = Owner as MainWindow;
                if (owner != null) owner.StartNodeJsInstall("");
                Close();
            };
            btnDownload.Click += delegate
            {
                try { Process.Start("https://nodejs.org/zh-cn/download"); }
                catch { }
            };
            btnCancel.Click += delegate { Close(); };

            btnPanel.Children.Add(btnLatest);
            btnPanel.Children.Add(btnDownload);
            btnPanel.Children.Add(btnCancel);
            Grid.SetRow(btnPanel, 3);
            grid.Children.Add(btnPanel);

            Content = grid;

            Loaded += delegate
            {
                Thread t = new Thread(LoadVersions);
                t.IsBackground = true;
                t.Start();
            };
        }

        private void LoadVersions()
        {
            try
            {
                string outp;
                int code = MainWindow.RunSync("cmd.exe",
                    "/c winget show OpenJS.NodeJS.LTS --versions --accept-source-agreements", out outp);
                var versions = ParseVersions(outp);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (versions.Count == 0)
                    {
                        _status.Text = "\u83b7\u53d6\u7248\u672c\u5931\u8d25\uff0c\u53ef\u5c1d\u8bd5\u300c\u53bb\u4e0b\u8f7d\u9875\u300d\u624b\u52a8\u5b89\u88c5\u3002\r\n" + outp;
                        return;
                    }
                    BuildVersionButtons(versions);
                    _status.Text = "\u5171 " + versions.Count + " \u4e2a\u7248\u672c\uff0c\u70b9\u51fb\u6309\u94ae\u5373\u53ef\u5b89\u88c5\u3002";
                }));
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _status.Text = "\u83b7\u53d6\u7248\u672c\u5931\u8d25\uff1a" + ex.Message;
                }));
            }
        }

        private void BuildVersionButtons(List<string> versions)
        {
            var rainbow = new[]
            {
                Color.FromRgb(230, 145, 56),
                Color.FromRgb(0, 122, 204),
                Color.FromRgb(0, 150, 90),
                Color.FromRgb(142, 68, 173),
                Color.FromRgb(198, 60, 60),
                Color.FromRgb(0, 150, 136)
            };

            for (int i = 0; i < versions.Count; i++)
            {
                string ver = versions[i];
                var b = MainWindow.MakeButton(ver, 170, rainbow[i % rainbow.Length]);
                b.Margin = new Thickness(0, 0, 10, 10);
                b.Click += delegate
                {
                    var owner = Owner as MainWindow;
                    if (owner != null) owner.StartNodeJsInstall(ver);
                    Close();
                };
                _versionPanel.Children.Add(b);
            }
        }

        private static List<string> ParseVersions(string output)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(output)) return list;
            foreach (string line in output.Split('\n'))
            {
                string t = line.Trim();
                if (t.Length == 0) continue;
                if (t.StartsWith("Found ", StringComparison.OrdinalIgnoreCase)) continue;
                if (Regex.IsMatch(t, @"^\d+(\.\d+)+"))
                    list.Add(t);
            }
            return list;
        }
    }

    internal class UpdatePromptWindow : Window
    {
        public UpdatePromptWindow(string latest, string current)
        {
            Title = "发现新版本";
            Width = 400;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            FontFamily = new FontFamily("Microsoft YaHei UI");

            var sp = new StackPanel { Margin = new Thickness(28, 24, 28, 20) };

            sp.Children.Add(new TextBlock
            {
                Text = "发现新版本 " + latest + "，当前 " + current + "\r\n是否更新？",
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(40, 44, 52))
            });

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 18, 0, 0)
            };

            var btnYes = MainWindow.MakeButton("更就完了", 110, Color.FromRgb(0, 150, 90));
            var btnNo = MainWindow.MakeButton("更个鸡毛", 110, Color.FromRgb(88, 88, 96));

            btnYes.Click += delegate
            {
                var owner = Owner as MainWindow;
                if (owner != null) owner.StartDshUpdate();
                Close();
            };
            btnNo.Click += delegate { Close(); };

            btnPanel.Children.Add(btnYes);
            btnPanel.Children.Add(btnNo);
            sp.Children.Add(btnPanel);

            Content = sp;
        }
    }

    internal class UninstallWindow : Window
    {
        public UninstallWindow()
        {
            Title = "一键还原";
            Width = 460;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            FontFamily = new FontFamily("Microsoft YaHei UI");

            var sp = new StackPanel { Margin = new Thickness(28, 24, 28, 20) };

            sp.Children.Add(new TextBlock
            {
                Text = "请选择卸载方式：",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(40, 44, 52)),
                TextAlignment = TextAlignment.Center
            });

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 18, 0, 0)
            };
            var btnOnly = MainWindow.MakeButton("卸载DSH本体", 120, Color.FromRgb(230, 145, 56));
            var btnFull = MainWindow.MakeButton("完全卸载DSH", 120, Color.FromRgb(198, 60, 60));
            var btnCancel = MainWindow.MakeButton("取消", 80, Color.FromRgb(88, 88, 96));

            btnOnly.Click += delegate
            {
                var owner = Owner as MainWindow;
                if (owner != null)
                {
                    owner.ShowConfirm("二次确认",
                        "确定要卸载 DSH 本体吗？\r\n\r\n将执行：\r\n" +
                        "  · npm uninstall -g @deepseek-ai/dsh\r\n" +
                        "  · 清理 npm/npx 缓存中的 DSH 文件\r\n\r\n" +
                        "影响：DSH 命令将不可用，需重新安装才能继续使用；不影响 ~/.dsh 配置文件。",
                        delegate { owner.StartDshUninstall(false); });
                }
                Close();
            };
            btnFull.Click += delegate
            {
                var owner = Owner as MainWindow;
                if (owner != null)
                {
                    owner.ShowConfirm("二次确认",
                        "确定要完全卸载 DSH 吗？\r\n\r\n将执行：\r\n" +
                        "  · npm uninstall -g @deepseek-ai/dsh\r\n" +
                        "  · 清理 npm/npx 缓存\r\n" +
                        "  · 删除用户配置目录 ~/.dsh（含配置、插件、数据）\r\n\r\n" +
                        "影响：DSH 及其所有配置、数据将被永久删除，不可恢复！",
                        delegate { owner.StartDshUninstall(true); });
                }
                Close();
            };
            btnCancel.Click += delegate { Close(); };

            btnPanel.Children.Add(btnOnly);
            btnPanel.Children.Add(btnFull);
            btnPanel.Children.Add(btnCancel);
            sp.Children.Add(btnPanel);

            Content = sp;
        }
    }

    internal class ConfirmWindow : Window
    {
        public ConfirmWindow(string title, string message, Action onConfirm)
        {
            Title = title;
            Width = 440;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            FontFamily = new FontFamily("Microsoft YaHei UI");

            var sp = new StackPanel { Margin = new Thickness(28, 24, 28, 20) };

            sp.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(40, 44, 52))
            });

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 18, 0, 0)
            };
            var btnOk = MainWindow.MakeButton("确认", 100, Color.FromRgb(198, 60, 60));
            var btnCancel = MainWindow.MakeButton("取消", 100, Color.FromRgb(88, 88, 96));

            btnOk.Click += delegate
            {
                Close();
                if (onConfirm != null) onConfirm();
            };
            btnCancel.Click += delegate { Close(); };

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            sp.Children.Add(btnPanel);

            Content = sp;
        }
    }

    internal static class Program
    {
        private const string SingleInstanceMutexName =
            "Global\\DSH-Launcher-SingleInstance-{4B2E6C0F-7A11-4E3D-9A5B-1C8F3E0D2A66}";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, SingleInstanceMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "oi oi oi小鬼，你已经打开了一个了喂！",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var app = new Application();
                app.Run(new MainWindow());
            }
        }
    }
}
