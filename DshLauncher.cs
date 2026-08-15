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
            Width = 600;
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

            _btnAbout = MakeSmallButton("关于", 64, Color.FromRgb(88, 88, 96));
            _btnCheck = MakeSmallButton("检查更新", 80, Color.FromRgb(0, 122, 204));
            _btnMirror = MakeSmallButton("国内镜像源", 80, Color.FromRgb(0, 122, 204));
            _btnAbout.VerticalAlignment = VerticalAlignment.Center;
            _btnMirror.VerticalAlignment = VerticalAlignment.Center;
            _btnCheck.VerticalAlignment = VerticalAlignment.Center;
            _btnMirror.ToolTip = "下载缓慢？切换国内 npm 镜像源";
            _btnCheck.ToolTip = "拉取最新版本与当前版本对比";
            _btnAbout.Click += BtnAbout_Click;
            _btnMirror.Click += BtnMirror_Click;
            _btnCheck.Click += BtnCheckUpdate_Click;
            DockPanel.SetDock(_btnAbout, Dock.Right);
            DockPanel.SetDock(_btnCheck, Dock.Right);
            DockPanel.SetDock(_btnMirror, Dock.Right);
            titleBar.Children.Add(_btnAbout);
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
            _btnInstall = MakeButton("安装 DSH", 96, Color.FromRgb(0, 150, 90));
            _btnStop = MakeButton("停止服务", 96, Color.FromRgb(198, 60, 60));
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
                AppendLog("启动器已就绪，正在检测环境...");
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
            string nodeVal = _nodeOk ? _nodeVersion : "未检测到";
            string npmVal = _npmOk ? _npmVersion : "未检测到";
            string dshVal = _dshGlobal ? "全局已装" : (_dshCached ? "已缓存" : "未安装");
            string svcVal = _serviceRunning ? "运行中" : "未运行";

            _dotNode.Fill = _nodeOk ? DotGreenBrush : DotRedBrush;
            _txtNode.Text = "Node.js: " + nodeVal;
            _dotNpm.Fill = _npmOk ? DotGreenBrush : DotRedBrush;
            _txtNpm.Text = "npm: " + npmVal;
            _dotDsh.Fill = (_dshGlobal || _dshCached) ? DotGreenBrush : DotRedBrush;
            _txtDsh.Text = "DSH: " + dshVal;
            _dotSvc.Fill = _serviceRunning ? DotGreenBrush : DotGrayBrush;
            _txtSvc.Text = "服务: " + svcVal;
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
                _btnInstall.Content = _dshGlobal ? "DSH 已加载" : "安装 DSH";
                _btnStop.IsEnabled = _serviceRunning;

                AppendLog("环境检测完成：Node=" + (_nodeOk ? "OK" : "缺失") +
                          "，npm=" + (_npmOk ? "OK" : "缺失") +
                          "，DSH=" + (_dshGlobal ? "全局" : (_dshCached ? "缓存" : "未安装")) +
                          "，DSH版本=" + (string.IsNullOrEmpty(_dshVersion) ? "未知" : _dshVersion) +
                          "，服务=" + (_serviceRunning ? "运行中" : "未运行"));

                if (_serviceRunning)
                    SetStatus("服务运行中，可打开浏览器 / 重启 / 停止");
                else if (_nodeOk && _npmOk)
                    SetStatus("环境就绪，可一键启动");
                else
                    SetStatus("请先安装 Node.js");

                if (!_nodeOk)
                {
                    MessageBoxResult r = MessageBox.Show(
                        "未检测到 Node.js，是否前往官方下载页？",
                        "提示", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (r == MessageBoxResult.Yes)
                        Process.Start("https://nodejs.org/zh-cn/download");
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
                AppendLog("DSH 正在运行，已打开浏览器。");
                return;
            }
            if (_serviceRunning)
            {
                OpenBrowser();
                AppendLog("服务已在运行，已打开浏览器。");
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
                AppendLog("未扫描到运行中的 DSH 进程。");
                SetStatus("未发现运行中的 DSH 服务");
                return;
            }

            AppendLog("扫描到 " + pids.Count + " 个 DSH 相关进程，开始强制停止...");
            ForceStopAll(pids);

            RunOnUi(delegate
            {
                _dshProcess = null;
                _serviceRunning = false;
                _btnStart.IsEnabled = true;
                _btnStop.IsEnabled = false;
                RefreshEnvCards();
            });
            SetStatus("已强制停止服务");
        }

        private void BtnBrowser_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser();
        }

        private void BtnMirror_Click(object sender, RoutedEventArgs e)
        {
            Thread t = new Thread(delegate()
            {
                string outp;
                int code = RunSync("cmd.exe", "/c npm config set registry https://registry.npmmirror.com", out outp);
                RunOnUi(delegate
                {
                    if (code == 0)
                    {
                        MessageBox.Show("已切换至国内镜像源：\r\nhttps://registry.npmmirror.com",
                            "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        AppendLog("npm 镜像源已切换为 https://registry.npmmirror.com");
                    }
                    else
                    {
                        MessageBox.Show("切换镜像源失败：" + outp, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            Thread t = new Thread(delegate()
            {
                string outp;
                int code = RunSync("cmd.exe", "/c npm view @deepseek-ai/dsh version", out outp);
                string latest = code == 0 ? outp.Trim() : "";
                RunOnUi(delegate
                {
                    if (string.IsNullOrEmpty(latest))
                    {
                        MessageBox.Show("获取最新版本失败，请检查网络。", "检查更新",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else if (string.IsNullOrEmpty(_dshVersion))
                    {
                        MessageBox.Show("最新版本：" + latest + "\r\n当前版本：未知", "检查更新",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (string.Equals(latest, _dshVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("已是最新版本（" + latest + "）", "检查更新",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("发现新版本 " + latest + "，当前 " + _dshVersion, "检查更新",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            });
            t.IsBackground = true;
            t.Start();
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

            string args = _dshGlobal
                ? "/c \"" + _dshCmd + "\" web"
                : "/c npx --yes " + PackageName + " web";
            AppendLog("启动命令：" + (_dshGlobal ? (_dshCmd + " web") : ("npx " + PackageName + " web")));

            var psi = BuildPsi("cmd.exe", args);
            Process p = null;
            try
            {
                p = new Process { StartInfo = psi };
                p.Start();
                ReadLinesUtf8Async(p.StandardOutput.BaseStream, delegate(string line) { CaptureOutput(CleanAnsi(line)); });
                ReadLinesUtf8Async(p.StandardError.BaseStream, delegate(string line) { CaptureOutput(CleanAnsi(line)); });
                _dshProcess = p;

                RunOnUi(delegate { _btnStop.IsEnabled = true; });
                AppendLog("DSH 进程已启动（PID " + p.Id + "），等待服务就绪...");
                SetStatus("DSH 启动中...");

                OpenBrowserWhenReady();

                p.WaitForExit();
                int code = p.ExitCode;
                AppendLog("DSH 进程已退出，退出码 " + code);

                RunOnUi(delegate
                {
                    _dshProcess = null;
                    _serviceRunning = false;
                    _btnStart.IsEnabled = true;
                    _btnStop.IsEnabled = false;
                    RefreshEnvCards();
                });
                SetStatus("服务已停止");
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
                SetStatus("正在重启 DSH...");

                List<int> pids = CollectDshPids();
                if (pids.Count > 0)
                {
                    AppendLog("重启：先强制停止当前服务（" + pids.Count + " 个进程）...");
                    ForceStopAll(pids);
                }
                else
                {
                    AppendLog("重启：当前无运行中的服务，直接启动...");
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

            AppendLog("重新启动服务...");
            StartDshThread();
        }

        private void InstallDshThread()
        {
            RunOnUi(delegate { _btnInstall.IsEnabled = false; });
            SetStatus("正在安装 DSH...");
            AppendLog("开始安装：npm install -g " + PackageName);

            var psi = BuildPsi("cmd.exe", "/c npm install -g " + PackageName);
            try
            {
                var p = new Process { StartInfo = psi };
                p.Start();
                ReadLinesUtf8Async(p.StandardOutput.BaseStream, delegate(string line) { AppendLog(CleanAnsi(line)); });
                ReadLinesUtf8Async(p.StandardError.BaseStream, delegate(string line) { AppendLog(CleanAnsi(line)); });
                p.WaitForExit();
                int code = p.ExitCode;

                if (code == 0)
                {
                    AppendLog("DSH 安装完成，正在重新检测环境...");
                    Thread t = new Thread(DetectEnvironment);
                    t.IsBackground = true;
                    t.Start();
                }
                else
                {
                    AppendLog("安装失败，退出码 " + code + "，请查看上方日志。");
                    RunOnUi(delegate { _btnInstall.IsEnabled = true; });
                    SetStatus("安装失败");
                }
            }
            catch (Exception ex)
            {
                AppendLog("安装出错：" + ex.Message);
                RunOnUi(delegate { _btnInstall.IsEnabled = true; });
                SetStatus("安装失败");
            }
        }

        private void OpenBrowserWhenReady()
        {
            for (int i = 0; i < 30; i++)
            {
                Thread.Sleep(1000);
                if (IsPortOpen(3080))
                {
                    AppendLog("服务已就绪，自动打开浏览器...");
                    OpenBrowser();
                    RunOnUi(delegate
                    {
                        _serviceRunning = true;
                        RefreshEnvCards();
                    });
                    SetStatus("服务运行中");
                    return;
                }
            }
            AppendLog("等待 30 秒后服务仍未就绪，请查看上方日志排查。");
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
                    AppendLog("已强制停止进程 PID " + id + "（含子进程）。");
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
            sb.AppendLine("原因：Cordis 服务名是全局唯一的，同名服务只能有一个提供者。");
            sb.AppendLine();
            sb.AppendLine("请选择卸载其中一方（卸载后需重启服务）：");
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
                Title = "DSH 启动失败：插件服务冲突",
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
                        "已成功卸载 " + package + "。\r\n\r\n是否立即重启服务？",
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
            var t = new Thread(delegate() { ReadLinesUtf8(stream, onLine); });
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
                    AppendLog("关闭启动器：正在强制停止 DSH 服务（" + pids.Count + " 个进程）...");
                    ForceStopAll(pids);
                }
                else if (choice == CloseChoice.CloseOnly)
                {
                    AppendLog("启动器已关闭，DSH 服务保持运行（" + pids.Count + " 个进程）。");
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
                Text = "检测到 DSH 服务仍在运行（" + pidCount + " 个进程）。\r\n\r\n请选择关闭方式：",
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

            var btnStop = MakeButton("停止并关闭", 110, Color.FromRgb(198, 60, 60));
            btnStop.Click += delegate { result = CloseChoice.StopAndClose; dlg.Close(); };
            btnPanel.Children.Add(btnStop);

            var btnCloseOnly = MakeButton("仅关闭窗口", 110, Color.FromRgb(0, 122, 204));
            btnCloseOnly.Click += delegate { result = CloseChoice.CloseOnly; dlg.Close(); };
            btnPanel.Children.Add(btnCloseOnly);

            var btnCancel = MakeButton("取消", 80, Color.FromRgb(88, 88, 96));
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
                        "DSH 一键启动器已经运行，请勿重复运行。",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var app = new Application();
                app.Run(new MainWindow());
            }
        }
    }
}
