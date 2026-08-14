using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Sockets;
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
        private string _npmPrefix = "";
        private string _npmCache = "";
        private string _dshCmd = "";

        private static readonly Regex AnsiRegex = new Regex("\x1b\\[[0-9;?]*[ -/]*[@-~]");

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

            // 标题
            var title = new TextBlock
            {
                Text = "DSH 一键启动器",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 17,
                FontWeight = FontWeights.Bold
            };
            Grid.SetRow(title, 0);
            root.Children.Add(title);

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

        private static Button MakeButton(string text, double width, Color normal)
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
                          "，服务=" + (_serviceRunning ? "运行中" : "未运行"));

                if (_serviceRunning)
                    SetStatus("服务运行中，可打开浏览器 / 重启 / 停止");
                else if (_nodeOk && _npmOk)
                    SetStatus("环境就绪，可一键启动");
                else
                    SetStatus("请先安装 Node.js");
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

        // ------------------------------------------------------------------
        // 启动 / 重启 / 安装（后台线程）
        // ------------------------------------------------------------------
        private void StartDshThread()
        {
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
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) AppendLog(CleanAnsi(e.Data));
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) AppendLog(CleanAnsi(e.Data));
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
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
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) AppendLog(CleanAnsi(e.Data));
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) AppendLog(CleanAnsi(e.Data));
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
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
        // 工具方法
        // ------------------------------------------------------------------
        private static int RunSync(string fileName, string args, out string output)
        {
            output = "";
            try
            {
                var psi = BuildPsi(fileName, args);
                using (var p = new Process())
                {
                    p.StartInfo = psi;
                    p.Start();
                    string o = p.StandardOutput.ReadToEnd();
                    string e = p.StandardError.ReadToEnd();
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

        private static ProcessStartInfo BuildPsi(string fileName, string args)
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

        protected override void OnClosing(CancelEventArgs e)
        {
            List<int> pids = CollectDshPids();
            if (pids.Count > 0)
            {
                MessageBoxResult r = MessageBox.Show(
                    "检测到 DSH 服务仍在运行（" + pids.Count + " 个进程）。\r\n\r\n关闭启动器将同时强制停止服务。\r\n确定要关闭吗？",
                    "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r == MessageBoxResult.Yes)
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
                        }
                        catch { }
                    }
                }
                else
                {
                    e.Cancel = true;
                }
            }
            base.OnClosing(e);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            var app = new Application();
            app.Run(new MainWindow());
        }
    }
}
