#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Xml.Serialization;
using System.Xml;
using NinjaTrader.Cbi;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

using WpfLine      = System.Windows.Shapes.Line;
using WpfEllipse   = System.Windows.Shapes.Ellipse;
using WpfRectangle = System.Windows.Shapes.Rectangle;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.RedTail
{
    public class RedTailToolbar : Indicator
    {
        #region Private Fields

        private Chart                       chartWindow;
        private Grid                        chartGrid;
        private Grid                        toolbarGrid;
        private int                         insertedRow = -1;
        private bool                        toolbarInstalled;

        private StackPanel                  toolButtonPanel;
        private bool                        drawingsVisible = true;
        private bool                        drawingsLocked  = false;

        private DispatcherTimer             lagTimer;
        private TextBlock                   lagTimerText;
        private double                      lagSec;
        private int                         clickCount = 0;

        // ATR
        private TextBlock                   atrText;
        private NinjaTrader.NinjaScript.Indicators.ATR atrIndicator;

        // Break Even
        private Button                      beButton;
        private static readonly Brush       BeBrush = FB(0, 200, 120);

        // Pan Mode
        private Button                      panButton;
        private bool                        panMode = false;
        private bool                        panDragging = false;
        private static readonly Brush       PanBrush = FB(255, 193, 7);

        // P/Invoke for simulating Ctrl key
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private const byte VK_CONTROL   = 0x11;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP   = 0x0002;

        // P/Invoke for screenshot (captures DX content via screen copy)
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, uint rop);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
        private const uint SRCCOPY = 0x00CC0020;

        // NT Menu items
        private Dictionary<string, MenuItem> ntDrawingMenuItems;
        private MenuItem                    ntHideAllMenuItem;
        private MenuItem                    ntRemoveAllMenuItem;

        // Settings
        private List<string>                allToolHeaders;      // all available from NT menu
        private HashSet<string>             enabledTools;        // user's selection
        private static readonly string      SettingsFile = Path.Combine(
            NinjaTrader.Core.Globals.UserDataDir, "RedTailToolbarSettings.txt");

        // Frozen Brushes
        private static readonly Brush   ActiveBrush     = Brushes.DodgerBlue;
        private static readonly Brush   InactiveBrush   = FB(180, 180, 180);
        private static readonly Brush   DangerBrush     = FB(220, 60, 60);
        private static readonly Brush   ToolbarBg       = FB(30, 30, 30);
        private static readonly Brush   ButtonHoverBg   = FB(55, 55, 55);
        private static readonly Brush   ButtonBg        = Brushes.Transparent;
        private static readonly Brush   SepBrush        = FB(70, 70, 70);
        private static readonly Brush   SettingsBg      = FB(40, 40, 40);
        private static readonly Brush   SettingsBorder  = FB(80, 80, 80);

        private static SolidColorBrush FB(byte r, byte g, byte b)
        { var br = new SolidColorBrush(Color.FromRgb(r, g, b)); br.Freeze(); return br; }
        private static SolidColorBrush FB(byte a, byte r, byte g, byte b)
        { var br = new SolidColorBrush(Color.FromArgb(a, r, g, b)); br.Freeze(); return br; }

        // Icon color mapping by category
        private static readonly Brush IcoLine   = FB(0, 188, 212);
        private static readonly Brush IcoShape  = FB(255, 193, 7);
        private static readonly Brush IcoFib    = FB(156, 39, 176);
        private static readonly Brush IcoText   = FB(200, 200, 200);
        private static readonly Brush IcoCustom = FB(255, 120, 50);
        private static readonly Brush IcoDefault = FB(150, 150, 150);
        private static readonly Brush IcoIndicator = FB(0, 200, 120);

        // Indicator Visibility
        private static readonly string IndSettingsFile = Path.Combine(
            NinjaTrader.Core.Globals.UserDataDir, "RedTailIndicatorVisibility.txt");
        private HashSet<string> hiddenIndicators;

        // Command Center
        private static readonly string TemplateDir = Path.Combine(
            NinjaTrader.Core.Globals.UserDataDir, "templates", "Indicator");

        // Screenshot
        private Button                      screenshotButton;

        // Timeframe Switcher
        private StackPanel                  tfPanel;
        private FrameworkElement            cachedIntervalSelector;

        #endregion

        #region Lifecycle

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description             = "RedTail Toolbar - Quick access for drawing tools and chart utilities.";
                Name                    = "RedTail Toolbar";
                Calculate               = Calculate.OnBarClose;
                IsOverlay               = true;
                DisplayInDataBox        = false;
                DrawOnPricePanel        = false;
                IsSuspendedWhileInactive = false;
                PaintPriceMarkers       = false;

                ToolbarHeight           = 30;
                BtnSize                 = 26;
                ShowLagTimer            = true;
                LagWarningSec           = 0.5;
                LagCriticalSec          = 2.0;
                ShowATR                 = true;
                AtrPeriod               = 14;
                ShowBreakEven           = true;
                BreakEvenTicks          = 0;
                ShowPanButton           = true;
                ShowIndicatorManager    = true;
                ShowCommandCenter      = true;
                ShowScreenshot         = true;
                ScreenshotFolder       = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RedTail Screenshots");
                ShowTimeframeSwitcher  = true;
                TimeframeList          = "1,3,5,15,60";
            }
            else if (State == State.DataLoaded)
            {
                if (ShowATR)
                    atrIndicator = ATR(AtrPeriod);

                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            CacheNTMenuItems();
                            LoadSettings();
                            LoadIndicatorVisibility();
                            InstallToolbar();
                        }
                        catch (Exception ex) { Print("RT Init Error: " + ex.Message + "\n" + ex.StackTrace); }
                    });
                }
            }
            else if (State == State.Terminated)
            {
                if (lagTimer != null) { lagTimer.Stop(); lagTimer = null; }
                // Ensure Ctrl key is released if pan mode was active
                if (panDragging || panMode)
                {
                    try { keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); } catch { }
                    panDragging = false;
                    panMode = false;
                }
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            RemovePanHandlers();
                            RemoveToolbar();
                        }
                        catch (Exception ex) { Print("RT Remove Error: " + ex.Message); }
                    });
                }
            }
        }

        protected override void OnBarUpdate()
        {
            if (ShowATR && atrIndicator != null && atrText != null && CurrentBar >= AtrPeriod)
            {
                double atrVal = atrIndicator[0];
                ChartControl?.Dispatcher?.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                {
                    try
                    {
                        if (atrText != null)
                            atrText.Text = atrVal < 1 ? atrVal.ToString("F4") : atrVal < 10 ? atrVal.ToString("F3") : atrVal.ToString("F2");
                    }
                    catch { }
                }));
            }
        }
        private DateTime lastMarketDataTime = DateTime.MinValue;

        protected override void OnMarketData(MarketDataEventArgs e)
        {
            if (ShowLagTimer && State == State.Realtime)
            {
                lastMarketDataTime = DateTime.Now;
                // e.Time may be exchange local time, try both and use the smaller delta
                double deltaUtc = Math.Abs((DateTime.UtcNow - e.Time).TotalSeconds);
                double deltaLocal = Math.Abs((DateTime.Now - e.Time).TotalSeconds);
                lagSec = Math.Min(deltaUtc, deltaLocal);
            }
        }
        protected override void OnRender(ChartControl cc, ChartScale cs) { }

        #endregion

        #region Cache NT Drawing Menu Items

        private void CacheNTMenuItems()
        {
            ntDrawingMenuItems = new Dictionary<string, MenuItem>(StringComparer.OrdinalIgnoreCase);
            allToolHeaders = new List<string>();

            chartWindow = Window.GetWindow(ChartControl) as Chart;
            if (chartWindow == null) return;

            // Find the Drawing Tool menu
            var allMenuItems = FindVisualChildren<MenuItem>(chartWindow);
            MenuItem drawingToolMenu = null;
            foreach (var mi in allMenuItems)
            {
                if ((mi.ToolTip?.ToString() ?? "") == "Drawing Tool")
                { drawingToolMenu = mi; break; }
            }

            if (drawingToolMenu == null) { Print("RT: Drawing Tool menu not found!"); return; }

            // Force submenu generation
            drawingToolMenu.IsSubmenuOpen = true;
            drawingToolMenu.UpdateLayout();
            drawingToolMenu.IsSubmenuOpen = false;

            for (int i = 0; i < drawingToolMenu.Items.Count; i++)
            {
                if (drawingToolMenu.Items[i] is MenuItem subMi)
                {
                    string header = subMi.Header?.ToString() ?? "";
                    if (string.IsNullOrEmpty(header)) continue;

                    // Capture special utility items
                    if (header == "Hide All Drawing Objects") { ntHideAllMenuItem = subMi; continue; }
                    if (header == "Remove All Drawing Objects") { ntRemoveAllMenuItem = subMi; continue; }
                    if (header == "Drawing Objects..." || header == "Snap Mode" || header == "Stay In Draw Mode") continue;

                    if (!ntDrawingMenuItems.ContainsKey(header))
                    {
                        ntDrawingMenuItems[header] = subMi;
                        allToolHeaders.Add(header);
                    }
                }
            }

            Print("RT: Cached " + ntDrawingMenuItems.Count + " drawing tools from NT menu");

            // Sort: RedTail first, then by category priority
            allToolHeaders.Sort((a, b) =>
            {
                int pa = GetCategoryPriority(GetToolCategory(a));
                int pb = GetCategoryPriority(GetToolCategory(b));
                if (pa != pb) return pa.CompareTo(pb);
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static int GetCategoryPriority(string category)
        {
            switch (category)
            {
                case "RedTail Custom":      return 0;
                case "Lines":               return 1;
                case "Fibonacci":           return 2;
                case "Shapes":              return 3;
                case "Annotation":          return 4;
                case "Channels & Advanced": return 5;
                case "Order Flow":          return 6;
                case "Elliott Wave":        return 7;
                case "TDU Custom":          return 8;
                case "Other":               return 9;
                default:                    return 10;
            }
        }

        #endregion

        #region Settings Persistence

        private void LoadSettings()
        {
            enabledTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(SettingsFile))
            {
                try
                {
                    var lines = File.ReadAllLines(SettingsFile);
                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            enabledTools.Add(trimmed);
                    }
                    Print("RT: Loaded " + enabledTools.Count + " tools from settings");
                }
                catch (Exception ex) { Print("RT: Error loading settings: " + ex.Message); }
            }

            // Default set if no settings file
            if (enabledTools.Count == 0)
            {
                enabledTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Horizontal Line", "Vertical Line", "Line", "Ray", "Extended Line",
                    "Rectangle", "Region Highlight X", "Region Highlight Y",
                    "Fibonacci Retracements", "Fibonacci Extensions", "Text"
                };

                // Auto-add any RedTail tools
                foreach (var h in allToolHeaders)
                {
                    if (h.StartsWith("RedTail", StringComparison.OrdinalIgnoreCase))
                        enabledTools.Add(h);
                }

                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            try
            {
                File.WriteAllLines(SettingsFile, enabledTools.ToArray());
                Print("RT: Saved " + enabledTools.Count + " tools to settings");
            }
            catch (Exception ex) { Print("RT: Error saving settings: " + ex.Message); }
        }

        #endregion

        #region Install / Remove Toolbar

        private void InstallToolbar()
        {
            if (toolbarInstalled) return;

            if (chartWindow == null)
                chartWindow = Window.GetWindow(ChartControl) as Chart;
            if (chartWindow == null) return;

            if (chartWindow.MainTabControl == null) return;
            Grid tabGrid = chartWindow.MainTabControl.Parent as Grid;
            if (tabGrid == null) return;

            DependencyObject tabGridParent = VisualTreeHelper.GetParent(tabGrid);

            // Find outerGrid
            Grid outerGrid = null;
            DependencyObject current = tabGridParent;
            while (current != null)
            {
                if (current is Grid g && g.Name == "outerGrid") { outerGrid = g; break; }
                current = VisualTreeHelper.GetParent(current);
            }

            if (outerGrid == null) { Print("RT: outerGrid not found"); return; }

            // Find inner grid inside MainTabControl
            Grid innerGrid = FindNamedGrid(chartWindow.MainTabControl, null, 3);
            if (innerGrid == null) { Print("RT: inner grid not found"); return; }

            // Build toolbar
            toolbarGrid = BuildToolbar();

            // Insert at row 0
            insertedRow = 0;
            innerGrid.RowDefinitions.Insert(insertedRow, new RowDefinition
            {
                Height = new GridLength(ToolbarHeight, GridUnitType.Pixel)
            });

            foreach (UIElement child in innerGrid.Children)
            {
                int row = Grid.GetRow(child);
                if (row >= insertedRow)
                    Grid.SetRow(child, row + 1);
            }

            Grid.SetRow(toolbarGrid, insertedRow);
            Grid.SetColumnSpan(toolbarGrid, Math.Max(1, innerGrid.ColumnDefinitions.Count));
            innerGrid.Children.Add(toolbarGrid);

            chartGrid = innerGrid;
            toolbarInstalled = true;
            Print("RT: Toolbar installed");

            if (ShowLagTimer)
            {
                lagTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(250)
                };
                lagTimer.Tick += LagTimer_Tick;
                lagTimer.Start();
            }
        }

        private void RemoveToolbar()
        {
            if (!toolbarInstalled || chartGrid == null || toolbarGrid == null) return;
            try
            {
                int row = insertedRow;
                chartGrid.Children.Remove(toolbarGrid);
                if (row >= 0 && row < chartGrid.RowDefinitions.Count)
                {
                    chartGrid.RowDefinitions.RemoveAt(row);
                    foreach (UIElement child in chartGrid.Children)
                    {
                        int r = Grid.GetRow(child);
                        if (r > row) Grid.SetRow(child, r - 1);
                    }
                }
                toolbarGrid = null; chartGrid = null; insertedRow = -1; toolbarInstalled = false;
                Print("RT: Toolbar removed");
            }
            catch (Exception ex) { Print("RT Remove Error: " + ex.Message); }
        }

        #endregion

        #region Build Toolbar

        private Grid BuildToolbar()
        {
            var grid = new Grid
            {
                Height = ToolbarHeight,
                Background = ToolbarBg,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var border = new Border
            {
                BorderBrush = SepBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = BuildContent()
            };
            grid.Children.Add(border);
            return grid;
        }

        private StackPanel BuildContent()
        {
            var p = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0)
            };

            // Brand - RedTail Hawk icon
            p.Children.Add(MakeHawkIcon(ToolbarHeight - 6));
            AddSep(p);

            // Dynamic tool buttons
            toolButtonPanel = new StackPanel { Orientation = Orientation.Horizontal };
            RebuildToolButtons();
            p.Children.Add(toolButtonPanel);

            AddSep(p);

            // Settings cog
            var cog = MakeUtilBtn("⚙", "Toolbar Settings", ShowSettings);
            cog.Foreground = InactiveBrush;
            p.Children.Add(cog);

            // Indicator visibility manager
            if (ShowIndicatorManager)
            {
                var indBtn = new Button
                {
                    Width = BtnSize + 4, Height = BtnSize - 4, Background = ButtonBg,
                    BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand, ToolTip = "Show/Hide Indicators",
                    Margin = new Thickness(2, 0, 2, 0), Padding = new Thickness(2),
                    Content = MakeLayersIcon(BtnSize - 10),
                    Style = FlatStyle()
                };
                indBtn.Click      += ShowIndicatorManagerPopup;
                indBtn.MouseEnter += (s, e) => indBtn.Background = ButtonHoverBg;
                indBtn.MouseLeave += (s, e) => indBtn.Background = ButtonBg;
                p.Children.Add(indBtn);
            }

            // Command Center
            if (ShowCommandCenter)
            {
                var ccBtn = new Button
                {
                    Width = BtnSize + 4, Height = BtnSize - 4, Background = ButtonBg,
                    BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand, ToolTip = "RedTail Command Center",
                    Margin = new Thickness(2, 0, 2, 0), Padding = new Thickness(2),
                    Content = MakeCommandCenterIcon(BtnSize - 10),
                    Style = FlatStyle()
                };
                ccBtn.Click      += ShowCommandCenter_Click;
                ccBtn.MouseEnter += (s, e) => ccBtn.Background = ButtonHoverBg;
                ccBtn.MouseLeave += (s, e) => ccBtn.Background = ButtonBg;
                p.Children.Add(ccBtn);
            }

            AddSep(p);

            // Visibility
            var vis = MakeUtilBtn("👁", "Hide/Show All Drawings", ToggleVisibility);
            p.Children.Add(vis);

            // Lock
            var lck = MakeUtilBtn("🔓", "Lock/Unlock All Drawings", ToggleLock);
            p.Children.Add(lck);

            // Delete
            var del = MakeUtilBtn("🗑", "Delete All Drawings", DeleteAll);
            del.Foreground = DangerBrush;
            p.Children.Add(del);

            AddSep(p);

            // Lag
            if (ShowLagTimer)
            {
                p.Children.Add(new TextBlock
                {
                    Text = "LAG:", FontSize = 10, Foreground = InactiveBrush,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 3, 0)
                });
                lagTimerText = new TextBlock
                {
                    Text = "...", FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Foreground = InactiveBrush, VerticalAlignment = VerticalAlignment.Center, MinWidth = 36
                };
                p.Children.Add(lagTimerText);
            }

            // ATR
            if (ShowATR)
            {
                AddSep(p);
                p.Children.Add(new TextBlock
                {
                    Text = "ATR(" + AtrPeriod + "):", FontSize = 10, Foreground = InactiveBrush,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 3, 0)
                });
                atrText = new TextBlock
                {
                    Text = "...", FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.DodgerBlue, VerticalAlignment = VerticalAlignment.Center, MinWidth = 36
                };
                p.Children.Add(atrText);
            }

            // Break Even
            if (ShowBreakEven)
            {
                AddSep(p);
                string beTip = BreakEvenTicks == 0
                    ? "Move stop to Break Even"
                    : "Move stop to Break Even + " + BreakEvenTicks + " tick" + (BreakEvenTicks == 1 ? "" : "s");
                string beLabel = BreakEvenTicks == 0 ? "BE" : "BE+" + BreakEvenTicks;

                beButton = new Button
                {
                    Width = BtnSize + 12, Height = BtnSize - 4, Background = ButtonBg,
                    BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand, ToolTip = beTip, Margin = new Thickness(2, 0, 2, 0),
                    FontSize = 10, FontWeight = FontWeights.Bold, Content = beLabel,
                    Foreground = BeBrush,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = FlatStyle()
                };
                beButton.Click      += BreakEvenClick;
                beButton.MouseEnter += (s, e) => beButton.Background = ButtonHoverBg;
                beButton.MouseLeave += (s, e) => beButton.Background = ButtonBg;
                p.Children.Add(beButton);
            }

            // Pan Mode
            if (ShowPanButton)
            {
                AddSep(p);
                panButton = new Button
                {
                    Width = BtnSize + 4, Height = BtnSize - 4, Background = ButtonBg,
                    BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand, ToolTip = "Toggle Pan Mode (free chart drag)",
                    Margin = new Thickness(2, 0, 2, 0),
                    FontSize = 14, Content = "✋",
                    Foreground = InactiveBrush,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = FlatStyle()
                };
                panButton.Click      += TogglePanMode;
                panButton.MouseEnter += (s, e) => panButton.Background = ButtonHoverBg;
                panButton.MouseLeave += (s, e) => panButton.Background = panMode ? FB(50, 255, 193, 7) : ButtonBg;
                p.Children.Add(panButton);
            }

            // Screenshot
            if (ShowScreenshot)
            {
                AddSep(p);
                screenshotButton = new Button
                {
                    Width = BtnSize + 4, Height = BtnSize - 4, Background = ButtonBg,
                    BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand, ToolTip = "Screenshot Chart",
                    Margin = new Thickness(2, 0, 2, 0), Padding = new Thickness(2),
                    Content = MakeScreenshotIcon(BtnSize - 10),
                    Style = FlatStyle()
                };
                screenshotButton.Click      += ScreenshotClick;
                screenshotButton.MouseEnter += (s, e) => screenshotButton.Background = ButtonHoverBg;
                screenshotButton.MouseLeave += (s, e) => screenshotButton.Background = ButtonBg;
                p.Children.Add(screenshotButton);
            }

            // Timeframe Switcher
            if (ShowTimeframeSwitcher)
            {
                AddSep(p);
                tfPanel = new StackPanel { Orientation = Orientation.Horizontal };
                BuildTimeframeButtons();
                p.Children.Add(tfPanel);
            }

            return p;
        }

        private void RebuildToolButtons()
        {
            if (toolButtonPanel == null) return;
            toolButtonPanel.Children.Clear();

            foreach (var header in allToolHeaders)
            {
                if (!enabledTools.Contains(header)) continue;

                var btn = new Button
                {
                    Width = BtnSize, Height = BtnSize - 4, Background = ButtonBg,
                    BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand, ToolTip = header,
                    Margin = new Thickness(1, 0, 1, 0), Padding = new Thickness(2),
                    Content = MakeIcon(header, BtnSize - 10),
                    Style = FlatStyle()
                };

                string h = header; // capture for lambda
                btn.Click      += (s, e) => ActivateTool(h);
                btn.MouseEnter += (s, e) => btn.Background = ButtonHoverBg;
                btn.MouseLeave += (s, e) => btn.Background = ButtonBg;

                toolButtonPanel.Children.Add(btn);
            }
        }

        #endregion

        #region Settings Panel

        private void ShowSettings(object sender, RoutedEventArgs e)
        {
            // Create a popup window with checkboxes for each tool
            var settingsWindow = new Window
            {
                Title = "RedTail Toolbar Settings",
                Width = 350,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = SettingsBg,
                ResizeMode = ResizeMode.CanResize,
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45, GridUnitType.Pixel) });

            // Scrollable checkbox list
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10, 10, 10, 0)
            };

            var stack = new StackPanel();
            var checkBoxes = new Dictionary<string, CheckBox>();

            // Header
            stack.Children.Add(new TextBlock
            {
                Text = "Select Drawing Tools for Toolbar:",
                FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10), FontSize = 13
            });

            // Select All / Deselect All
            var selectPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var selectAll = new Button { Content = "Select All", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 2, 8, 2) };
            var deselectAll = new Button { Content = "Deselect All", Padding = new Thickness(8, 2, 8, 2) };

            selectAll.Click += (s, ev) => { foreach (var cb in checkBoxes.Values) cb.IsChecked = true; };
            deselectAll.Click += (s, ev) => { foreach (var cb in checkBoxes.Values) cb.IsChecked = false; };

            selectPanel.Children.Add(selectAll);
            selectPanel.Children.Add(deselectAll);
            stack.Children.Add(selectPanel);

            // Group tools by category, ordered by priority
            string[] categoryOrder = {
                "RedTail Custom", "Lines", "Fibonacci", "Shapes", "Annotation",
                "Channels & Advanced", "Order Flow", "Elliott Wave", "TDU Custom", "Other"
            };

            foreach (string cat in categoryOrder)
            {
                var toolsInCat = allToolHeaders.Where(h => GetToolCategory(h) == cat).ToList();
                if (toolsInCat.Count == 0) continue;

                stack.Children.Add(new TextBlock
                {
                    Text = cat,
                    FontWeight = FontWeights.SemiBold, Foreground = GetCategoryColor(cat),
                    Margin = new Thickness(0, 8, 0, 4), FontSize = 12
                });

                foreach (var header in toolsInCat)
                {
                    var cb = new CheckBox
                    {
                        Content = header,
                        IsChecked = enabledTools.Contains(header),
                        Foreground = Brushes.White,
                        Margin = new Thickness(8, 2, 0, 2)
                    };
                    checkBoxes[header] = cb;
                    stack.Children.Add(cb);
                }
            }

            scroll.Content = stack;
            Grid.SetRow(scroll, 0);
            mainGrid.Children.Add(scroll);

            // OK / Cancel buttons
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var okBtn = new Button
            {
                Content = "OK", Width = 80, Height = 28, Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            var cancelBtn = new Button
            {
                Content = "Cancel", Width = 80, Height = 28,
                IsCancel = true
            };

            okBtn.Click += (s, ev) =>
            {
                enabledTools.Clear();
                foreach (var kvp in checkBoxes)
                {
                    if (kvp.Value.IsChecked == true)
                        enabledTools.Add(kvp.Key);
                }
                SaveSettings();
                RebuildToolButtons();
                settingsWindow.Close();
            };

            cancelBtn.Click += (s, ev) => settingsWindow.Close();

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);

            Grid.SetRow(btnPanel, 1);
            mainGrid.Children.Add(btnPanel);

            settingsWindow.Content = mainGrid;
            settingsWindow.Show();
        }

        private string GetToolCategory(string header)
        {
            if (header.StartsWith("RedTail", StringComparison.OrdinalIgnoreCase)) return "RedTail Custom";
            if (header.Contains("Line") || header == "Ray" || header == "Path" || header.Contains("Arrow")) return "Lines";
            if (header.Contains("Fibonacci")) return "Fibonacci";
            if (header.Contains("Region") || header.Contains("Rectangle") || header.Contains("Ellipse")
                || header.Contains("Triangle") || header.Contains("Polygon") || header.Contains("Arc")) return "Shapes";
            if (header.Contains("Channel") || header.Contains("Pitchfork") || header.Contains("Gann")
                || header.Contains("Regression") || header.Contains("Trend")) return "Channels & Advanced";
            if (header.Contains("Text") || header.Contains("Marker") || header.Contains("Ruler")
                || header.Contains("Risk")) return "Annotation";
            if (header.Contains("Order Flow") || header.Contains("VWAP") || header.Contains("Volume")) return "Order Flow";
            if (header.Contains("Elliott") || header.Contains("Wave")) return "Elliott Wave";
            if (header.Contains("TDU") || header.Contains("TDu")) return "TDU Custom";
            return "Other";
        }

        private Brush GetCategoryColor(string category)
        {
            switch (category)
            {
                case "RedTail Custom": return DangerBrush;
                case "Lines": return IcoLine;
                case "Fibonacci": return IcoFib;
                case "Shapes": return IcoShape;
                case "Channels & Advanced": return FB(100, 200, 255);
                case "Annotation": return IcoText;
                case "Order Flow": return FB(76, 175, 80);
                case "TDU Custom": return FB(255, 200, 50);
                default: return IcoDefault;
            }
        }

        #endregion

        #region Button / Icon Factories

        private Button MakeUtilBtn(string content, string tip, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Width = BtnSize + 4, Height = BtnSize - 4, Background = ButtonBg,
                BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, ToolTip = tip, Margin = new Thickness(2, 0, 2, 0),
                FontSize = 14, Content = content, Foreground = InactiveBrush,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Style = FlatStyle()
            };
            btn.Click      += handler;
            btn.MouseEnter += (s, e) => btn.Background = ButtonHoverBg;
            btn.MouseLeave += (s, e) => btn.Background = ButtonBg;
            return btn;
        }

        private Style FlatStyle()
        {
            var s = new Style(typeof(Button));
            var t = new ControlTemplate(typeof(Button));
            var b = new FrameworkElementFactory(typeof(Border));
            b.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            b.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            b.SetValue(Border.PaddingProperty, new Thickness(2));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            b.AppendChild(cp);
            t.VisualTree = b;
            s.Setters.Add(new Setter(Control.TemplateProperty, t));
            return s;
        }

        private void AddSep(StackPanel p)
        {
            p.Children.Add(new Border
            {
                Width = 1, Height = ToolbarHeight - 10, Background = SepBrush,
                Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center
            });
        }

        #endregion

        #region Icon Rendering

        private Canvas MakeHawkIcon(double sz)
        {
            var c = new Canvas
            {
                Width = sz + 4,
                Height = sz,
                Margin = new Thickness(3, 0, 2, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            Brush hawk = DangerBrush;
            double cx = (sz + 4) / 2;  // center x
            double cy = sz / 2;        // center y
            double s = sz / 24.0;      // scale factor

            // === BODY (central torso) ===
            c.Children.Add(Ln(cx, cy - 6*s, cx, cy + 4*s, hawk, 2.2));

            // === HEAD ===
            // Skull - small oval shape
            c.Children.Add(Ln(cx - 1.5*s, cy - 7*s, cx, cy - 9*s, hawk, 2));
            c.Children.Add(Ln(cx, cy - 9*s, cx + 1.5*s, cy - 7*s, hawk, 2));
            // Beak - sharp pointed
            c.Children.Add(Ln(cx, cy - 8.5*s, cx, cy - 10.5*s, hawk, 1.5));
            c.Children.Add(Ln(cx, cy - 10.5*s, cx + 1*s, cy - 8.5*s, hawk, 1));

            // === LEFT WING (spread wide) ===
            // Upper wing edge - sweeps out and up
            c.Children.Add(Ln(cx - 1*s, cy - 5*s, cx - 5*s, cy - 8*s, hawk, 1.8));
            c.Children.Add(Ln(cx - 5*s, cy - 8*s, cx - 9*s, cy - 9*s, hawk, 1.8));
            c.Children.Add(Ln(cx - 9*s, cy - 9*s, cx - 12*s, cy - 8*s, hawk, 1.5));
            // Wing tip feathers (jagged edge)
            c.Children.Add(Ln(cx - 12*s, cy - 8*s, cx - 11.5*s, cy - 6.5*s, hawk, 1.2));
            c.Children.Add(Ln(cx - 11.5*s, cy - 6.5*s, cx - 10.5*s, cy - 6*s, hawk, 1.2));
            c.Children.Add(Ln(cx - 10.5*s, cy - 6*s, cx - 9.5*s, cy - 5*s, hawk, 1.2));
            // Lower wing edge - connects back to body
            c.Children.Add(Ln(cx - 9.5*s, cy - 5*s, cx - 6*s, cy - 3*s, hawk, 1.5));
            c.Children.Add(Ln(cx - 6*s, cy - 3*s, cx - 2*s, cy - 1*s, hawk, 1.5));

            // === RIGHT WING (mirror) ===
            c.Children.Add(Ln(cx + 1*s, cy - 5*s, cx + 5*s, cy - 8*s, hawk, 1.8));
            c.Children.Add(Ln(cx + 5*s, cy - 8*s, cx + 9*s, cy - 9*s, hawk, 1.8));
            c.Children.Add(Ln(cx + 9*s, cy - 9*s, cx + 12*s, cy - 8*s, hawk, 1.5));
            // Wing tip feathers
            c.Children.Add(Ln(cx + 12*s, cy - 8*s, cx + 11.5*s, cy - 6.5*s, hawk, 1.2));
            c.Children.Add(Ln(cx + 11.5*s, cy - 6.5*s, cx + 10.5*s, cy - 6*s, hawk, 1.2));
            c.Children.Add(Ln(cx + 10.5*s, cy - 6*s, cx + 9.5*s, cy - 5*s, hawk, 1.2));
            // Lower wing edge
            c.Children.Add(Ln(cx + 9.5*s, cy - 5*s, cx + 6*s, cy - 3*s, hawk, 1.5));
            c.Children.Add(Ln(cx + 6*s, cy - 3*s, cx + 2*s, cy - 1*s, hawk, 1.5));

            // === TAIL FEATHERS (fanned out) ===
            c.Children.Add(Ln(cx, cy + 4*s, cx - 3*s, cy + 8*s, hawk, 1.3));
            c.Children.Add(Ln(cx, cy + 4*s, cx - 1*s, cy + 9*s, hawk, 1.3));
            c.Children.Add(Ln(cx, cy + 4*s, cx + 1*s, cy + 9*s, hawk, 1.3));
            c.Children.Add(Ln(cx, cy + 4*s, cx + 3*s, cy + 8*s, hawk, 1.3));

            // === LEFT LEG & TALONS ===
            // Leg
            c.Children.Add(Ln(cx - 1.5*s, cy + 3*s, cx - 3*s, cy + 6*s, hawk, 1.3));
            // Talons - 3 forward claws spread out
            c.Children.Add(Ln(cx - 3*s, cy + 6*s, cx - 5*s, cy + 7.5*s, hawk, 1.2));
            c.Children.Add(Ln(cx - 3*s, cy + 6*s, cx - 3.5*s, cy + 8*s, hawk, 1.2));
            c.Children.Add(Ln(cx - 3*s, cy + 6*s, cx - 1.5*s, cy + 7.5*s, hawk, 1.2));
            // Talon tips (tiny hooks)
            c.Children.Add(Ln(cx - 5*s, cy + 7.5*s, cx - 5.5*s, cy + 7*s, hawk, 0.8));
            c.Children.Add(Ln(cx - 3.5*s, cy + 8*s, cx - 4*s, cy + 7.5*s, hawk, 0.8));
            c.Children.Add(Ln(cx - 1.5*s, cy + 7.5*s, cx - 1*s, cy + 7*s, hawk, 0.8));

            // === RIGHT LEG & TALONS ===
            c.Children.Add(Ln(cx + 1.5*s, cy + 3*s, cx + 3*s, cy + 6*s, hawk, 1.3));
            c.Children.Add(Ln(cx + 3*s, cy + 6*s, cx + 5*s, cy + 7.5*s, hawk, 1.2));
            c.Children.Add(Ln(cx + 3*s, cy + 6*s, cx + 3.5*s, cy + 8*s, hawk, 1.2));
            c.Children.Add(Ln(cx + 3*s, cy + 6*s, cx + 1.5*s, cy + 7.5*s, hawk, 1.2));
            c.Children.Add(Ln(cx + 5*s, cy + 7.5*s, cx + 5.5*s, cy + 7*s, hawk, 0.8));
            c.Children.Add(Ln(cx + 3.5*s, cy + 8*s, cx + 4*s, cy + 7.5*s, hawk, 0.8));
            c.Children.Add(Ln(cx + 1.5*s, cy + 7.5*s, cx + 1*s, cy + 7*s, hawk, 0.8));

            return c;
        }

        private Brush GetIconColor(string header)
        {
            if (header.StartsWith("RedTail", StringComparison.OrdinalIgnoreCase)) return IcoCustom;
            if (header.Contains("Line") || header == "Ray" || header == "Path" || header.Contains("Arrow")) return IcoLine;
            if (header.Contains("Fibonacci")) return IcoFib;
            if (header.Contains("Region") || header.Contains("Rectangle") || header.Contains("Ellipse")
                || header.Contains("Triangle") || header.Contains("Polygon")) return IcoShape;
            if (header.Contains("Text") || header.Contains("Marker")) return IcoText;
            return IcoDefault;
        }

        private string GetIconType(string header)
        {
            string h = header.ToLower();

            // Lines
            if (h == "horizontal line" || h == "labeled horizontal line") return "hline";
            if (h == "vertical line" || h == "labeled vertical line") return "vline";
            if (h == "line" || h == "labeled line") return "line";
            if (h == "ray" || h == "labeled ray") return "ray";
            if (h == "extended line" || h == "labeled extended line") return "extline";
            if (h == "arrow line" || h == "labeled arrow line") return "arrowline";
            if (h == "path") return "path";

            // Shapes
            if (h == "rectangle") return "rect";
            if (h == "region highlight x") return "regionx";
            if (h == "region highlight y") return "regiony";
            if (h == "ellipse") return "ellipse";
            if (h == "triangle") return "triangle";
            if (h == "polygon") return "polygon";
            if (h == "arc") return "arc";

            // Fibonacci
            if (h == "fibonacci retracements") return "fib";
            if (h == "fibonacci extensions") return "fibext";
            if (h == "fibonacci time extensions") return "fibtimeext";
            if (h == "fibonacci circle") return "fibcircle";

            // Channels & Advanced
            if (h == "andrew's pitchfork") return "pitchfork";
            if (h == "gann fan") return "gannfan";
            if (h == "regression channel") return "regchannel";
            if (h == "trend channel") return "channel";
            if (h == "time cycles") return "timecycles";

            // Annotation
            if (h == "text") return "text";
            if (h == "chart marker") return "marker";
            if (h == "ruler") return "ruler";
            if (h == "risk-reward") return "riskreward";

            // Order Flow
            if (h.Contains("volume profile")) return "volprofile";
            if (h.Contains("vwap")) return "vwap";

            // Elliott Wave
            if (h.Contains("elliott")) return "elliott";

            // RedTail custom tools
            if (h.Contains("redtail") && h.Contains("rectangle")) return "rtrect";
            if (h.Contains("redtail") && h.Contains("frvp") && h.Contains("fib")) return "rtfrvpfib";
            if (h.Contains("redtail") && h.Contains("mtf") && h.Contains("fib")) return "rtmtffib";
            if (h.Contains("redtail") && h.Contains("fib")) return "rtfib";
            if (h.Contains("redtail") && h.Contains("hline")) return "rthline";
            if (h.Contains("redtail") && h.Contains("measure")) return "rtmeasure";
            if (h.Contains("redtail") && (h.Contains("vp") || h.Contains("zone"))) return "rtzone";
            if (h.Contains("redtail") && h.Contains("trend") && h.Contains("channel")) return "rttrendchannel";
            if (h.Contains("redtail") && h.Contains("avwap")) return "rtavwap";
            if (h.Contains("redtail")) return "rtgeneric";

            // Gann
            if (h.Contains("gann")) return "gannfan";

            // Measured Move / AB=CD
            if (h.Contains("measured move") || h.Contains("ab=cd")) return "measuredmove";

            // Generic fallback based on keywords
            if (h.Contains("line")) return "line";
            if (h.Contains("channel")) return "channel";
            if (h.Contains("fib")) return "fib";
            if (h.Contains("supply") || h.Contains("demand")) return "rtzone";
            if (h.Contains("arrow")) return "arrowline";
            if (h.Contains("vwap") || h.Contains("anchored")) return "vwap";
            if (h.Contains("volume")) return "volprofile";
            if (h.Contains("range")) return "ruler";

            return "default";
        }

        private Canvas MakeIcon(string header, double sz)
        {
            Brush color = GetIconColor(header);
            string type = GetIconType(header);
            var c = new Canvas { Width = sz, Height = sz };
            double m = 2, w = sz - 4, h = sz - 4;

            switch (type)
            {
                // === LINES ===
                case "hline":
                    c.Children.Add(Ln(m,sz/2,sz-m,sz/2,color,2));
                    break;
                case "vline":
                    c.Children.Add(Ln(sz/2,m,sz/2,sz-m,color,2));
                    break;
                case "line":
                    c.Children.Add(Ln(m,sz-m,sz-m,m,color,2));
                    break;
                case "ray":
                    c.Children.Add(Ln(m,sz-m,sz-m,m,color,2));
                    c.Children.Add(Ln(sz-m-4,m+1,sz-m,m,color,1.5));
                    c.Children.Add(Ln(sz-m-1,m+4,sz-m,m,color,1.5));
                    break;
                case "extline":
                    c.Children.Add(Ln(0,sz-m,sz,m,color,1.5));
                    var d1=new WpfEllipse{Width=3,Height=3,Fill=color};
                    Canvas.SetLeft(d1,0);Canvas.SetTop(d1,sz-m-1.5);c.Children.Add(d1);
                    var d2=new WpfEllipse{Width=3,Height=3,Fill=color};
                    Canvas.SetLeft(d2,sz-3);Canvas.SetTop(d2,m-1.5);c.Children.Add(d2);
                    break;
                case "arrowline":
                    c.Children.Add(Ln(m,sz-m,sz-m,m,color,1.5));
                    // Arrow head at end
                    c.Children.Add(Ln(sz-m-4,m,sz-m,m,color,1.5));
                    c.Children.Add(Ln(sz-m,m,sz-m,m+4,color,1.5));
                    // Arrow head at start
                    c.Children.Add(Ln(m+4,sz-m,m,sz-m,color,1.5));
                    c.Children.Add(Ln(m,sz-m,m,sz-m-4,color,1.5));
                    break;
                case "path":
                    // Zigzag path
                    c.Children.Add(Ln(m,sz*0.7,sz*0.3,m+2,color,1.5));
                    c.Children.Add(Ln(sz*0.3,m+2,sz*0.6,sz*0.6,color,1.5));
                    c.Children.Add(Ln(sz*0.6,sz*0.6,sz-m,m+2,color,1.5));
                    break;

                // === SHAPES ===
                case "rect":
                    var r=new WpfRectangle{Width=w,Height=h*0.6,Stroke=color,StrokeThickness=1.5,Fill=Brushes.Transparent};
                    Canvas.SetLeft(r,m);Canvas.SetTop(r,m+h*0.2);c.Children.Add(r);
                    break;
                case "regionx":
                    var rx=new WpfRectangle{Width=w*0.5,Height=h,Stroke=color,StrokeThickness=1,Fill=FB(40,255,193,7)};
                    Canvas.SetLeft(rx,m+w*0.25);Canvas.SetTop(rx,m);c.Children.Add(rx);
                    break;
                case "regiony":
                    var ry=new WpfRectangle{Width=w,Height=h*0.4,Stroke=color,StrokeThickness=1,Fill=FB(40,255,193,7)};
                    Canvas.SetLeft(ry,m);Canvas.SetTop(ry,m+h*0.3);c.Children.Add(ry);
                    break;
                case "ellipse":
                    var el=new WpfEllipse{Width=w,Height=h*0.65,Stroke=color,StrokeThickness=1.5,Fill=Brushes.Transparent};
                    Canvas.SetLeft(el,m);Canvas.SetTop(el,m+h*0.18);c.Children.Add(el);
                    break;
                case "triangle":
                    c.Children.Add(Ln(sz/2,m,m,sz-m,color,1.5));
                    c.Children.Add(Ln(m,sz-m,sz-m,sz-m,color,1.5));
                    c.Children.Add(Ln(sz-m,sz-m,sz/2,m,color,1.5));
                    break;
                case "polygon":
                    // Pentagon shape
                    double cx=sz/2, cy=sz/2, pr=w*0.45;
                    for(int i=0;i<5;i++){
                        double a1=-Math.PI/2+i*2*Math.PI/5, a2=-Math.PI/2+(i+1)*2*Math.PI/5;
                        c.Children.Add(Ln(cx+pr*Math.Cos(a1),cy+pr*Math.Sin(a1),cx+pr*Math.Cos(a2),cy+pr*Math.Sin(a2),color,1.3));}
                    break;
                case "arc":
                    // Curved arc approximated with lines
                    for(int i=0;i<8;i++){
                        double a1=Math.PI*0.2+i*Math.PI*0.075, a2=Math.PI*0.2+(i+1)*Math.PI*0.075;
                        double ar=w*0.45;
                        c.Children.Add(Ln(sz/2+ar*Math.Cos(a1),sz*0.7-ar*Math.Sin(a1),
                                         sz/2+ar*Math.Cos(a2),sz*0.7-ar*Math.Sin(a2),color,1.5));}
                    break;

                // === FIBONACCI ===
                case "fib":
                    for(int i=0;i<5;i++){var fl=Ln(m,m+(h/4.0)*i,sz-m,m+(h/4.0)*i,color,1);
                        fl.Opacity=(i==0||i==4)?1:0.5;c.Children.Add(fl);}
                    break;
                case "fibext":
                    for(int i=0;i<4;i++){var fl=Ln(m,m+(h/3.0)*i,sz-m,m+(h/3.0)*i,color,1);
                        if(i>=2)fl.StrokeDashArray=new DoubleCollection{2,2};c.Children.Add(fl);}
                    break;
                case "fibtimeext":
                    // Vertical fib lines
                    for(int i=0;i<4;i++){double x=m+(w/3.0)*i;
                        var fl=Ln(x,m,x,sz-m,color,1);if(i>=2)fl.StrokeDashArray=new DoubleCollection{2,2};c.Children.Add(fl);}
                    break;
                case "fibcircle":
                    // Concentric circles
                    for(int i=1;i<=3;i++){double cr=w*0.15*i;
                        var ce=new WpfEllipse{Width=cr*2,Height=cr*2,Stroke=color,StrokeThickness=1,Fill=Brushes.Transparent};
                        ce.Opacity=1.0-i*0.2;Canvas.SetLeft(ce,sz/2-cr);Canvas.SetTop(ce,sz/2-cr);c.Children.Add(ce);}
                    break;

                // === CHANNELS & ADVANCED ===
                case "channel":
                    c.Children.Add(Ln(m,sz-m,sz-m,m+h*0.2,color,1.5));
                    c.Children.Add(Ln(m,sz-m-h*0.3,sz-m,m,color,1.5));
                    break;
                case "regchannel":
                    // Regression channel with center line
                    c.Children.Add(Ln(m,sz-m,sz-m,m+h*0.15,color,1.5));
                    c.Children.Add(Ln(m,sz-m-h*0.35,sz-m,m,color,1.5));
                    var mid=Ln(m,sz-m-h*0.175,sz-m,m+h*0.075,color,1);mid.StrokeDashArray=new DoubleCollection{3,2};c.Children.Add(mid);
                    break;
                case "pitchfork":
                    // Three-pronged pitchfork
                    c.Children.Add(Ln(m,sz*0.7,sz-m,m,color,1.5));
                    c.Children.Add(Ln(m,sz*0.5,sz-m,m-2,color,1));
                    c.Children.Add(Ln(m,sz*0.9,sz-m,m+4,color,1));
                    // Handle
                    c.Children.Add(Ln(m-1,sz*0.7,m+4,sz-m,color,1.5));
                    break;
                case "gannfan":
                    // Fan of lines from bottom-left
                    c.Children.Add(Ln(m,sz-m,sz-m,m,color,1.5));
                    c.Children.Add(Ln(m,sz-m,sz-m,sz*0.35,color,1));
                    c.Children.Add(Ln(m,sz-m,sz-m,sz*0.65,color,1));
                    c.Children.Add(Ln(m,sz-m,sz*0.5,m,color,1));
                    break;
                case "timecycles":
                    // Vertical arcs
                    for(int i=0;i<3;i++){double acx=m+w*0.25*(i+1);
                        for(int j=0;j<6;j++){double a1=j*Math.PI/6,a2=(j+1)*Math.PI/6;double ar2=w*0.12*(i+1);
                            c.Children.Add(Ln(acx+ar2*Math.Cos(a1),sz-m-ar2*Math.Sin(a1),
                                             acx+ar2*Math.Cos(a2),sz-m-ar2*Math.Sin(a2),color,1));}}
                    break;

                // === ANNOTATION ===
                case "text":
                    var tb=new TextBlock{Text="T",FontSize=sz*0.7,FontWeight=FontWeights.Bold,Foreground=color};
                    Canvas.SetLeft(tb,m+1);Canvas.SetTop(tb,-2);c.Children.Add(tb);
                    break;
                case "marker":
                    // Diamond marker
                    c.Children.Add(Ln(sz/2,m,sz-m,sz/2,color,1.5));
                    c.Children.Add(Ln(sz-m,sz/2,sz/2,sz-m,color,1.5));
                    c.Children.Add(Ln(sz/2,sz-m,m,sz/2,color,1.5));
                    c.Children.Add(Ln(m,sz/2,sz/2,m,color,1.5));
                    break;
                case "ruler":
                    // Ruler with tick marks
                    c.Children.Add(Ln(m,sz/2,sz-m,sz/2,color,1.5));
                    c.Children.Add(Ln(m,sz*0.3,m,sz*0.7,color,1));
                    c.Children.Add(Ln(sz-m,sz*0.3,sz-m,sz*0.7,color,1));
                    c.Children.Add(Ln(sz/2,sz*0.38,sz/2,sz*0.62,color,0.8));
                    break;
                case "riskreward":
                    // Green zone on top, red zone on bottom
                    var rrTop=new WpfRectangle{Width=w*0.7,Height=h*0.35,Fill=FB(80,76,175,80),Stroke=FB(76,175,80),StrokeThickness=1};
                    Canvas.SetLeft(rrTop,m+w*0.15);Canvas.SetTop(rrTop,m);c.Children.Add(rrTop);
                    var rrBot=new WpfRectangle{Width=w*0.7,Height=h*0.35,Fill=FB(80,220,60,60),Stroke=FB(220,60,60),StrokeThickness=1};
                    Canvas.SetLeft(rrBot,m+w*0.15);Canvas.SetTop(rrBot,sz-m-h*0.35);c.Children.Add(rrBot);
                    c.Children.Add(Ln(m,sz/2,sz-m,sz/2,color,1.5));
                    break;

                // === ORDER FLOW ===
                case "volprofile":
                    // Horizontal volume bars
                    double[] vols = {0.5,0.8,1.0,0.7,0.4,0.9,0.6};
                    for(int i=0;i<vols.Length;i++){double bw=w*vols[i]*0.85;double by=m+i*(h/vols.Length);
                        var vr2=new WpfRectangle{Width=bw,Height=h/vols.Length-1,Fill=color,Opacity=0.6};
                        Canvas.SetLeft(vr2,m);Canvas.SetTop(vr2,by);c.Children.Add(vr2);}
                    break;
                case "vwap":
                    // Wavy line with bands
                    c.Children.Add(Ln(m,sz*0.5,sz*0.3,sz*0.4,color,1.5));
                    c.Children.Add(Ln(sz*0.3,sz*0.4,sz*0.6,sz*0.55,color,1.5));
                    c.Children.Add(Ln(sz*0.6,sz*0.55,sz-m,sz*0.45,color,1.5));
                    // Upper band
                    var ub=Ln(m,sz*0.35,sz-m,sz*0.3,color,0.8);ub.StrokeDashArray=new DoubleCollection{2,2};c.Children.Add(ub);
                    // Lower band
                    var lb=Ln(m,sz*0.65,sz-m,sz*0.6,color,0.8);lb.StrokeDashArray=new DoubleCollection{2,2};c.Children.Add(lb);
                    break;

                // === ELLIOTT WAVE ===
                case "elliott":
                    // 5-wave impulse pattern
                    c.Children.Add(Ln(m,sz*0.7,sz*0.2,sz*0.3,color,1.3));
                    c.Children.Add(Ln(sz*0.2,sz*0.3,sz*0.35,sz*0.55,color,1.3));
                    c.Children.Add(Ln(sz*0.35,sz*0.55,sz*0.55,m,color,1.3));
                    c.Children.Add(Ln(sz*0.55,m,sz*0.7,sz*0.4,color,1.3));
                    c.Children.Add(Ln(sz*0.7,sz*0.4,sz-m,sz*0.15,color,1.3));
                    break;

                // === MEASURED MOVE / AB=CD ===
                case "measuredmove":
                    c.Children.Add(Ln(m,sz*0.75,sz*0.35,sz*0.25,color,1.5));
                    c.Children.Add(Ln(sz*0.35,sz*0.25,sz*0.55,sz*0.55,color,1.5));
                    c.Children.Add(Ln(sz*0.55,sz*0.55,sz-m,m,color,1.5));
                    break;

                // === REDTAIL CUSTOM ===
                case "rtfib":
                    // Standard fib retracement - horizontal levels with diagonal anchor line
                    c.Children.Add(Ln(m, sz-m, sz-m, m, color, 1));
                    for(int i=0;i<5;i++){double y=m+(h/4.0)*i;
                        var fl=Ln(m,y,sz-m,y,color,1);
                        fl.Opacity=(i==0||i==4)?1:0.5;c.Children.Add(fl);}
                    break;
                case "rtfrvpfib":
                    // FRVP Fib - fib levels with small volume bars on the left side
                    for(int i=0;i<5;i++){double y=m+(h/4.0)*i;
                        c.Children.Add(Ln(sz*0.35,y,sz-m,y,color,1));}
                    // Mini volume bars along left
                    double[] frvpVols = {0.4,0.7,1.0,0.6,0.3};
                    for(int i=0;i<frvpVols.Length;i++){double y=m+(h/4.0)*i;
                        var vb=Ln(m,y,m+frvpVols[i]*(sz*0.3),y,FB(0,200,120),1);c.Children.Add(vb);}
                    break;
                case "rtmtffib":
                    // MTF Fib - stacked fib levels at different scales to show multi-timeframe
                    // Outer (higher TF) levels - full width, solid
                    c.Children.Add(Ln(m,m,sz-m,m,color,1));
                    c.Children.Add(Ln(m,sz-m,sz-m,sz-m,color,1));
                    // Inner (lower TF) levels - indented, dashed
                    for(int i=1;i<4;i++){double y=m+(h/4.0)*i;
                        var fl=Ln(m+3,y,sz-m-3,y,color,1);
                        fl.StrokeDashArray=new DoubleCollection{2,2};fl.Opacity=0.6;c.Children.Add(fl);}
                    // Diagonal connecting line
                    var mtfDiag=Ln(m,sz-m,sz-m,m,color,1);mtfDiag.Opacity=0.35;c.Children.Add(mtfDiag);
                    break;
                case "rtrect":
                    // Rectangle with dashed midline (zone box)
                    var rr=new WpfRectangle{Width=w,Height=h*0.65,Stroke=color,StrokeThickness=1,Fill=FB(30,255,120,50)};
                    Canvas.SetLeft(rr,m);Canvas.SetTop(rr,m+h*0.18);c.Children.Add(rr);
                    var ml2=Ln(m,sz/2,sz-m,sz/2,color,1);
                    ml2.StrokeDashArray=new DoubleCollection{3,2};c.Children.Add(ml2);
                    break;
                case "rthline":
                    // Horizontal line with small price tag on right
                    c.Children.Add(Ln(m,sz/2,sz-m-4,sz/2,color,1));
                    // Price label box
                    var plr=new WpfRectangle{Width=5,Height=5,Fill=color,Stroke=color,StrokeThickness=0.5};
                    Canvas.SetLeft(plr,sz-m-4);Canvas.SetTop(plr,sz/2-2.5);c.Children.Add(plr);
                    break;
                case "rtmeasure":
                    // Ruler/measure: diagonal line with horizontal + vertical projection lines
                    c.Children.Add(Ln(m,sz-m,sz-m,m,color,1));
                    // Horizontal projection (dashed)
                    var mh=Ln(m,sz-m,sz-m,sz-m,color,1);mh.StrokeDashArray=new DoubleCollection{2,2};mh.Opacity=0.5;c.Children.Add(mh);
                    // Vertical projection (dashed)
                    var mv=Ln(sz-m,m,sz-m,sz-m,color,1);mv.StrokeDashArray=new DoubleCollection{2,2};mv.Opacity=0.5;c.Children.Add(mv);
                    // End dots
                    var md1=new WpfEllipse{Width=3,Height=3,Fill=color};
                    Canvas.SetLeft(md1,m-1.5);Canvas.SetTop(md1,sz-m-1.5);c.Children.Add(md1);
                    var md2=new WpfEllipse{Width=3,Height=3,Fill=color};
                    Canvas.SetLeft(md2,sz-m-1.5);Canvas.SetTop(md2,m-1.5);c.Children.Add(md2);
                    break;
                case "rtzone":
                    // Supply/demand zone - filled zone band with upper/lower boundaries
                    var rtz=new WpfRectangle{Width=w,Height=h*0.35,Stroke=color,StrokeThickness=1,Fill=FB(50,255,120,50)};
                    Canvas.SetLeft(rtz,m);Canvas.SetTop(rtz,m+h*0.3);c.Children.Add(rtz);
                    // Price action wick poking through zone
                    c.Children.Add(Ln(sz*0.6,m,sz*0.6,sz-m,color,1));
                    break;
                case "rttrendchannel":
                    // Parallel channel - two angled lines with fill between
                    c.Children.Add(Ln(m,sz*0.75,sz-m,sz*0.35,color,1));
                    c.Children.Add(Ln(m,sz*0.45,sz-m,m,color,1));
                    // Dashed midline
                    var tcMid=Ln(m,sz*0.6,sz-m,sz*0.175,color,1);
                    tcMid.StrokeDashArray=new DoubleCollection{2,2};tcMid.Opacity=0.4;c.Children.Add(tcMid);
                    break;
                case "rtavwap":
                    // Anchored VWAP - anchor dot with curved VWAP line + bands
                    // Anchor point
                    var anch=new WpfEllipse{Width=4,Height=4,Fill=DangerBrush};
                    Canvas.SetLeft(anch,m-1);Canvas.SetTop(anch,sz*0.65-2);c.Children.Add(anch);
                    // VWAP line curving from anchor
                    c.Children.Add(Ln(m+2,sz*0.65,sz*0.35,sz*0.5,color,1));
                    c.Children.Add(Ln(sz*0.35,sz*0.5,sz*0.65,sz*0.45,color,1));
                    c.Children.Add(Ln(sz*0.65,sz*0.45,sz-m,sz*0.4,color,1));
                    // Upper band (dashed)
                    var avub=Ln(m+2,sz*0.45,sz-m,sz*0.25,color,1);avub.StrokeDashArray=new DoubleCollection{2,2};avub.Opacity=0.5;c.Children.Add(avub);
                    // Lower band (dashed)
                    var avlb=Ln(m+2,sz*0.85,sz-m,sz*0.55,color,1);avlb.StrokeDashArray=new DoubleCollection{2,2};avlb.Opacity=0.5;c.Children.Add(avlb);
                    break;
                case "rtgeneric":
                    // Generic RT tool - small RedTail hawk silhouette (simple V shape for wings)
                    c.Children.Add(Ln(m,m+2,sz/2,sz*0.4,color,1));
                    c.Children.Add(Ln(sz/2,sz*0.4,sz-m,m+2,color,1));
                    // Tail
                    c.Children.Add(Ln(sz/2,sz*0.4,sz/2,sz-m,DangerBrush,1));
                    break;

                // === DEFAULT: First 2 letters ===
                default:
                    string letters = header.Length >= 2 ? header.Substring(0,2) : header.Length > 0 ? header.Substring(0,1) : "?";
                    var dt=new TextBlock{Text=letters,FontSize=sz*0.45,FontWeight=FontWeights.Bold,Foreground=color};
                    Canvas.SetLeft(dt,m);Canvas.SetTop(dt,1);c.Children.Add(dt);
                    break;
            }
            return c;
        }

        private WpfLine Ln(double x1,double y1,double x2,double y2,Brush s,double t)
        {
            return new WpfLine{X1=x1,Y1=y1,X2=x2,Y2=y2,Stroke=s,StrokeThickness=t,
                StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};
        }

        #endregion

        #region Tool Activation

        private void ActivateTool(string header)
        {
            try
            {
                if (ChartControl == null || ntDrawingMenuItems == null) return;

                clickCount++;
                bool shouldLog = clickCount <= 5;

                MenuItem targetItem;
                if (!ntDrawingMenuItems.TryGetValue(header, out targetItem))
                {
                    if (shouldLog) Print("RT: No menu item for: " + header);
                    return;
                }

                ChartControl.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    try
                    {
                        // Built-in tools use RoutedCommand
                        if (targetItem.Command is RoutedCommand rc)
                        {
                            if (rc.CanExecute(targetItem.CommandParameter, targetItem.CommandTarget ?? chartWindow))
                            {
                                rc.Execute(targetItem.CommandParameter, targetItem.CommandTarget ?? chartWindow);
                                if (shouldLog) Print("RT: Activated " + header);
                            }
                            return;
                        }

                        // Custom tools (Command=null): raise Click
                        targetItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, targetItem));
                        if (shouldLog) Print("RT: Activated custom " + header);
                    }
                    catch (Exception ex) { Print("RT Activate Error: " + ex.Message); }
                }));
            }
            catch (Exception ex) { Print("RT Error: " + ex.Message); }
        }

        private static List<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            var results = new List<T>();
            if (parent == null) return results;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found) results.Add(found);
                results.AddRange(FindVisualChildren<T>(child));
            }
            return results;
        }

        #endregion

        #region Utility Actions

        private void ToggleVisibility(object sender, RoutedEventArgs e)
        {
            try
            {
                // Use NT's built-in Hide All Drawing Objects menu item
                if (ntHideAllMenuItem != null && ntHideAllMenuItem.Command is RoutedCommand rc)
                {
                    if (rc.CanExecute(ntHideAllMenuItem.CommandParameter, ntHideAllMenuItem.CommandTarget ?? chartWindow))
                    {
                        rc.Execute(ntHideAllMenuItem.CommandParameter, ntHideAllMenuItem.CommandTarget ?? chartWindow);
                        drawingsVisible = !drawingsVisible;
                        var btn = sender as Button;
                        if (btn != null)
                        {
                            btn.Content = drawingsVisible ? "👁" : "👁‍🗨";
                            btn.Foreground = drawingsVisible ? InactiveBrush : ActiveBrush;
                        }
                        Print("RT: Toggled drawing visibility");
                    }
                }
            }
            catch (Exception ex) { Print("RT Error: " + ex.Message); }
        }

        private void ToggleLock(object sender, RoutedEventArgs e)
        {
            try
            {
                drawingsLocked = !drawingsLocked;
                var btn = sender as Button;
                foreach (var d in DrawObjects.ToList())
                    if (d is NinjaTrader.NinjaScript.DrawingTools.DrawingTool dt) dt.IsLocked = drawingsLocked;
                if (btn != null) { btn.Content = drawingsLocked?"🔒":"🔓"; btn.Foreground = drawingsLocked?ActiveBrush:InactiveBrush; }
                ChartControl?.InvalidateVisual(); ForceRefresh();
            }
            catch (Exception ex) { Print("RT Error: " + ex.Message); }
        }

        private void DeleteAll(object sender, RoutedEventArgs e)
        {
            try
            {
                // Use NT's built-in Remove All Drawing Objects (it has its own confirmation)
                if (ntRemoveAllMenuItem != null && ntRemoveAllMenuItem.Command is RoutedCommand rc)
                {
                    if (rc.CanExecute(ntRemoveAllMenuItem.CommandParameter, ntRemoveAllMenuItem.CommandTarget ?? chartWindow))
                        rc.Execute(ntRemoveAllMenuItem.CommandParameter, ntRemoveAllMenuItem.CommandTarget ?? chartWindow);
                }
            }
            catch (Exception ex) { Print("RT Error: " + ex.Message); }
        }

        #endregion

        #region Pan Mode

        private void TogglePanMode(object sender, RoutedEventArgs e)
        {
            panMode = !panMode;

            if (panMode)
            {
                panButton.Foreground = PanBrush;
                panButton.Background = FB(50, 255, 193, 7);
                InstallPanHandlers();
                Print("RT: Pan mode ON");
            }
            else
            {
                // Make sure Ctrl is released if we're turning off mid-drag
                if (panDragging)
                {
                    keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    panDragging = false;
                }
                panButton.Foreground = InactiveBrush;
                panButton.Background = ButtonBg;
                RemovePanHandlers();
                Print("RT: Pan mode OFF");
            }
        }

        private void InstallPanHandlers()
        {
            if (ChartControl == null) return;
            ChartControl.PreviewMouseLeftButtonDown += Pan_MouseDown;
            ChartControl.PreviewMouseLeftButtonUp   += Pan_MouseUp;
            ChartControl.MouseLeave                 += Pan_MouseLeave;
        }

        private void RemovePanHandlers()
        {
            if (ChartControl == null) return;
            try
            {
                ChartControl.PreviewMouseLeftButtonDown -= Pan_MouseDown;
                ChartControl.PreviewMouseLeftButtonUp   -= Pan_MouseUp;
                ChartControl.MouseLeave                 -= Pan_MouseLeave;
            }
            catch { }
        }

        private void Pan_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!panMode) return;

            // Don't interfere with toolbar clicks - check if the click is on the toolbar area
            var pos = e.GetPosition(toolbarGrid);
            if (toolbarGrid != null && pos.Y >= 0 && pos.Y <= toolbarGrid.ActualHeight
                && pos.X >= 0 && pos.X <= toolbarGrid.ActualWidth)
                return;

            // Simulate Ctrl key press
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            panDragging = true;
        }

        private void Pan_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!panDragging) return;
            // Release Ctrl key
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            panDragging = false;
        }

        private void Pan_MouseLeave(object sender, MouseEventArgs e)
        {
            // Safety: release Ctrl if mouse leaves chart while dragging
            if (!panDragging) return;
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            panDragging = false;
        }

        #endregion

        #region Break Even

        private void BreakEvenClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ChartControl == null) return;

                Account acct = null;
                // Find the first connected account with a position on this instrument
                lock (Account.All)
                {
                    foreach (Account a in Account.All)
                    {
                        if (a.ConnectionStatus != ConnectionStatus.Connected) continue;
                        foreach (Position pos in a.Positions)
                        {
                            if (pos.Instrument == Instrument && pos.MarketPosition != MarketPosition.Flat)
                            {
                                acct = a;
                                break;
                            }
                        }
                        if (acct != null) break;
                    }
                }

                if (acct == null)
                {
                    Print("RT BE: No open position found for " + Instrument.FullName);
                    FlashBE(false);
                    return;
                }

                // Get position details
                Position position = null;
                foreach (Position pos in acct.Positions)
                {
                    if (pos.Instrument == Instrument && pos.MarketPosition != MarketPosition.Flat)
                    {
                        position = pos;
                        break;
                    }
                }

                if (position == null) { FlashBE(false); return; }

                double entryPrice = position.AveragePrice;
                double tickSize   = Instrument.MasterInstrument.TickSize;
                double bePrice;

                if (position.MarketPosition == MarketPosition.Long)
                    bePrice = entryPrice + (BreakEvenTicks * tickSize);
                else
                    bePrice = entryPrice - (BreakEvenTicks * tickSize);

                // Check that BE price makes sense (position must be in profit enough)
                double lastPrice = GetCurrentBid() > 0 ? GetCurrentBid() : Close[0];
                bool inProfit = position.MarketPosition == MarketPosition.Long
                    ? lastPrice >= bePrice
                    : lastPrice <= bePrice;

                if (!inProfit)
                {
                    Print("RT BE: Position not in enough profit to move stop to " + bePrice.ToString("F" + GetPriceDecimals()));
                    FlashBE(false);
                    return;
                }

                // Find and modify stop loss orders
                int modified = 0;
                foreach (Order order in acct.Orders)
                {
                    if (order.Instrument != Instrument) continue;
                    if (order.OrderState != OrderState.Accepted && order.OrderState != OrderState.Working) continue;
                    if (order.OrderType != OrderType.StopMarket && order.OrderType != OrderType.StopLimit) continue;

                    // Determine if this is a stop for our position direction
                    bool isStopForLong = (position.MarketPosition == MarketPosition.Long &&
                        (order.OrderAction == OrderAction.Sell || order.OrderAction == OrderAction.SellShort));
                    bool isStopForShort = (position.MarketPosition == MarketPosition.Short &&
                        (order.OrderAction == OrderAction.Buy || order.OrderAction == OrderAction.BuyToCover));

                    if (!isStopForLong && !isStopForShort) continue;

                    // Only move stop UP for longs, DOWN for shorts (never away from profit)
                    if (position.MarketPosition == MarketPosition.Long && order.StopPrice >= bePrice) continue;
                    if (position.MarketPosition == MarketPosition.Short && order.StopPrice <= bePrice) continue;

                    try
                    {
                        // Set the changed properties on the order object, then call Change with Order[] only
                        order.StopPriceChanged = bePrice;
                        order.QuantityChanged  = order.Quantity;
                        if (order.OrderType == OrderType.StopLimit)
                            order.LimitPriceChanged = bePrice;

                        acct.Change(new[] { order });

                        modified++;
                        Print("RT BE: Moved stop to " + bePrice.ToString("F" + GetPriceDecimals()) +
                            " (entry=" + entryPrice.ToString("F" + GetPriceDecimals()) +
                            ", offset=" + BreakEvenTicks + " ticks)");
                    }
                    catch (Exception ex) { Print("RT BE: Error modifying order: " + ex.Message); }
                }

                if (modified > 0)
                    FlashBE(true);
                else
                {
                    Print("RT BE: No eligible stop orders found to modify");
                    FlashBE(false);
                }
            }
            catch (Exception ex) { Print("RT BE Error: " + ex.Message); }
        }

        private int GetPriceDecimals()
        {
            double ts = Instrument.MasterInstrument.TickSize;
            if (ts >= 1) return 0;
            if (ts >= 0.1) return 1;
            if (ts >= 0.01) return 2;
            if (ts >= 0.001) return 3;
            return 4;
        }

        private double GetCurrentBid()
        {
            try
            {
                if (BarsArray != null && BarsArray.Length > 0)
                    return GetCurrentBid(0);
            }
            catch { }
            return 0;
        }

        private void FlashBE(bool success)
        {
            if (beButton == null) return;
            ChartControl?.Dispatcher?.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                try
                {
                    Brush flashColor = success ? Brushes.LimeGreen : DangerBrush;
                    beButton.Foreground = flashColor;
                    beButton.Background = success ? FB(30, 0, 200, 120) : FB(30, 220, 60, 60);

                    var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
                    resetTimer.Tick += (s, e2) =>
                    {
                        beButton.Foreground = BeBrush;
                        beButton.Background = ButtonBg;
                        resetTimer.Stop();
                    };
                    resetTimer.Start();
                }
                catch { }
            }));
        }

        #endregion

        #region Lag Timer

        private int lagTickCount = 0;

        private void LagTimer_Tick(object sender, EventArgs e)
        {
            if (lagTimerText == null) return;
            try
            {
                if (State == State.Realtime)
                {
                    // Check if we've received any market data recently (within 30 seconds)
                    double secsSinceData = (DateTime.Now - lastMarketDataTime).TotalSeconds;

                    if (lastMarketDataTime == DateTime.MinValue || secsSinceData > 30)
                    {
                        lagTimerText.Text = "NO DATA";
                        lagTimerText.Foreground = InactiveBrush;
                    }
                    else if (lagSec < 0)
                    {
                        lagTimerText.Text = "0.00s";
                        lagTimerText.Foreground = Brushes.LimeGreen;
                    }
                    else if (lagSec < LagWarningSec)
                    {
                        lagTimerText.Text = lagSec.ToString("F2") + "s";
                        lagTimerText.Foreground = Brushes.LimeGreen;
                    }
                    else if (lagSec < LagCriticalSec)
                    {
                        lagTimerText.Text = lagSec.ToString("F2") + "s";
                        lagTimerText.Foreground = Brushes.Orange;
                    }
                    else
                    {
                        lagTimerText.Text = lagSec.ToString("F1") + "s";
                        lagTimerText.Foreground = DangerBrush;
                    }
                }
                else if (State == State.Historical)
                {
                    lagTimerText.Text = "HIST";
                    lagTimerText.Foreground = Brushes.Orange;
                }
                else
                {
                    lagTimerText.Text = "OFF";
                    lagTimerText.Foreground = InactiveBrush;
                }
            }
            catch { }
        }

        #endregion

        #region Indicator Visibility Manager

        private Canvas MakeLayersIcon(double sz)
        {
            var c = new Canvas { Width = sz, Height = sz };
            double m = 1.5;

            // Three stacked parallelogram "layers" - bottom to top
            // Bottom layer (dimmest)
            var botColor = FB(0, 160, 100);
            c.Children.Add(Ln(m + 2, sz - m, sz - m + 1, sz - m, botColor, 1.5));
            c.Children.Add(Ln(m + 2, sz - m, m + 4, sz - m - 3, botColor, 1.5));
            c.Children.Add(Ln(sz - m + 1, sz - m, sz - m - 1, sz - m - 3, botColor, 1.5));
            c.Children.Add(Ln(m + 4, sz - m - 3, sz - m - 1, sz - m - 3, botColor, 1.5));

            // Middle layer
            var midColor = FB(0, 190, 110);
            c.Children.Add(Ln(m + 1, sz * 0.55, sz - m, sz * 0.55, midColor, 1.5));
            c.Children.Add(Ln(m + 1, sz * 0.55, m + 3, sz * 0.55 - 3, midColor, 1.5));
            c.Children.Add(Ln(sz - m, sz * 0.55, sz - m - 2, sz * 0.55 - 3, midColor, 1.5));
            c.Children.Add(Ln(m + 3, sz * 0.55 - 3, sz - m - 2, sz * 0.55 - 3, midColor, 1.5));

            // Top layer (brightest)
            var topColor = FB(0, 220, 130);
            c.Children.Add(Ln(m, sz * 0.25, sz - m - 1, sz * 0.25, topColor, 1.8));
            c.Children.Add(Ln(m, sz * 0.25, m + 2, sz * 0.25 - 3, topColor, 1.8));
            c.Children.Add(Ln(sz - m - 1, sz * 0.25, sz - m - 3, sz * 0.25 - 3, topColor, 1.8));
            c.Children.Add(Ln(m + 2, sz * 0.25 - 3, sz - m - 3, sz * 0.25 - 3, topColor, 1.8));

            return c;
        }

        private void LoadIndicatorVisibility()
        {
            hiddenIndicators = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(IndSettingsFile))
            {
                try
                {
                    var lines = File.ReadAllLines(IndSettingsFile);
                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            hiddenIndicators.Add(trimmed);
                    }
                    Print("RT: Loaded " + hiddenIndicators.Count + " hidden indicator names");

                    // Apply saved visibility state to chart indicators
                    ApplyIndicatorVisibility();
                }
                catch (Exception ex) { Print("RT: Error loading indicator visibility: " + ex.Message); }
            }
        }

        private void SaveIndicatorVisibility()
        {
            try
            {
                File.WriteAllLines(IndSettingsFile, hiddenIndicators.ToArray());
                Print("RT: Saved " + hiddenIndicators.Count + " hidden indicator names");
            }
            catch (Exception ex) { Print("RT: Error saving indicator visibility: " + ex.Message); }
        }

        private List<NinjaTrader.NinjaScript.IndicatorBase> GetChartIndicators()
        {
            var indicators = new List<NinjaTrader.NinjaScript.IndicatorBase>();
            try
            {
                if (ChartControl == null) return indicators;

                // Access indicators via ChartControl.Indicators
                var indCollection = ChartControl.Indicators;
                if (indCollection != null)
                {
                    foreach (var ind in indCollection)
                    {
                        if (ind == null || ind == this) continue;
                        indicators.Add(ind);
                    }
                }
            }
            catch (Exception ex) { Print("RT: Error getting chart indicators: " + ex.Message); }
            return indicators;
        }

        private void ApplyIndicatorVisibility()
        {
            try
            {
                var indicators = GetChartIndicators();
                foreach (var ind in indicators)
                {
                    if (hiddenIndicators.Contains(ind.Name))
                        SetIndicatorVisible(ind, false);
                }
                if (hiddenIndicators.Count > 0)
                    ForceChartRerender();
            }
            catch (Exception ex) { Print("RT: Error applying indicator visibility: " + ex.Message); }
        }

        private void SetIndicatorVisible(NinjaTrader.NinjaScript.IndicatorBase ind, bool visible)
        {
            try
            {
                // Set IsVisible directly - it's a public property on IndicatorBase
                ind.IsVisible = visible;

                // Also toggle plot brush opacity (some indicators need this)
                if (ind.Plots != null)
                {
                    foreach (var plot in ind.Plots)
                    {
                        if (plot.Brush is SolidColorBrush brush)
                        {
                            plot.Brush = new SolidColorBrush(brush.Color)
                            {
                                Opacity = visible ? 1.0 : 0.0
                            };
                        }
                    }
                }

                // Handle Draw objects (regions etc) created by the indicator
                try
                {
                    foreach (var panel in ChartControl.ChartPanels)
                    {
                        if (panel.ChartObjects != null)
                        {
                            foreach (var chartObject in panel.ChartObjects.ToList())
                            {
                                if (chartObject is NinjaTrader.NinjaScript.DrawingTools.DrawingTool drawingTool)
                                {
                                    // Check if this drawing tool belongs to our indicator
                                    // by matching the NinjaScriptBase reference
                                    try
                                    {
                                        var nsProp = drawingTool.GetType().GetProperty("AttachedTo",
                                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                        if (nsProp != null)
                                        {
                                            var attachedObj = nsProp.GetValue(drawingTool);
                                            if (attachedObj != null)
                                            {
                                                // Check if the attached NinjaScript matches our indicator
                                                var attachedNsProp = attachedObj.GetType().GetProperty("NinjaScriptBase",
                                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                                if (attachedNsProp != null)
                                                {
                                                    var attachedNs = attachedNsProp.GetValue(attachedObj);
                                                    if (attachedNs == ind)
                                                        drawingTool.IsVisible = visible;
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
                catch { }

                Print("RT: Set " + ind.Name + " visible=" + visible);
            }
            catch (Exception ex) { Print("RT: Error toggling " + ind.Name + ": " + ex.Message); }
        }

        private bool GetIndicatorIsVisible(NinjaTrader.NinjaScript.IndicatorBase ind)
        {
            try { return ind.IsVisible; }
            catch { return true; }
        }

        private void ForceChartRerender()
        {
            try
            {
                if (chartWindow == null || ChartControl == null) return;

                // Send F5 (Refresh) to the chart window - this is the only reliable way
                // to force NT8's SharpDX render pipeline to fully re-render after
                // changing indicator visibility from outside the chart's own event loop
                ChartControl.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    try
                    {
                        // Focus the chart window first
                        chartWindow.Activate();

                        // Send F5 key press/release
                        const byte VK_F5 = 0x74;
                        keybd_event(VK_F5, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                        keybd_event(VK_F5, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    }
                    catch { }
                }));
            }
            catch { }
        }

        private void ShowIndicatorManagerPopup(object sender, RoutedEventArgs e)
        {
            try
            {
                // Gather all indicators on the chart (excluding this toolbar)
                var indicators = GetChartIndicators();

                if (indicators.Count == 0)
                {
                    MessageBox.Show("No indicators found on this chart.", "RedTail HTS/STS",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Sort alphabetically
                indicators.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

                // Build popup window
                var managerWindow = new Window
                {
                    Title = "RedTail HTS/STS",
                    Width = 380,
                    Height = 450,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = SettingsBg,
                    ResizeMode = ResizeMode.CanResize,
                };

                var mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45, GridUnitType.Pixel) });

                // Scrollable list
                var scroll = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(10, 10, 10, 0)
                };

                var stack = new StackPanel();
                var checkBoxes = new Dictionary<NinjaTrader.NinjaScript.IndicatorBase, CheckBox>();

                // Header
                stack.Children.Add(new TextBlock
                {
                    Text = "Show/Hide Chart Indicators:",
                    FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 6), FontSize = 13
                });

                // Show All / Hide All buttons
                var selectPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                var showAllBtn = new Button { Content = "Show All", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 2, 8, 2) };
                var hideAllBtn = new Button { Content = "Hide All", Padding = new Thickness(8, 2, 8, 2) };

                showAllBtn.Click += (s, ev) => { foreach (var cb in checkBoxes.Values) cb.IsChecked = true; };
                hideAllBtn.Click += (s, ev) => { foreach (var cb in checkBoxes.Values) cb.IsChecked = false; };

                selectPanel.Children.Add(showAllBtn);
                selectPanel.Children.Add(hideAllBtn);
                stack.Children.Add(selectPanel);

                // Separator
                stack.Children.Add(new Border
                {
                    Height = 1, Background = SepBrush,
                    Margin = new Thickness(0, 2, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                });

                // Track rows for delete removal
                var indicatorRows = new Dictionary<NinjaTrader.NinjaScript.IndicatorBase, Grid>();

                // One row per indicator: checkbox + delete button
                foreach (var ind in indicators)
                {
                    string displayName = ind.Name;

                    // Try to get fuller display name with parameters
                    try
                    {
                        string fullName = ind.ToString();
                        if (!string.IsNullOrEmpty(fullName) && fullName != ind.Name)
                            displayName = fullName;
                    }
                    catch { }

                    bool isVisible = GetIndicatorIsVisible(ind);

                    var rowGrid = new Grid
                    {
                        Margin = new Thickness(4, 3, 4, 3)
                    };
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24, GridUnitType.Pixel) });

                    var cb = new CheckBox
                    {
                        Content = displayName,
                        IsChecked = isVisible,
                        Foreground = Brushes.White,
                        VerticalContentAlignment = VerticalAlignment.Center,
                    };
                    Grid.SetColumn(cb, 0);
                    rowGrid.Children.Add(cb);

                    // Delete button
                    var capturedInd = ind;
                    var capturedRow = rowGrid;
                    var delBtn = new Button
                    {
                        Content = "✕",
                        Width = 20, Height = 20,
                        FontSize = 11, FontWeight = FontWeights.Bold,
                        Foreground = DangerBrush, Background = Brushes.Transparent,
                        BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand, ToolTip = "Remove " + ind.Name + " from chart",
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Padding = new Thickness(0),
                    };
                    delBtn.MouseEnter += (s, ev) => delBtn.Background = FB(60, 220, 60, 60);
                    delBtn.MouseLeave += (s, ev) => delBtn.Background = Brushes.Transparent;
                    delBtn.Click += (s, ev) =>
                    {
                        var result = MessageBox.Show(
                            "Remove \"" + capturedInd.Name + "\" from the chart?",
                            "RedTail HTS/STS",
                            MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            try
                            {
                                // Remove from tracking
                                checkBoxes.Remove(capturedInd);
                                indicatorRows.Remove(capturedInd);
                                stack.Children.Remove(capturedRow);

                                // Remove from hidden set if present
                                hiddenIndicators.Remove(capturedInd.Name);
                                SaveIndicatorVisibility();

                                // Remove indicator from chart via ChartControl
                                ChartControl.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                                {
                                    try
                                    {
                                        ChartControl.Indicators.Remove(capturedInd as NinjaTrader.Gui.NinjaScript.IndicatorRenderBase);
                                        ForceChartRerender();
                                        Print("RT: Deleted indicator " + capturedInd.Name + " from chart");
                                    }
                                    catch (Exception ex) { Print("RT: Error deleting indicator: " + ex.Message); }
                                }));
                            }
                            catch (Exception ex) { Print("RT: Error removing indicator row: " + ex.Message); }
                        }
                    };
                    Grid.SetColumn(delBtn, 1);
                    rowGrid.Children.Add(delBtn);

                    checkBoxes[ind] = cb;
                    indicatorRows[ind] = rowGrid;
                    stack.Children.Add(rowGrid);
                }

                scroll.Content = stack;
                Grid.SetRow(scroll, 0);
                mainGrid.Children.Add(scroll);

                // OK / Cancel buttons
                var btnPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                var okBtn = new Button
                {
                    Content = "Apply", Width = 80, Height = 28, Margin = new Thickness(0, 0, 8, 0),
                    IsDefault = true
                };
                var cancelBtn = new Button
                {
                    Content = "Cancel", Width = 80, Height = 28,
                    IsCancel = true
                };

                okBtn.Click += (s, ev) =>
                {
                    hiddenIndicators.Clear();
                    foreach (var kvp in checkBoxes)
                    {
                        var ind = kvp.Key;
                        bool show = kvp.Value.IsChecked == true;

                        if (!show)
                            hiddenIndicators.Add(ind.Name);

                        SetIndicatorVisible(ind, show);
                    }
                    SaveIndicatorVisibility();

                    // Force a proper chart re-render after all toggles
                    ForceChartRerender();

                    managerWindow.Close();
                    Print("RT: Indicator visibility updated");
                };

                cancelBtn.Click += (s, ev) => managerWindow.Close();

                btnPanel.Children.Add(okBtn);
                btnPanel.Children.Add(cancelBtn);

                Grid.SetRow(btnPanel, 1);
                mainGrid.Children.Add(btnPanel);

                managerWindow.Content = mainGrid;
                managerWindow.Show();
            }
            catch (Exception ex) { Print("RT HTS/STS Error: " + ex.Message + "\n" + ex.StackTrace); }
        }

        #endregion

        #region Command Center

        private Canvas MakeCommandCenterIcon(double sz)
        {
            var c = new Canvas { Width = sz, Height = sz };
            double m = 1.5;
            Brush color = DangerBrush;

            // Dashboard / control panel icon - grid of sliders
            // Top slider
            c.Children.Add(Ln(m, sz * 0.2, sz - m, sz * 0.2, FB(100, 100, 100), 1.5));
            var dot1 = new WpfEllipse { Width = 4, Height = 4, Fill = color };
            Canvas.SetLeft(dot1, sz * 0.65 - 2); Canvas.SetTop(dot1, sz * 0.2 - 2); c.Children.Add(dot1);

            // Middle slider
            c.Children.Add(Ln(m, sz * 0.5, sz - m, sz * 0.5, FB(100, 100, 100), 1.5));
            var dot2 = new WpfEllipse { Width = 4, Height = 4, Fill = color };
            Canvas.SetLeft(dot2, sz * 0.35 - 2); Canvas.SetTop(dot2, sz * 0.5 - 2); c.Children.Add(dot2);

            // Bottom slider
            c.Children.Add(Ln(m, sz * 0.8, sz - m, sz * 0.8, FB(100, 100, 100), 1.5));
            var dot3 = new WpfEllipse { Width = 4, Height = 4, Fill = color };
            Canvas.SetLeft(dot3, sz * 0.5 - 2); Canvas.SetTop(dot3, sz * 0.8 - 2); c.Children.Add(dot3);

            return c;
        }

        private void ShowCommandCenter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Gather all RedTail indicators on the chart
                var allIndicators = GetChartIndicators();
                var redtailIndicators = allIndicators
                    .Where(ind => ind.Name != null && ind.Name.IndexOf("RedTail", StringComparison.OrdinalIgnoreCase) >= 0
                        && ind != this)
                    .OrderBy(ind => ind.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (redtailIndicators.Count == 0)
                {
                    MessageBox.Show("No RedTail indicators found on this chart.\n\nAdd a RedTail indicator to the chart first,\nthen use the Command Center to configure it.",
                        "RedTail Command Center", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Build the window
                var ccWindow = new Window
                {
                    Title = "RedTail Command Center",
                    Width = 520,
                    Height = 640,
                    MinWidth = 420,
                    MinHeight = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = FB(28, 28, 28),
                    ResizeMode = ResizeMode.CanResize,
                };

                var mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44, GridUnitType.Pixel) });   // selector row
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });     // property area
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48, GridUnitType.Pixel) });   // buttons

                // ======== ROW 0: Indicator selector ========
                var selectorPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 6, 12, 4)
                };

                selectorPanel.Children.Add(new TextBlock
                {
                    Text = "Indicator:", FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                });

                var indCombo = new ComboBox
                {
                    Width = 320, Height = 26, FontSize = 11,
                    Background = FB(45, 45, 45), Foreground = Brushes.White,
                    VerticalContentAlignment = VerticalAlignment.Center,
                };

                foreach (var ind in redtailIndicators)
                {
                    string display = ind.Name;
                    try
                    {
                        string full = ind.ToString();
                        if (!string.IsNullOrEmpty(full) && full != ind.Name)
                            display = full;
                    }
                    catch { }

                    indCombo.Items.Add(new ComboBoxItem
                    {
                        Content = display,
                        Tag = ind,
                        Foreground = Brushes.White,
                    });
                }

                selectorPanel.Children.Add(indCombo);
                Grid.SetRow(selectorPanel, 0);
                mainGrid.Children.Add(selectorPanel);

                // ======== ROW 1: Property area (populated on selection) ========
                var propertyScroll = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Margin = new Thickness(12, 4, 12, 4)
                };

                var propertyStack = new StackPanel();
                propertyStack.Children.Add(new TextBlock
                {
                    Text = "← Select a RedTail indicator to configure",
                    Foreground = FB(120, 120, 120), FontStyle = FontStyles.Italic,
                    FontSize = 12, Margin = new Thickness(0, 20, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                propertyScroll.Content = propertyStack;
                Grid.SetRow(propertyScroll, 1);
                mainGrid.Children.Add(propertyScroll);

                // Track current property controls for Apply
                var currentPropertyControls = new Dictionary<PropertyInfo, Func<object>>();
                NinjaTrader.NinjaScript.IndicatorBase currentIndicator = null;

                // ======== ROW 2: Action buttons ========
                var btnPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };

                var applyBtn = MakeCCButton("Apply", FB(0, 150, 80), true);
                var cancelBtn = MakeCCButton("Cancel", FB(120, 120, 120), false);

                applyBtn.Click += (s, ev) =>
                {
                    if (currentIndicator == null) return;

                    try
                    {
                        int applied = 0;
                        var errors = new List<string>();

                        foreach (var kvp in currentPropertyControls)
                        {
                            try
                            {
                                var prop = kvp.Key;
                                var getValue = kvp.Value;
                                object newVal = getValue();
                                if (newVal == null) continue;

                                object oldVal = null;
                                try { oldVal = prop.GetValue(currentIndicator); } catch { }

                                // Check if actually changed
                                bool same = false;
                                if (oldVal != null)
                                {
                                    if (oldVal is SolidColorBrush oldBrush && newVal is SolidColorBrush newBrush)
                                        same = oldBrush.Color == newBrush.Color;
                                    else
                                        same = oldVal.Equals(newVal);
                                }
                                if (same) continue;

                                // For Brush properties, handle frozen brushes
                                if (prop.PropertyType == typeof(Brush) || prop.PropertyType == typeof(SolidColorBrush))
                                {
                                    if (newVal is SolidColorBrush scb)
                                    {
                                        // SerializableColor approach — many NT indicators use a backing
                                        // Serialize property like "NYVWAPColorSerialize" for "NYVWAPColor"
                                        // Try the direct set first, then try the serializable backing
                                        try
                                        {
                                            var freshBrush = new SolidColorBrush(scb.Color);
                                            freshBrush.Freeze();
                                            prop.SetValue(currentIndicator, freshBrush);
                                            applied++;
                                            continue;
                                        }
                                        catch
                                        {
                                            // Try the Serialize backing property
                                            var serProp = currentIndicator.GetType().GetProperty(prop.Name + "Serialize",
                                                BindingFlags.Public | BindingFlags.Instance);
                                            if (serProp != null && serProp.PropertyType == typeof(string))
                                            {
                                                string colorStr = string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}",
                                                    scb.Color.A, scb.Color.R, scb.Color.G, scb.Color.B);
                                                serProp.SetValue(currentIndicator, colorStr);
                                                applied++;
                                                continue;
                                            }
                                        }
                                    }
                                }

                                prop.SetValue(currentIndicator, newVal);
                                applied++;
                            }
                            catch (Exception ex)
                            {
                                errors.Add(kvp.Key.Name + ": " + ex.Message);
                                Print("RT CC: Error setting " + kvp.Key.Name + ": " + ex.Message);
                            }
                        }

                        if (applied > 0)
                        {
                            Print("RT CC: Applied " + applied + " property changes to " + currentIndicator.Name);

                            // Force full chart re-render via F5
                            ChartControl?.InvalidateVisual();
                            ForceChartRerender();

                            // Close the window after applying
                            ccWindow.Close();
                        }

                        if (errors.Count > 0 && applied == 0)
                        {
                            MessageBox.Show("Some properties could not be changed at runtime:\n\n" +
                                string.Join("\n", errors.Take(5)) +
                                (errors.Count > 5 ? "\n...and " + (errors.Count - 5) + " more" : "") +
                                "\n\nThese properties may require removing and re-adding\nthe indicator through NinjaTrader's Indicators dialog.",
                                "RedTail Command Center", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        Print("RT CC Apply Error: " + ex.Message + "\n" + ex.StackTrace);
                        MessageBox.Show("Error: " + ex.Message, "RedTail Command Center",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };

                cancelBtn.Click += (s, ev) => ccWindow.Close();

                btnPanel.Children.Add(applyBtn);
                btnPanel.Children.Add(cancelBtn);
                Grid.SetRow(btnPanel, 2);
                mainGrid.Children.Add(btnPanel);

                // ======== Combo selection changed → populate properties ========
                indCombo.SelectionChanged += (s, ev) =>
                {
                    propertyStack.Children.Clear();
                    currentPropertyControls.Clear();
                    currentIndicator = null;

                    var selected = indCombo.SelectedItem as ComboBoxItem;
                    if (selected == null) return;
                    var ind = selected.Tag as NinjaTrader.NinjaScript.IndicatorBase;
                    if (ind == null) return;
                    currentIndicator = ind;

                    BuildPropertyPanel(ind, propertyStack, currentPropertyControls);
                };

                // Auto-select first if only one
                if (indCombo.Items.Count == 1)
                    indCombo.SelectedIndex = 0;

                ccWindow.Content = mainGrid;
                ccWindow.Show();
            }
            catch (Exception ex) { Print("RT CC Error: " + ex.Message + "\n" + ex.StackTrace); }
        }

        private Button MakeCCButton(string content, Brush bgColor, bool isPrimary)
        {
            var btn = new Button
            {
                Content = content,
                Width = isPrimary ? 90 : 80, Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 11, FontWeight = isPrimary ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = Brushes.White, Background = bgColor,
                BorderBrush = FB(80, 80, 80), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand, Padding = new Thickness(6, 2, 6, 2),
            };
            var origBg = bgColor;
            btn.MouseEnter += (s, e) => btn.Opacity = 0.85;
            btn.MouseLeave += (s, e) => btn.Opacity = 1.0;
            return btn;
        }

        private static string SafeDisplayName(DisplayAttribute d, string fallback)
        { try { return d?.GetName() ?? fallback; } catch { return fallback; } }

        private static string SafeDisplayGroupName(DisplayAttribute d, string fallback)
        { try { return d?.GetGroupName() ?? fallback; } catch { return fallback; } }

        private static string SafeDisplayDescription(DisplayAttribute d, string fallback)
        { try { return d?.GetDescription() ?? fallback; } catch { return fallback; } }

        private ControlTemplate BuildColorSwatchTemplate()
        {
            // ControlTemplate that renders the ToggleButton as a simple rounded rectangle
            // filled with the Background brush, so it looks like a color swatch
            var template = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ToggleButton));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
                { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush")
                { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness")
                { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            template.VisualTree = borderFactory;
            return template;
        }

        private void BuildPropertyPanel(NinjaTrader.NinjaScript.IndicatorBase ind, StackPanel stack,
            Dictionary<PropertyInfo, Func<object>> controls)
        {
            try
            {
                // Get all public instance properties with [Display] attribute
                var props = ind.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite)
                    .Where(p => p.GetCustomAttribute<DisplayAttribute>() != null)
                    .OrderBy(p =>
                    {
                        var d = p.GetCustomAttribute<DisplayAttribute>();
                        try { return d?.GetGroupName() ?? "ZZZ"; } catch { return "ZZZ"; }
                    })
                    .ThenBy(p =>
                    {
                        var d = p.GetCustomAttribute<DisplayAttribute>();
                        try { return d?.GetOrder() ?? 999; } catch { return 999; }
                    })
                    .ToList();

                if (props.Count == 0)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = "No configurable properties found for this indicator.",
                        Foreground = FB(160, 160, 160), FontStyle = FontStyles.Italic,
                        FontSize = 12, Margin = new Thickness(0, 10, 0, 0)
                    });
                    return;
                }

                // ======== Quick Toggles section (bool properties) ========
                var boolProps = props.Where(p => p.PropertyType == typeof(bool)).ToList();
                if (boolProps.Count > 0)
                {
                    stack.Children.Add(MakeSectionHeader("Quick Toggles", DangerBrush));

                    var toggleGrid = new System.Windows.Controls.WrapPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(4, 2, 4, 8)
                    };

                    foreach (var prop in boolProps)
                    {
                        var display = prop.GetCustomAttribute<DisplayAttribute>();
                        string label = SafeDisplayName(display, prop.Name);
                        bool currentVal = (bool)prop.GetValue(ind);

                        var toggleBtn = new System.Windows.Controls.Primitives.ToggleButton
                        {
                            Content = label,
                            IsChecked = currentVal,
                            MinWidth = 90, Height = 26,
                            FontSize = 10.5,
                            Margin = new Thickness(2, 2, 2, 2),
                            Padding = new Thickness(8, 2, 8, 2),
                            Foreground = Brushes.White,
                            Background = currentVal ? FB(0, 130, 70) : FB(60, 60, 60),
                            BorderBrush = currentVal ? FB(0, 170, 90) : FB(90, 90, 90),
                            BorderThickness = new Thickness(1),
                            Cursor = Cursors.Hand,
                            ToolTip = SafeDisplayDescription(display, label),
                        };

                        var capturedToggle = toggleBtn;
                        toggleBtn.Checked   += (s, e) => { capturedToggle.Background = FB(0, 130, 70); capturedToggle.BorderBrush = FB(0, 170, 90); };
                        toggleBtn.Unchecked += (s, e) => { capturedToggle.Background = FB(60, 60, 60); capturedToggle.BorderBrush = FB(90, 90, 90); };

                        toggleGrid.Children.Add(toggleBtn);
                        controls[prop] = () => toggleBtn.IsChecked == true;
                    }

                    stack.Children.Add(toggleGrid);
                    stack.Children.Add(MakeThinSep());
                }

                // ======== Grouped non-bool properties ========
                var nonBoolProps = props.Where(p => p.PropertyType != typeof(bool)).ToList();
                string lastGroup = null;

                foreach (var prop in nonBoolProps)
                {
                    var display = prop.GetCustomAttribute<DisplayAttribute>();
                    string group = SafeDisplayGroupName(display, "General");
                    string label = SafeDisplayName(display, prop.Name);
                    string desc  = SafeDisplayDescription(display, label);

                    if (group != lastGroup)
                    {
                        if (lastGroup != null) stack.Children.Add(MakeThinSep());
                        stack.Children.Add(MakeSectionHeader(group, FB(100, 180, 255)));
                        lastGroup = group;
                    }

                    object currentVal;
                    try { currentVal = prop.GetValue(ind); }
                    catch { continue; }

                    var rowGrid = new Grid { Margin = new Thickness(4, 3, 4, 3) };
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180, GridUnitType.Pixel) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var labelTb = new TextBlock
                    {
                        Text = label, FontSize = 11, Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = desc ?? label,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    };
                    Grid.SetColumn(labelTb, 0);
                    rowGrid.Children.Add(labelTb);

                    // Create appropriate editor based on property type
                    FrameworkElement editor = null;
                    Func<object> getter = null;

                    Type pType = prop.PropertyType;

                    if (pType == typeof(int))
                    {
                        var range = prop.GetCustomAttribute<RangeAttribute>();
                        var tb = MakeCCTextBox(currentVal?.ToString() ?? "0");
                        editor = tb;
                        getter = () =>
                        {
                            int val;
                            if (int.TryParse(tb.Text, out val))
                            {
                                if (range != null)
                                {
                                    if (val < Convert.ToInt32(range.Minimum)) val = Convert.ToInt32(range.Minimum);
                                    if (val > Convert.ToInt32(range.Maximum)) val = Convert.ToInt32(range.Maximum);
                                }
                                return val;
                            }
                            return currentVal;
                        };
                    }
                    else if (pType == typeof(double))
                    {
                        var range = prop.GetCustomAttribute<RangeAttribute>();
                        var tb = MakeCCTextBox(currentVal != null ? ((double)currentVal).ToString("G") : "0");
                        editor = tb;
                        getter = () =>
                        {
                            double val;
                            if (double.TryParse(tb.Text, out val))
                            {
                                if (range != null)
                                {
                                    if (val < Convert.ToDouble(range.Minimum)) val = Convert.ToDouble(range.Minimum);
                                    if (val > Convert.ToDouble(range.Maximum)) val = Convert.ToDouble(range.Maximum);
                                }
                                return val;
                            }
                            return currentVal;
                        };
                    }
                    else if (pType == typeof(float))
                    {
                        var tb = MakeCCTextBox(currentVal != null ? ((float)currentVal).ToString("G") : "0");
                        editor = tb;
                        getter = () =>
                        {
                            float val;
                            if (float.TryParse(tb.Text, out val)) return val;
                            return currentVal;
                        };
                    }
                    else if (pType == typeof(string))
                    {
                        var tb = MakeCCTextBox(currentVal?.ToString() ?? "");
                        editor = tb;
                        getter = () => tb.Text;
                    }
                    else if (pType.IsEnum)
                    {
                        var combo = new ComboBox
                        {
                            Height = 24, FontSize = 11,
                            Background = FB(50, 50, 50),
                            Foreground = Brushes.White,
                            VerticalContentAlignment = VerticalAlignment.Center,
                        };
                        foreach (var enumVal in Enum.GetValues(pType))
                        {
                            combo.Items.Add(new ComboBoxItem
                            {
                                Content = enumVal.ToString(),
                                Tag = enumVal,
                                Foreground = Brushes.White,
                            });
                        }
                        // Select current value
                        for (int i = 0; i < combo.Items.Count; i++)
                        {
                            var item = combo.Items[i] as ComboBoxItem;
                            if (item?.Tag != null && item.Tag.Equals(currentVal))
                            {
                                combo.SelectedIndex = i;
                                break;
                            }
                        }
                        editor = combo;
                        getter = () =>
                        {
                            var sel = combo.SelectedItem as ComboBoxItem;
                            return sel?.Tag ?? currentVal;
                        };
                    }
                    else if (pType == typeof(Brush) || pType == typeof(SolidColorBrush))
                    {
                        // Color picker - clickable swatch with dropdown palette
                        Color currentColor = Colors.Gray;
                        if (currentVal is SolidColorBrush scb) currentColor = scb.Color;

                        var colorPanel = new StackPanel { Orientation = Orientation.Horizontal };

                        // Clickable color swatch (ToggleButton styled as swatch)
                        var colorToggle = new System.Windows.Controls.Primitives.ToggleButton
                        {
                            Width = 32, Height = 22,
                            Background = new SolidColorBrush(currentColor),
                            BorderBrush = FB(100, 100, 100), BorderThickness = new Thickness(1),
                            Margin = new Thickness(0, 0, 6, 0),
                            Cursor = Cursors.Hand,
                            Template = BuildColorSwatchTemplate(),
                        };

                        // Hex text input
                        var colorText = MakeCCTextBox(string.Format("#{0:X2}{1:X2}{2:X2}", currentColor.R, currentColor.G, currentColor.B));
                        colorText.Width = 80;
                        colorText.TextChanged += (s, ev) =>
                        {
                            try
                            {
                                var c = (Color)ColorConverter.ConvertFromString(colorText.Text);
                                colorToggle.Background = new SolidColorBrush(c);
                            }
                            catch { }
                        };

                        // Popup palette
                        var popup = new System.Windows.Controls.Primitives.Popup
                        {
                            PlacementTarget = colorToggle,
                            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                            StaysOpen = false,
                            AllowsTransparency = true,
                        };

                        // Open/close popup via toggle click events
                        colorToggle.Checked   += (s, ev) => { popup.IsOpen = true; };
                        colorToggle.Unchecked += (s, ev) => { popup.IsOpen = false; };
                        popup.Closed          += (s, ev) => { colorToggle.IsChecked = false; };

                        var paletteBorder = new Border
                        {
                            Background = FB(38, 38, 38),
                            BorderBrush = FB(80, 80, 80), BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(8),
                        };

                        var paletteStack = new StackPanel();

                        // Color palette - trading-friendly colors
                        string[][] colorRows = new string[][]
                        {
                            // Row 1: Core trading colors (greens, reds)
                            new[] { "#00C853", "#00E676", "#00C896", "#4CAF50", "#2E7D32", "#1B5E20",
                                    "#FF1744", "#FF5252", "#E53935", "#C62828", "#B71C1C", "#880E4F" },
                            // Row 2: Blues, cyans, oranges, yellows
                            new[] { "#2196F3", "#1976D2", "#0D47A1", "#00BCD4", "#0097A7", "#006064",
                                    "#FF9800", "#F57C00", "#E65100", "#FFEB3B", "#FFC107", "#FF6F00" },
                            // Row 3: Purples, pinks, teals, limes
                            new[] { "#9C27B0", "#7B1FA2", "#4A148C", "#E91E63", "#AD1457", "#880E4F",
                                    "#009688", "#00796B", "#004D40", "#CDDC39", "#9E9D24", "#827717" },
                            // Row 4: Neutrals & grays
                            new[] { "#FFFFFF", "#ECEFF1", "#B0BEC5", "#78909C", "#546E7A", "#37474F",
                                    "#263238", "#212121", "#424242", "#616161", "#9E9E9E", "#BDBDBD" },
                        };

                        foreach (var row in colorRows)
                        {
                            var rowPanel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                            foreach (var hex in row)
                            {
                                Color swatchColor;
                                try { swatchColor = (Color)ColorConverter.ConvertFromString(hex); }
                                catch { continue; }

                                var swatch = new Border
                                {
                                    Width = 18, Height = 18,
                                    Background = new SolidColorBrush(swatchColor),
                                    BorderBrush = FB(70, 70, 70), BorderThickness = new Thickness(0.5),
                                    CornerRadius = new CornerRadius(2),
                                    Margin = new Thickness(1),
                                    Cursor = Cursors.Hand,
                                    ToolTip = hex,
                                };

                                string capturedHex = hex;
                                swatch.MouseLeftButtonDown += (s, ev) =>
                                {
                                    colorText.Text = capturedHex;
                                    try
                                    {
                                        var c = (Color)ColorConverter.ConvertFromString(capturedHex);
                                        colorToggle.Background = new SolidColorBrush(c);
                                    }
                                    catch { }
                                    colorToggle.IsChecked = false;
                                };

                                // Hover effect
                                swatch.MouseEnter += (s, ev) => swatch.BorderBrush = Brushes.White;
                                swatch.MouseLeave += (s, ev) => swatch.BorderBrush = FB(70, 70, 70);

                                rowPanel.Children.Add(swatch);
                            }
                            paletteStack.Children.Add(rowPanel);
                        }

                        paletteBorder.Child = paletteStack;
                        popup.Child = paletteBorder;

                        colorPanel.Children.Add(colorToggle);
                        colorPanel.Children.Add(colorText);
                        editor = colorPanel;
                        getter = () =>
                        {
                            try
                            {
                                var c = (Color)ColorConverter.ConvertFromString(colorText.Text);
                                return new SolidColorBrush(c);
                            }
                            catch { return currentVal; }
                        };
                    }
                    else
                    {
                        // Unsupported type - show read-only
                        var ro = new TextBlock
                        {
                            Text = currentVal?.ToString() ?? "(null)",
                            FontSize = 11, Foreground = FB(130, 130, 130),
                            FontStyle = FontStyles.Italic,
                            VerticalAlignment = VerticalAlignment.Center,
                        };
                        editor = ro;
                        // No getter for read-only
                    }

                    if (editor != null)
                    {
                        Grid.SetColumn(editor, 1);
                        rowGrid.Children.Add(editor);
                        if (getter != null) controls[prop] = getter;
                    }

                    stack.Children.Add(rowGrid);
                }

                // ======== Templates section ========
                stack.Children.Add(MakeThinSep());
                stack.Children.Add(MakeSectionHeader("Templates", FB(255, 193, 7)));

                BuildTemplateSection(ind, stack);
            }
            catch (Exception ex)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "Error reading properties: " + ex.Message,
                    Foreground = DangerBrush, FontSize = 11,
                    Margin = new Thickness(4, 10, 4, 0), TextWrapping = TextWrapping.Wrap,
                });
                Print("RT CC BuildProps Error: " + ex.Message);
            }
        }

        private void BuildTemplateSection(NinjaTrader.NinjaScript.IndicatorBase ind, StackPanel stack)
        {
            try
            {
                // Find template files for this indicator type
                string indTypeName = ind.GetType().Name;
                var templateFiles = new List<string>();

                if (Directory.Exists(TemplateDir))
                {
                    // NinjaTrader stores templates as TypeName.xml or custom names
                    foreach (var file in Directory.GetFiles(TemplateDir, "*.xml"))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        // Check if the template name starts with the indicator type name
                        if (fileName.IndexOf(indTypeName, StringComparison.OrdinalIgnoreCase) >= 0
                            || fileName.IndexOf(ind.Name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            templateFiles.Add(file);
                        }
                    }
                }

                if (templateFiles.Count == 0)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = "No saved templates found for " + ind.Name + ".\nSave templates via NinjaTrader's indicator properties dialog.",
                        Foreground = FB(120, 120, 120), FontStyle = FontStyles.Italic,
                        FontSize = 11, Margin = new Thickness(8, 4, 4, 4),
                        TextWrapping = TextWrapping.Wrap,
                    });
                    return;
                }

                var templatePanel = new StackPanel { Margin = new Thickness(4, 2, 4, 2) };

                foreach (var tFile in templateFiles)
                {
                    string tName = Path.GetFileNameWithoutExtension(tFile);
                    var tRow = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    tRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    tRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70, GridUnitType.Pixel) });

                    var tLabel = new TextBlock
                    {
                        Text = tName, FontSize = 11, Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Margin = new Thickness(8, 0, 4, 0),
                    };
                    Grid.SetColumn(tLabel, 0);
                    tRow.Children.Add(tLabel);

                    string capturedFile = tFile;
                    var loadBtn = new Button
                    {
                        Content = "Load",
                        Width = 60, Height = 22,
                        FontSize = 10, Foreground = Brushes.White,
                        Background = FB(60, 120, 180),
                        BorderBrush = FB(80, 140, 200), BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand,
                    };
                    loadBtn.Click += (s, ev) =>
                    {
                        try
                        {
                            // Read the template XML and apply directly to the indicator
                            var xmlContent = File.ReadAllText(capturedFile);
                            int applied = ApplyTemplateXmlDirect(ind, xmlContent);
                            Print("RT CC: Applied template '" + tName + "' (" + applied + " properties) to " + ind.Name);

                            if (applied > 0)
                            {
                                ChartControl?.InvalidateVisual();
                                ForceChartRerender();

                                loadBtn.Content = "✓ Loaded";
                                loadBtn.Background = FB(0, 130, 70);
                                var resetT = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                                resetT.Tick += (s2, e2) =>
                                {
                                    loadBtn.Content = "Load";
                                    loadBtn.Background = FB(60, 120, 180);
                                    resetT.Stop();
                                };
                                resetT.Start();
                            }
                        }
                        catch (Exception ex)
                        {
                            Print("RT CC: Template load error: " + ex.Message);
                            MessageBox.Show("Error loading template:\n" + ex.Message, "RedTail Command Center",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    };
                    Grid.SetColumn(loadBtn, 1);
                    tRow.Children.Add(loadBtn);

                    templatePanel.Children.Add(tRow);
                }

                stack.Children.Add(templatePanel);
            }
            catch (Exception ex)
            {
                Print("RT CC: Template section error: " + ex.Message);
            }
        }

        private int ApplyTemplateXmlDirect(NinjaTrader.NinjaScript.IndicatorBase ind, string xmlContent)
        {
            int applied = 0;
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(xmlContent);

                var props = ind.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite)
                    .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

                foreach (System.Xml.XmlNode node in doc.SelectNodes("//*"))
                {
                    if (node.ChildNodes.Count == 1 && node.FirstChild is System.Xml.XmlText)
                    {
                        string name = node.LocalName;
                        string value = node.InnerText.Trim();

                        PropertyInfo prop;
                        if (props.TryGetValue(name, out prop))
                        {
                            try
                            {
                                object converted = ConvertTemplateValue(prop.PropertyType, value);
                                if (converted != null)
                                {
                                    if (converted is SolidColorBrush scb)
                                    {
                                        var fresh = new SolidColorBrush(scb.Color);
                                        fresh.Freeze();
                                        converted = fresh;
                                    }
                                    prop.SetValue(ind, converted);
                                    applied++;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex) { Print("RT CC: XML parse error: " + ex.Message); }
            return applied;
        }

        private object ConvertTemplateValue(Type targetType, string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            if (targetType == typeof(bool))
            {
                bool b;
                if (bool.TryParse(value, out b)) return b;
            }
            else if (targetType == typeof(int))
            {
                int i;
                if (int.TryParse(value, out i)) return i;
            }
            else if (targetType == typeof(double))
            {
                double d;
                if (double.TryParse(value, out d)) return d;
            }
            else if (targetType == typeof(float))
            {
                float f;
                if (float.TryParse(value, out f)) return f;
            }
            else if (targetType == typeof(string))
            {
                return value;
            }
            else if (targetType.IsEnum)
            {
                try { return Enum.Parse(targetType, value, true); }
                catch { }
            }

            return null;
        }

        private TextBox MakeCCTextBox(string text)
        {
            return new TextBox
            {
                Text = text, FontSize = 11, Height = 24,
                Background = FB(50, 50, 50), Foreground = Brushes.White,
                BorderBrush = FB(80, 80, 80), BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 2, 4, 2),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
        }

        private TextBlock MakeSectionHeader(string text, Brush color)
        {
            return new TextBlock
            {
                Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = color,
                Margin = new Thickness(0, 8, 0, 4),
            };
        }

        private Border MakeThinSep()
        {
            return new Border
            {
                Height = 1, Background = FB(60, 60, 60),
                Margin = new Thickness(0, 6, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
        }

        #endregion

        #region Screenshot

        private Canvas MakeScreenshotIcon(double sz)
        {
            var c = new Canvas { Width = sz, Height = sz };
            double m = 2;
            Brush color = FB(180, 180, 180);

            // Camera body
            var body = new WpfRectangle
            {
                Width = sz - m * 2, Height = (sz - m * 2) * 0.6,
                Stroke = color, StrokeThickness = 1, Fill = Brushes.Transparent,
                RadiusX = 1.5, RadiusY = 1.5
            };
            Canvas.SetLeft(body, m); Canvas.SetTop(body, sz * 0.32); c.Children.Add(body);

            // Lens circle
            var lens = new WpfEllipse
            {
                Width = sz * 0.32, Height = sz * 0.32,
                Stroke = color, StrokeThickness = 1, Fill = Brushes.Transparent
            };
            Canvas.SetLeft(lens, sz / 2 - sz * 0.16); Canvas.SetTop(lens, sz * 0.45); c.Children.Add(lens);

            // Viewfinder bump on top
            c.Children.Add(Ln(sz * 0.32, sz * 0.32, sz * 0.42, sz * 0.18, color, 1));
            c.Children.Add(Ln(sz * 0.42, sz * 0.18, sz * 0.62, sz * 0.18, color, 1));
            c.Children.Add(Ln(sz * 0.62, sz * 0.18, sz * 0.68, sz * 0.32, color, 1));

            return c;
        }

        private void ScreenshotClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (chartWindow == null) return;

                // Ensure folder exists
                string folder = ScreenshotFolder;
                if (string.IsNullOrWhiteSpace(folder))
                    folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RedTail Screenshots");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                // Build filename: instrument_timeframe_timestamp.png
                string instrument = Instrument?.MasterInstrument?.Name ?? "Chart";
                string tf = BarsPeriod != null ? BarsPeriod.ToString() : "unknown";
                // Clean up characters that aren't file-safe
                tf = tf.Replace(" ", "").Replace(":", "-");
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = string.Format("{0}_{1}_{2}.png", instrument, tf, timestamp);
                string filePath = Path.Combine(folder, fileName);

                // Capture via screen BitBlt (gets DX content since it copies actual screen pixels)
                ChartControl.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    try
                    {
                        var hwndSource = PresentationSource.FromVisual(chartWindow) as HwndSource;
                        if (hwndSource == null) return;
                        IntPtr hwnd = hwndSource.Handle;

                        RECT rect;
                        if (!GetWindowRect(hwnd, out rect)) return;
                        int w = rect.Right - rect.Left;
                        int h = rect.Bottom - rect.Top;
                        if (w <= 0 || h <= 0) return;

                        // BitBlt from screen DC to memory DC
                        IntPtr screenDC = GetDC(IntPtr.Zero);
                        IntPtr memDC = CreateCompatibleDC(screenDC);
                        IntPtr hBitmap = CreateCompatibleBitmap(screenDC, w, h);
                        IntPtr oldBmp = SelectObject(memDC, hBitmap);

                        BitBlt(memDC, 0, 0, w, h, screenDC, rect.Left, rect.Top, SRCCOPY);

                        SelectObject(memDC, oldBmp);

                        // Convert GDI bitmap to WPF BitmapSource and save as PNG
                        var bmpSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bmpSource));
                        using (var fs = new FileStream(filePath, FileMode.Create))
                            encoder.Save(fs);

                        // Cleanup GDI resources
                        DeleteObject(hBitmap);
                        DeleteDC(memDC);
                        ReleaseDC(IntPtr.Zero, screenDC);

                        Print("RT: Screenshot saved → " + filePath);

                        // Flash the button to confirm
                        screenshotButton.Opacity = 0.4;
                        var resetT = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                        resetT.Tick += (s2, e2) => { screenshotButton.Opacity = 1.0; resetT.Stop(); };
                        resetT.Start();
                    }
                    catch (Exception ex) { Print("RT Screenshot Error: " + ex.Message); }
                }));
            }
            catch (Exception ex) { Print("RT Screenshot Error: " + ex.Message); }
        }

        #endregion

        #region Timeframe Switcher

        private void BuildTimeframeButtons()
        {
            if (tfPanel == null) return;
            tfPanel.Children.Clear();

            string[] parts = (TimeframeList ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            // Determine current chart timeframe for highlighting
            BarsPeriodType currentType = BarsPeriodType.Minute;
            int currentValue = 0;
            try
            {
                if (BarsPeriod != null)
                {
                    currentType = BarsPeriod.BarsPeriodType;
                    currentValue = BarsPeriod.Value;
                }
            }
            catch { }

            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // Parse: pure number = minutes, number + suffix = other types
                string label;
                string tooltip;
                int minutes = 0;
                BarsPeriodType periodType = BarsPeriodType.Minute;
                int periodValue = 0;

                string lower = trimmed.ToLower();
                if (lower.EndsWith("rn"))
                {
                    // Renko chart (brick size in ticks)
                    if (int.TryParse(lower.Substring(0, lower.Length - 2), out periodValue))
                    {
                        periodType = BarsPeriodType.Renko;
                        label = periodValue + "Rn";
                        tooltip = periodValue + " Renko";
                    }
                    else continue;
                }
                else if (lower.EndsWith("t"))
                {
                    // Tick chart
                    if (int.TryParse(lower.TrimEnd('t'), out periodValue))
                    {
                        periodType = BarsPeriodType.Tick;
                        label = periodValue + "T";
                        tooltip = periodValue + " Tick";
                    }
                    else continue;
                }
                else if (lower.EndsWith("r"))
                {
                    // Range chart
                    if (int.TryParse(lower.TrimEnd('r'), out periodValue))
                    {
                        periodType = BarsPeriodType.Range;
                        label = periodValue + "R";
                        tooltip = periodValue + " Range";
                    }
                    else continue;
                }
                else if (lower.EndsWith("s"))
                {
                    // Second chart
                    if (int.TryParse(lower.TrimEnd('s'), out periodValue))
                    {
                        periodType = BarsPeriodType.Second;
                        label = periodValue + "s";
                        tooltip = periodValue + " Second";
                    }
                    else continue;
                }
                else if (lower == "d" || lower == "daily")
                {
                    periodType = BarsPeriodType.Day;
                    periodValue = 1;
                    label = "D";
                    tooltip = "Daily";
                }
                else if (lower == "w" || lower == "weekly")
                {
                    periodType = BarsPeriodType.Week;
                    periodValue = 1;
                    label = "W";
                    tooltip = "Weekly";
                }
                else
                {
                    // Assume minutes
                    if (!int.TryParse(trimmed, out minutes) || minutes <= 0) continue;
                    periodType = BarsPeriodType.Minute;
                    periodValue = minutes;
                    label = minutes >= 60 ? (minutes / 60) + "H" : minutes + "m";
                    tooltip = minutes >= 60 ? (minutes / 60) + " Hour" : minutes + " Minute";
                }

                // Check if this button matches current chart timeframe
                bool isActive = (periodType == currentType && periodValue == currentValue);

                var capturedType = periodType;
                var capturedValue = periodValue;

                var btn = new Button
                {
                    MinWidth = BtnSize, Height = BtnSize - 6,
                    Background = isActive ? FB(40, 100, 200, 255) : ButtonBg,
                    BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand, ToolTip = tooltip,
                    Margin = new Thickness(1, 0, 1, 0),
                    Padding = new Thickness(4, 0, 4, 0),
                    FontSize = 9.5, FontWeight = FontWeights.SemiBold,
                    Content = label,
                    Foreground = isActive ? Brushes.White : FB(160, 200, 255),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = FlatStyle()
                };

                btn.Click += (s, ev) =>
                {
                    SwitchTimeframe(capturedType, capturedValue);
                    // Highlight the clicked button, dim the rest
                    foreach (var child in tfPanel.Children)
                    {
                        if (child is Button b)
                        {
                            b.Background = (b == btn) ? FB(40, 100, 200, 255) : ButtonBg;
                            b.Foreground = (b == btn) ? Brushes.White : FB(160, 200, 255);
                        }
                    }
                };
                btn.MouseEnter += (s, ev) => { if (btn.Background != FB(40, 100, 200, 255)) btn.Background = ButtonHoverBg; };
                btn.MouseLeave += (s, ev) =>
                {
                    // Check if this is the active button by foreground color
                    if (btn.Foreground is SolidColorBrush scb && scb.Color == Colors.White) return;
                    btn.Background = ButtonBg;
                };

                tfPanel.Children.Add(btn);
            }
        }

        private void SwitchTimeframe(BarsPeriodType periodType, int periodValue)
        {
            try
            {
                if (chartWindow == null || ChartControl == null) return;

                ChartControl.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    try
                    {
                        // Find or use cached IntervalSelector
                        if (cachedIntervalSelector == null)
                        {
                            var allElements = FindVisualChildren<FrameworkElement>(chartWindow);
                            foreach (var fe in allElements)
                            {
                                if (fe.GetType().FullName == "NinjaTrader.Gui.Tools.IntervalSelector"
                                    && fe.Name == "intervalSelector")
                                {
                                    cachedIntervalSelector = fe;
                                    break;
                                }
                            }
                        }

                        if (cachedIntervalSelector == null)
                        {
                            Print("RT TF: IntervalSelector not found");
                            return;
                        }

                        // Get the Interval property
                        var intervalProp = cachedIntervalSelector.GetType().GetProperty("Interval",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (intervalProp == null)
                        {
                            Print("RT TF: Interval property not found");
                            return;
                        }

                        // Build the new BarsPeriod
                        var newPeriod = new NinjaTrader.Data.BarsPeriod
                        {
                            BarsPeriodType = periodType,
                            Value = periodValue,
                        };

                        // Set the Interval property on the IntervalSelector
                        intervalProp.SetValue(cachedIntervalSelector, newPeriod);

                        // Try invoking Apply if it exists
                        var applyMethod = cachedIntervalSelector.GetType().GetMethod("Apply",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (applyMethod != null)
                            applyMethod.Invoke(cachedIntervalSelector, null);

                        // Simulate Enter key to trigger chart reload
                        var enterKey = new KeyEventArgs(Keyboard.PrimaryDevice,
                            PresentationSource.FromVisual(cachedIntervalSelector), 0, Key.Enter)
                        {
                            RoutedEvent = Keyboard.KeyDownEvent
                        };
                        cachedIntervalSelector.RaiseEvent(enterKey);

                        Print("RT: Switched to " + periodType + " " + periodValue);
                    }
                    catch (Exception ex) { Print("RT TF Switch Error: " + ex.Message + "\n" + ex.StackTrace); }
                }));
            }
            catch (Exception ex) { Print("RT TF Switch Error: " + ex.Message); }
        }

        #endregion

        #region Visual Tree Helpers

        private void DumpTree(DependencyObject obj, int depth, int maxDepth)
        {
            if (obj == null || depth > maxDepth) return;
            string indent = new string(' ', depth * 2);
            string info = obj.GetType().Name;
            if (obj is FrameworkElement fe)
            {
                if (!string.IsNullOrEmpty(fe.Name)) info += " Name=\"" + fe.Name + "\"";
                info += " [" + fe.ActualWidth.ToString("F0") + "x" + fe.ActualHeight.ToString("F0") + "]";
            }
            if (obj is Grid g) info += " Rows=" + g.RowDefinitions.Count + " Cols=" + g.ColumnDefinitions.Count + " Children=" + g.Children.Count;
            Print(indent + info);
            int count = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < count; i++)
                DumpTree(VisualTreeHelper.GetChild(obj, i), depth + 1, maxDepth);
        }

        private Grid FindNamedGrid(DependencyObject parent, string name, int maxDepth)
        {
            if (parent == null || maxDepth <= 0) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Grid g)
                {
                    if (name == null && g.RowDefinitions.Count > 0) return g;
                    if (name != null && g.Name == name) return g;
                }
                var result = FindNamedGrid(child, name, maxDepth - 1);
                if (result != null) return result;
            }
            return null;
        }

        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Range(24, 64)]
        [Display(Name="Toolbar Height", Order=1, GroupName="Toolbar Settings")]
        public int ToolbarHeight { get; set; }

        [NinjaScriptProperty]
        [Range(20, 48)]
        [Display(Name="Button Size", Order=2, GroupName="Toolbar Settings")]
        public int BtnSize { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Lag Timer", Order=3, GroupName="Toolbar Settings")]
        public bool ShowLagTimer { get; set; }

        [NinjaScriptProperty]
        [Range(0.01, 30.0)]
        [Display(Name="Lag Warning (sec)", Order=4, GroupName="Toolbar Settings")]
        public double LagWarningSec { get; set; }

        [NinjaScriptProperty]
        [Range(0.05, 60.0)]
        [Display(Name="Lag Critical (sec)", Order=5, GroupName="Toolbar Settings")]
        public double LagCriticalSec { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show ATR", Order=6, GroupName="Toolbar Settings")]
        public bool ShowATR { get; set; }

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name="ATR Period", Order=7, GroupName="Toolbar Settings")]
        public int AtrPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Break Even Button", Order=8, GroupName="Toolbar Settings")]
        public bool ShowBreakEven { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name="Break Even Offset (ticks)", Description="0 = true break even, 1 = BE+1 tick, etc.", Order=9, GroupName="Toolbar Settings")]
        public int BreakEvenTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Pan Button", Description="Toggle free chart panning without holding Ctrl", Order=10, GroupName="Toolbar Settings")]
        public bool ShowPanButton { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Indicator Manager", Description="Button to show/hide chart indicators", Order=11, GroupName="Toolbar Settings")]
        public bool ShowIndicatorManager { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Command Center", Description="Button to open the RedTail Command Center for live indicator configuration", Order=12, GroupName="Toolbar Settings")]
        public bool ShowCommandCenter { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Screenshot Button", Description="One-click chart screenshot saved as PNG", Order=13, GroupName="Toolbar Settings")]
        public bool ShowScreenshot { get; set; }

        [Display(Name="Screenshot Folder", Description="Folder path where screenshots are saved", Order=14, GroupName="Toolbar Settings")]
        public string ScreenshotFolder { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Timeframe Switcher", Description="Quick buttons to switch chart timeframes", Order=15, GroupName="Toolbar Settings")]
        public bool ShowTimeframeSwitcher { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Timeframe List", Description="Comma-separated intervals. Numbers = minutes (60 = 1H), T = tick, R = range, S = second, Rn = renko, D = daily, W = weekly. Examples: 1,5,15,60,386t,4r,30s,4rn,D", Order=16, GroupName="Toolbar Settings")]
        public string TimeframeList { get; set; }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private RedTail.RedTailToolbar[] cacheRedTailToolbar;
		public RedTail.RedTailToolbar RedTailToolbar(int toolbarHeight, int btnSize, bool showLagTimer, double lagWarningSec, double lagCriticalSec, bool showATR, int atrPeriod, bool showBreakEven, int breakEvenTicks, bool showPanButton, bool showIndicatorManager, bool showCommandCenter, bool showScreenshot, bool showTimeframeSwitcher, string timeframeList)
		{
			return RedTailToolbar(Input, toolbarHeight, btnSize, showLagTimer, lagWarningSec, lagCriticalSec, showATR, atrPeriod, showBreakEven, breakEvenTicks, showPanButton, showIndicatorManager, showCommandCenter, showScreenshot, showTimeframeSwitcher, timeframeList);
		}

		public RedTail.RedTailToolbar RedTailToolbar(ISeries<double> input, int toolbarHeight, int btnSize, bool showLagTimer, double lagWarningSec, double lagCriticalSec, bool showATR, int atrPeriod, bool showBreakEven, int breakEvenTicks, bool showPanButton, bool showIndicatorManager, bool showCommandCenter, bool showScreenshot, bool showTimeframeSwitcher, string timeframeList)
		{
			if (cacheRedTailToolbar != null)
				for (int idx = 0; idx < cacheRedTailToolbar.Length; idx++)
					if (cacheRedTailToolbar[idx] != null && cacheRedTailToolbar[idx].ToolbarHeight == toolbarHeight && cacheRedTailToolbar[idx].BtnSize == btnSize && cacheRedTailToolbar[idx].ShowLagTimer == showLagTimer && cacheRedTailToolbar[idx].LagWarningSec == lagWarningSec && cacheRedTailToolbar[idx].LagCriticalSec == lagCriticalSec && cacheRedTailToolbar[idx].ShowATR == showATR && cacheRedTailToolbar[idx].AtrPeriod == atrPeriod && cacheRedTailToolbar[idx].ShowBreakEven == showBreakEven && cacheRedTailToolbar[idx].BreakEvenTicks == breakEvenTicks && cacheRedTailToolbar[idx].ShowPanButton == showPanButton && cacheRedTailToolbar[idx].ShowIndicatorManager == showIndicatorManager && cacheRedTailToolbar[idx].ShowCommandCenter == showCommandCenter && cacheRedTailToolbar[idx].ShowScreenshot == showScreenshot && cacheRedTailToolbar[idx].ShowTimeframeSwitcher == showTimeframeSwitcher && cacheRedTailToolbar[idx].TimeframeList == timeframeList && cacheRedTailToolbar[idx].EqualsInput(input))
						return cacheRedTailToolbar[idx];
			return CacheIndicator<RedTail.RedTailToolbar>(new RedTail.RedTailToolbar(){ ToolbarHeight = toolbarHeight, BtnSize = btnSize, ShowLagTimer = showLagTimer, LagWarningSec = lagWarningSec, LagCriticalSec = lagCriticalSec, ShowATR = showATR, AtrPeriod = atrPeriod, ShowBreakEven = showBreakEven, BreakEvenTicks = breakEvenTicks, ShowPanButton = showPanButton, ShowIndicatorManager = showIndicatorManager, ShowCommandCenter = showCommandCenter, ShowScreenshot = showScreenshot, ShowTimeframeSwitcher = showTimeframeSwitcher, TimeframeList = timeframeList }, input, ref cacheRedTailToolbar);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RedTail.RedTailToolbar RedTailToolbar(int toolbarHeight, int btnSize, bool showLagTimer, double lagWarningSec, double lagCriticalSec, bool showATR, int atrPeriod, bool showBreakEven, int breakEvenTicks, bool showPanButton, bool showIndicatorManager, bool showCommandCenter, bool showScreenshot, bool showTimeframeSwitcher, string timeframeList)
		{
			return indicator.RedTailToolbar(Input, toolbarHeight, btnSize, showLagTimer, lagWarningSec, lagCriticalSec, showATR, atrPeriod, showBreakEven, breakEvenTicks, showPanButton, showIndicatorManager, showCommandCenter, showScreenshot, showTimeframeSwitcher, timeframeList);
		}

		public Indicators.RedTail.RedTailToolbar RedTailToolbar(ISeries<double> input , int toolbarHeight, int btnSize, bool showLagTimer, double lagWarningSec, double lagCriticalSec, bool showATR, int atrPeriod, bool showBreakEven, int breakEvenTicks, bool showPanButton, bool showIndicatorManager, bool showCommandCenter, bool showScreenshot, bool showTimeframeSwitcher, string timeframeList)
		{
			return indicator.RedTailToolbar(input, toolbarHeight, btnSize, showLagTimer, lagWarningSec, lagCriticalSec, showATR, atrPeriod, showBreakEven, breakEvenTicks, showPanButton, showIndicatorManager, showCommandCenter, showScreenshot, showTimeframeSwitcher, timeframeList);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RedTail.RedTailToolbar RedTailToolbar(int toolbarHeight, int btnSize, bool showLagTimer, double lagWarningSec, double lagCriticalSec, bool showATR, int atrPeriod, bool showBreakEven, int breakEvenTicks, bool showPanButton, bool showIndicatorManager, bool showCommandCenter, bool showScreenshot, bool showTimeframeSwitcher, string timeframeList)
		{
			return indicator.RedTailToolbar(Input, toolbarHeight, btnSize, showLagTimer, lagWarningSec, lagCriticalSec, showATR, atrPeriod, showBreakEven, breakEvenTicks, showPanButton, showIndicatorManager, showCommandCenter, showScreenshot, showTimeframeSwitcher, timeframeList);
		}

		public Indicators.RedTail.RedTailToolbar RedTailToolbar(ISeries<double> input , int toolbarHeight, int btnSize, bool showLagTimer, double lagWarningSec, double lagCriticalSec, bool showATR, int atrPeriod, bool showBreakEven, int breakEvenTicks, bool showPanButton, bool showIndicatorManager, bool showCommandCenter, bool showScreenshot, bool showTimeframeSwitcher, string timeframeList)
		{
			return indicator.RedTailToolbar(input, toolbarHeight, btnSize, showLagTimer, lagWarningSec, lagCriticalSec, showATR, atrPeriod, showBreakEven, breakEvenTicks, showPanButton, showIndicatorManager, showCommandCenter, showScreenshot, showTimeframeSwitcher, timeframeList);
		}
	}
}

#endregion
