// RedTail Measure Tool v1.0
// Custom NinjaTrader 8 Drawing Tool
// Comprehensive trade measurement with auto-detection
// Contact: 3astbeast@pm.me

#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
#endregion

namespace NinjaTrader.NinjaScript.DrawingTools
{
    [CategoryOrder("Display Options", 1)]
    [CategoryOrder("Trade Settings", 2)]
    [CategoryOrder("Visual Settings", 3)]
    public class RedTailMeasureTool : DrawingTool
    {
        #region Enums
        public enum TextPlacement
        {
            TopOutside,
            BottomOutside,
            TopInside,
            BottomInside,
            Split
        }
        #endregion

        #region Variables
        private ChartAnchor startAnchor;
        private ChartAnchor endAnchor;
        private bool isDrawingComplete;

        // Move/resize tracking
        private ChartAnchor editingAnchor;
        private bool isMoving;
        private DateTime moveStartTime;
        private DateTime moveEndTime;
        private double moveStartPrice;
        private double moveEndPrice;

        // SharpDX resources
        private SharpDX.Direct2D1.Brush borderBrushDx;
        private SharpDX.Direct2D1.Brush textBrushDx;
        private SharpDX.Direct2D1.Brush textBackgroundBrushDx;
        private SharpDX.DirectWrite.TextFormat textFormatDx;

        // Cached calculations
        private int cachedBarCount;
        private TimeSpan cachedDuration;
        private double cachedPriceRange;
        private double cachedTicks;
        private double cachedPoints;
        private double cachedDollarValue;
        private double cachedPercentChange;
        private double cachedTicksPerBar;
        private double cachedDollarsPerMinute;
        private double cachedNetPnL;
        private long cachedTotalVolume;
        private long cachedBuyVolume;
        private long cachedSellVolume;
        private long cachedDelta;
        private double cachedAvgVolumePerBar;
        private bool cachedIsLong;
        private double cachedTickSize;
        private double cachedTickValue;
        private string cachedInstrumentName;
        #endregion

        #region Properties

        // ===== Display Options =====
        [NinjaScriptProperty]
        [Display(Name = "Show Bars & Time", Order = 1, GroupName = "Display Options")]
        public bool ShowBarsAndTime { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Price (Points/Ticks)", Order = 2, GroupName = "Display Options")]
        public bool ShowPriceTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Dollar Value", Order = 3, GroupName = "Display Options")]
        public bool ShowDollarValue { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Percentage", Order = 4, GroupName = "Display Options")]
        public bool ShowPercentage { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Velocity (Ticks/Bar)", Order = 5, GroupName = "Display Options")]
        public bool ShowVelocity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show $/Minute", Order = 6, GroupName = "Display Options")]
        public bool ShowDollarsPerMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Net P&L (After Commission)", Order = 7, GroupName = "Display Options")]
        public bool ShowNetPnL { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Volume", Order = 8, GroupName = "Display Options")]
        public bool ShowVolume { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Delta", Order = 9, GroupName = "Display Options")]
        public bool ShowDelta { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Avg Vol/Bar", Order = 10, GroupName = "Display Options")]
        public bool ShowAvgVolPerBar { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Diagonal Line", Order = 11, GroupName = "Display Options")]
        public bool ShowDiagonalLine { get; set; }

        // ===== Trade Settings =====
        [NinjaScriptProperty]
        [Display(Name = "Number of Contracts", Order = 1, GroupName = "Trade Settings")]
        [Range(1, 1000)]
        public int NumContracts { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Round Trip Commission ($)", Order = 2, GroupName = "Trade Settings")]
        public double RoundTripCommission { get; set; }

        // ===== Visual Settings =====
        [XmlIgnore]
        [Display(Name = "Long Fill Color", Order = 1, GroupName = "Visual Settings")]
        public System.Windows.Media.Brush LongFillColor { get; set; }

        [Browsable(false)]
        public string LongFillColorSerialize
        {
            get { return Serialize.BrushToString(LongFillColor); }
            set { LongFillColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Short Fill Color", Order = 2, GroupName = "Visual Settings")]
        public System.Windows.Media.Brush ShortFillColor { get; set; }

        [Browsable(false)]
        public string ShortFillColorSerialize
        {
            get { return Serialize.BrushToString(ShortFillColor); }
            set { ShortFillColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Border Color", Order = 3, GroupName = "Visual Settings")]
        public System.Windows.Media.Brush BorderColor { get; set; }

        [Browsable(false)]
        public string BorderColorSerialize
        {
            get { return Serialize.BrushToString(BorderColor); }
            set { BorderColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Text Color", Order = 4, GroupName = "Visual Settings")]
        public System.Windows.Media.Brush TextColor { get; set; }

        [Browsable(false)]
        public string TextColorSerialize
        {
            get { return Serialize.BrushToString(TextColor); }
            set { TextColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Text Background Color", Order = 5, GroupName = "Visual Settings")]
        public System.Windows.Media.Brush TextBackgroundColor { get; set; }

        [Browsable(false)]
        public string TextBackgroundColorSerialize
        {
            get { return Serialize.BrushToString(TextBackgroundColor); }
            set { TextBackgroundColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Fill Opacity (%)", Order = 6, GroupName = "Visual Settings")]
        [Range(0, 100)]
        public int FillOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Text Background Opacity (%)", Order = 7, GroupName = "Visual Settings")]
        [Range(0, 100)]
        public int TextBackgroundOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Border Width", Order = 8, GroupName = "Visual Settings")]
        [Range(1, 5)]
        public int BorderWidth { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Border Opacity (%)", Order = 9, GroupName = "Visual Settings")]
        [Range(0, 100)]
        public int BorderOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Font Size", Order = 10, GroupName = "Visual Settings")]
        [Range(8, 24)]
        public int FontSize { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Text Placement", Order = 11, GroupName = "Visual Settings")]
        public TextPlacement TextPosition { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Auto Color (Green/Red by Direction)", Order = 12, GroupName = "Visual Settings")]
        public bool AutoColor { get; set; }

        // ===== Anchors =====
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolAnchor", GroupName = "NinjaScriptGeneral")]
        public ChartAnchor StartAnchor
        {
            get
            {
                if (startAnchor == null)
                    startAnchor = new ChartAnchor();
                return startAnchor;
            }
            set { startAnchor = value; }
        }

        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolAnchor", GroupName = "NinjaScriptGeneral")]
        public ChartAnchor EndAnchor
        {
            get
            {
                if (endAnchor == null)
                    endAnchor = new ChartAnchor();
                return endAnchor;
            }
            set { endAnchor = value; }
        }

        public override IEnumerable<ChartAnchor> Anchors
        {
            get { return new[] { StartAnchor, EndAnchor }; }
        }
        #endregion

        #region State Management
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name                = "RedTail Measure";
                Description         = "Comprehensive trade measurement tool with auto-detection";
                DrawingState        = DrawingState.Building;

                // Display defaults - all on
                ShowBarsAndTime     = true;
                ShowPriceTicks      = true;
                ShowDollarValue     = true;
                ShowPercentage      = true;
                ShowVelocity        = true;
                ShowDollarsPerMinute = true;
                ShowNetPnL          = true;
                ShowVolume          = true;
                ShowDelta           = true;
                ShowAvgVolPerBar    = true;
                ShowDiagonalLine    = true;

                // Trade defaults
                NumContracts        = 1;
                RoundTripCommission = 4.12;

                // Visual defaults
                LongFillColor       = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 180, 80));
                ShortFillColor      = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 220, 50, 50));
                BorderColor         = System.Windows.Media.Brushes.White;
                TextColor           = System.Windows.Media.Brushes.Black;
                TextBackgroundColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 30, 30, 30));
                FillOpacity         = 20;
                TextBackgroundOpacity = 20;
                BorderWidth         = 1;
                BorderOpacity       = 100;
                FontSize            = 11;
                TextPosition        = TextPlacement.Split;
                AutoColor           = true;

                if (LongFillColor.CanFreeze) LongFillColor.Freeze();
                if (ShortFillColor.CanFreeze) ShortFillColor.Freeze();
                if (TextBackgroundColor.CanFreeze) TextBackgroundColor.Freeze();

                startAnchor = new ChartAnchor();
                endAnchor   = new ChartAnchor();
            }
            else if (State == State.Terminated)
            {
                DisposeResources();
            }
        }
        #endregion

        #region Mouse Events
        public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, System.Windows.Point point)
        {
            if (DrawingState == DrawingState.Building)
                return Cursors.Cross;

            if (DrawingState == DrawingState.Moving)
                return Cursors.SizeAll;

            if (DrawingState == DrawingState.Editing)
                return Cursors.SizeNWSE;

            // Check if near an anchor for resize
            if (IsSelected)
            {
                ChartAnchor closest = GetClosestAnchor(chartControl, chartPanel, chartScale, point, 10);
                if (closest != null)
                    return Cursors.SizeNWSE;
            }

            if (IsPointInsideRect(chartControl, chartPanel, chartScale, point))
                return IsSelected ? Cursors.SizeAll : Cursors.Hand;

            return null;
        }

        public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Building)
            {
                if (startAnchor.IsEditing)
                {
                    dataPoint.CopyDataValues(startAnchor);
                    startAnchor.IsEditing = false;

                    dataPoint.CopyDataValues(endAnchor);
                    endAnchor.IsEditing = true;
                }
                else if (endAnchor.IsEditing)
                {
                    dataPoint.CopyDataValues(endAnchor);
                    endAnchor.IsEditing = false;
                    DrawingState = DrawingState.Normal;
                    isDrawingComplete = true;
                    IsSelected = false;
                }
            }
            else if (DrawingState == DrawingState.Normal && IsSelected)
            {
                System.Windows.Point mousePoint = dataPoint.GetPoint(chartControl, chartPanel, chartScale);

                // Check if clicking near an anchor for resize
                ChartAnchor closest = GetClosestAnchor(chartControl, chartPanel, chartScale, mousePoint, 10);
                if (closest != null)
                {
                    editingAnchor = closest;
                    editingAnchor.IsEditing = true;
                    DrawingState = DrawingState.Editing;
                }
                else if (IsPointInsideRect(chartControl, chartPanel, chartScale, mousePoint))
                {
                    // Start move - store initial anchor positions
                    moveStartTime  = startAnchor.Time;
                    moveEndTime    = endAnchor.Time;
                    moveStartPrice = startAnchor.Price;
                    moveEndPrice   = endAnchor.Price;
                    editingAnchor  = dataPoint;
                    isMoving       = true;
                    DrawingState   = DrawingState.Moving;
                }
            }
        }

        public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Building && !startAnchor.IsEditing && endAnchor.IsEditing)
            {
                dataPoint.CopyDataValues(endAnchor);
            }
            else if (DrawingState == DrawingState.Editing && editingAnchor != null)
            {
                dataPoint.CopyDataValues(editingAnchor);
            }
            else if (DrawingState == DrawingState.Moving && isMoving && editingAnchor != null)
            {
                // Calculate delta from the initial mouse-down anchor
                double deltaPrice = dataPoint.Price - editingAnchor.Price;
                TimeSpan deltaTime = dataPoint.Time - editingAnchor.Time;

                startAnchor.Price = moveStartPrice + deltaPrice;
                startAnchor.Time  = moveStartTime  + deltaTime;
                endAnchor.Price   = moveEndPrice   + deltaPrice;
                endAnchor.Time    = moveEndTime    + deltaTime;
            }
        }

        public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Editing)
            {
                if (editingAnchor != null)
                    editingAnchor.IsEditing = false;
                editingAnchor = null;
                DrawingState = DrawingState.Normal;
            }
            else if (DrawingState == DrawingState.Moving)
            {
                isMoving = false;
                editingAnchor = null;
                DrawingState = DrawingState.Normal;
            }
        }

        private ChartAnchor GetClosestAnchor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, System.Windows.Point point, double maxDist)
        {
            System.Windows.Point startPt = startAnchor.GetPoint(chartControl, chartPanel, chartScale);
            System.Windows.Point endPt   = endAnchor.GetPoint(chartControl, chartPanel, chartScale);

            double distStart = Math.Sqrt(Math.Pow(point.X - startPt.X, 2) + Math.Pow(point.Y - startPt.Y, 2));
            double distEnd   = Math.Sqrt(Math.Pow(point.X - endPt.X, 2) + Math.Pow(point.Y - endPt.Y, 2));

            // Also check the other two corners (they map to start.X/end.Y and end.X/start.Y)
            // For simplicity, only allow resizing from the two diagonal anchors
            if (distStart <= maxDist && distStart <= distEnd)
                return startAnchor;
            if (distEnd <= maxDist)
                return endAnchor;

            return null;
        }

        private bool IsPointInsideRect(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, System.Windows.Point point)
        {
            if (startAnchor == null || endAnchor == null)
                return false;

            double x1 = startAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
            double y1 = startAnchor.GetPoint(chartControl, chartPanel, chartScale).Y;
            double x2 = endAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
            double y2 = endAnchor.GetPoint(chartControl, chartPanel, chartScale).Y;

            double minX = Math.Min(x1, x2);
            double maxX = Math.Max(x1, x2);
            double minY = Math.Min(y1, y2);
            double maxY = Math.Max(y1, y2);

            return point.X >= minX && point.X <= maxX && point.Y >= minY && point.Y <= maxY;
        }
        #endregion

        #region Hit Test
        public override bool IsVisibleOnChart(ChartControl chartControl, ChartScale chartScale, DateTime firstTimeOnChart, DateTime lastTimeOnChart)
        {
            return true;
        }

        public override bool IsAlertConditionTrue(AlertConditionItem alertConditionItem, Condition condition, ChartAlertValue[] values, ChartControl chartControl, ChartScale chartScale)
        {
            return false;
        }

        public override System.Windows.Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
        {
            if (startAnchor == null || endAnchor == null)
                return new System.Windows.Point[0];

            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
            System.Windows.Point startPt = startAnchor.GetPoint(chartControl, chartPanel, chartScale);
            System.Windows.Point endPt = endAnchor.GetPoint(chartControl, chartPanel, chartScale);

            return new[]
            {
                startPt,
                endPt,
                new System.Windows.Point(startPt.X, endPt.Y),
                new System.Windows.Point(endPt.X, startPt.Y)
            };
        }
        #endregion

        #region Calculations
        private void CalculateMetrics(ChartControl chartControl, ChartScale chartScale)
        {
            if (startAnchor == null || endAnchor == null || chartControl == null)
                return;

            // Get instrument from AttachedTo
            Instrument instrument = AttachedTo != null ? AttachedTo.Instrument : null;
            if (instrument == null) return;

            // Auto-detect instrument properties
            cachedTickSize       = instrument.MasterInstrument.TickSize;
            cachedTickValue      = instrument.MasterInstrument.PointValue * cachedTickSize;
            cachedInstrumentName = instrument.MasterInstrument.Name;

            double startPrice = startAnchor.Price;
            double endPrice   = endAnchor.Price;
            DateTime startTime = startAnchor.Time;
            DateTime endTime   = endAnchor.Time;

            DateTime earlierTime = startTime < endTime ? startTime : endTime;
            DateTime laterTime   = startTime < endTime ? endTime : startTime;

            // Direction based on drag: end above start = long
            cachedIsLong = endPrice > startPrice;

            // Price calculations
            cachedPriceRange   = Math.Abs(endPrice - startPrice);
            cachedPoints       = cachedPriceRange;
            cachedTicks        = cachedTickSize > 0 ? cachedPriceRange / cachedTickSize : 0;
            cachedDollarValue  = cachedTicks * cachedTickValue * NumContracts;
            cachedNetPnL       = cachedDollarValue - (RoundTripCommission * NumContracts);

            // Percentage
            cachedPercentChange = startPrice != 0 ? (cachedPriceRange / startPrice) * 100.0 : 0;

            // Time and bar calculations
            cachedDuration = laterTime - earlierTime;

            cachedBarCount    = 0;
            cachedTotalVolume = 0;
            cachedBuyVolume   = 0;
            cachedSellVolume  = 0;

            try
            {
                // Use slot indices to count bars in range
                int startSlot = (int)chartControl.GetSlotIndexByTime(earlierTime);
                int endSlot   = (int)chartControl.GetSlotIndexByTime(laterTime);
                cachedBarCount = Math.Abs(endSlot - startSlot);
                if (cachedBarCount == 0) cachedBarCount = 1;

                // Try multiple approaches to access Bars data for volume
                Bars bars = null;

                try
                {
                    // Approach 1: AttachedTo.ChartObject -> ChartBars -> Bars
                    if (AttachedTo != null && AttachedTo.ChartObject != null)
                    {
                        NinjaTrader.Gui.Chart.ChartBars chartBarsObj = AttachedTo.ChartObject as NinjaTrader.Gui.Chart.ChartBars;
                        if (chartBarsObj != null)
                            bars = chartBarsObj.Bars;
                    }
                }
                catch { }

                if (bars == null)
                {
                    try
                    {
                        // Approach 2: Use reflection to find Bars on the chart
                        if (chartControl.OwnerChart != null)
                        {
                            var chartType = chartControl.OwnerChart.GetType();
                            // Try ChartBars property
                            var cbProp = chartType.GetProperty("ChartBars", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (cbProp != null)
                            {
                                var cbObj = cbProp.GetValue(chartControl.OwnerChart) as NinjaTrader.Gui.Chart.ChartBars;
                                if (cbObj != null)
                                    bars = cbObj.Bars;
                            }

                            // Try BarsArray property
                            if (bars == null)
                            {
                                var baProp = chartType.GetProperty("BarsArray", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (baProp != null)
                                {
                                    var baVal = baProp.GetValue(chartControl.OwnerChart) as Bars[];
                                    if (baVal != null && baVal.Length > 0)
                                        bars = baVal[0];
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (bars != null && bars.Count > 0)
                {
                    int sIdx = -1;
                    int eIdx = -1;

                    for (int i = 0; i < bars.Count; i++)
                    {
                        DateTime barTime = bars.GetTime(i);
                        if (barTime >= earlierTime && sIdx == -1)
                            sIdx = i;
                        if (barTime <= laterTime)
                            eIdx = i;
                    }

                    if (sIdx >= 0 && eIdx >= 0 && eIdx >= sIdx)
                    {
                        cachedBarCount = eIdx - sIdx + 1;

                        for (int i = sIdx; i <= eIdx; i++)
                        {
                            try
                            {
                                long vol = bars.GetVolume(i);
                                cachedTotalVolume += vol;

                                double open  = bars.GetOpen(i);
                                double close = bars.GetClose(i);

                                if (close >= open)
                                    cachedBuyVolume += vol;
                                else
                                    cachedSellVolume += vol;
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            cachedDelta           = cachedBuyVolume - cachedSellVolume;
            cachedAvgVolumePerBar = cachedBarCount > 0 ? (double)cachedTotalVolume / cachedBarCount : 0;
            cachedTicksPerBar     = cachedBarCount > 0 ? cachedTicks / cachedBarCount : 0;

            double totalMinutes   = cachedDuration.TotalMinutes;
            cachedDollarsPerMinute = totalMinutes > 0 ? cachedDollarValue / totalMinutes : 0;
        }
        #endregion

        #region Rendering
        public override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (startAnchor == null || endAnchor == null)
                return;

            RenderTarget renderTarget = RenderTarget;
            if (renderTarget == null) return;

            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];

            System.Windows.Point startPtW = startAnchor.GetPoint(chartControl, chartPanel, chartScale);
            System.Windows.Point endPtW   = endAnchor.GetPoint(chartControl, chartPanel, chartScale);
            SharpDX.Vector2 startPoint = new SharpDX.Vector2((float)startPtW.X, (float)startPtW.Y);
            SharpDX.Vector2 endPoint   = new SharpDX.Vector2((float)endPtW.X, (float)endPtW.Y);

            float x1 = Math.Min(startPoint.X, endPoint.X);
            float y1 = Math.Min(startPoint.Y, endPoint.Y);
            float x2 = Math.Max(startPoint.X, endPoint.X);
            float y2 = Math.Max(startPoint.Y, endPoint.Y);

            float width  = x2 - x1;
            float height = y2 - y1;

            if (width < 2 || height < 2) return;

            CalculateMetrics(chartControl, chartScale);
            UpdateResources(renderTarget);

            // Fill color based on direction
            float opacity = FillOpacity / 100f;
            System.Windows.Media.Color fillColor;

            if (AutoColor)
                fillColor = cachedIsLong
                    ? ((System.Windows.Media.SolidColorBrush)LongFillColor).Color
                    : ((System.Windows.Media.SolidColorBrush)ShortFillColor).Color;
            else
                fillColor = ((System.Windows.Media.SolidColorBrush)LongFillColor).Color;

            using (var currentFillBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                new SharpDX.Color4(fillColor.R / 255f, fillColor.G / 255f, fillColor.B / 255f, opacity)))
            {
                var rect = new SharpDX.RectangleF(x1, y1, width, height);
                renderTarget.FillRectangle(rect, currentFillBrush);
                renderTarget.DrawRectangle(rect, borderBrushDx, BorderWidth);
            }

            // Diagonal line
            if (ShowDiagonalLine)
            {
                using (var diagBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                    new SharpDX.Color4(1f, 1f, 1f, 0.3f)))
                {
                    var strokeProps = new StrokeStyleProperties { DashStyle = SharpDX.Direct2D1.DashStyle.Dash };
                    using (var dashStyle = new StrokeStyle(renderTarget.Factory, strokeProps))
                    {
                        renderTarget.DrawLine(startPoint, endPoint, diagBrush, 1, dashStyle);
                    }
                }
            }

            // Build text lines
            List<string> topLines    = new List<string>();
            List<string> bottomLines = new List<string>();

            string dirArrow = cachedIsLong ? "\u25B2" : "\u25BC"; // up/down triangle
            string durationStr = FormatDuration(cachedDuration);

            if (ShowBarsAndTime)
                topLines.Add(string.Format("{0} bars  |  {1}", cachedBarCount, durationStr));

            if (ShowPercentage)
            {
                string pctPrefix = cachedIsLong ? "+" : "-";
                topLines.Add(string.Format("{0} {1}{2:F3}%", dirArrow, pctPrefix, cachedPercentChange));
            }

            if (ShowVelocity)
                topLines.Add(string.Format("{0:F1} ticks/bar", cachedTicksPerBar));

            if (ShowDollarsPerMinute && cachedDuration.TotalMinutes > 0)
                topLines.Add(string.Format("${0:F2}/min", cachedDollarsPerMinute));

            if (ShowPriceTicks)
                bottomLines.Add(string.Format("{0:F2} pts  |  {1:F0} ticks", cachedPoints, cachedTicks));

            if (ShowDollarValue)
            {
                string dollarPrefix = cachedIsLong ? "+" : "-";
                string ctLabel = NumContracts > 1 ? string.Format("  ({0} cts)", NumContracts) : "";
                bottomLines.Add(string.Format("{0}${1:F2}{2}", dollarPrefix, cachedDollarValue, ctLabel));
            }

            if (ShowNetPnL)
            {
                string netPrefix = cachedNetPnL >= 0 ? "+" : "";
                bottomLines.Add(string.Format("Net: {0}${1:F2}", netPrefix, cachedNetPnL));
            }

            if (ShowVolume)
                bottomLines.Add(string.Format("Vol: {0}", FormatVolume(cachedTotalVolume)));

            if (ShowDelta)
            {
                string deltaPrefix = cachedDelta >= 0 ? "+" : "";
                bottomLines.Add(string.Format("Delta: {0}{1}", deltaPrefix, FormatVolume(cachedDelta)));
            }

            if (ShowAvgVolPerBar && cachedBarCount > 0)
                bottomLines.Add(string.Format("Avg Vol/Bar: {0:F0}", cachedAvgVolumePerBar));

            // Render text - calculate minimum width needed for legibility
            float padding    = 4f;
            float lineHeight = FontSize + 4f;
            float minTextWidth = 220f; // Minimum pixel width for readable text
            float rectCenterX = x1 + width / 2f;

            // Measure the widest line across both blocks to determine needed width
            float maxNeededWidth = minTextWidth;
            List<string> allLines = new List<string>(topLines);
            allLines.AddRange(bottomLines);
            foreach (string line in allLines)
            {
                using (var measureLayout = new SharpDX.DirectWrite.TextLayout(
                    NinjaTrader.Core.Globals.DirectWriteFactory,
                    line,
                    textFormatDx,
                    2000f,
                    lineHeight))
                {
                    float measuredWidth = measureLayout.Metrics.WidthIncludingTrailingWhitespace + padding * 4;
                    if (measuredWidth > maxNeededWidth)
                        maxNeededWidth = measuredWidth;
                }
            }

            // Use rect width if wide enough, otherwise use measured minimum centered on rect
            float textBlockWidth = Math.Max(width, maxNeededWidth);
            float textBlockX = (textBlockWidth > width)
                ? rectCenterX - textBlockWidth / 2f
                : x1;

            switch (TextPosition)
            {
                case TextPlacement.Split:
                    RenderTextBlock(renderTarget, topLines, textBlockX, y1 - (topLines.Count * lineHeight) - padding * 2, textBlockWidth, lineHeight, padding);
                    RenderTextBlock(renderTarget, bottomLines, textBlockX, y2 + padding, textBlockWidth, lineHeight, padding);
                    break;

                case TextPlacement.TopOutside:
                    var allTop = new List<string>(topLines);
                    allTop.AddRange(bottomLines);
                    RenderTextBlock(renderTarget, allTop, textBlockX, y1 - (allTop.Count * lineHeight) - padding * 2, textBlockWidth, lineHeight, padding);
                    break;

                case TextPlacement.BottomOutside:
                    var allBottom = new List<string>(topLines);
                    allBottom.AddRange(bottomLines);
                    RenderTextBlock(renderTarget, allBottom, textBlockX, y2 + padding, textBlockWidth, lineHeight, padding);
                    break;

                case TextPlacement.TopInside:
                    var allTopIn = new List<string>(topLines);
                    allTopIn.AddRange(bottomLines);
                    RenderTextBlock(renderTarget, allTopIn, textBlockX, y1 + padding, textBlockWidth, lineHeight, padding);
                    break;

                case TextPlacement.BottomInside:
                    var allBottomIn = new List<string>(topLines);
                    allBottomIn.AddRange(bottomLines);
                    float totalH = allBottomIn.Count * lineHeight + padding * 2;
                    RenderTextBlock(renderTarget, allBottomIn, textBlockX, y2 - totalH, textBlockWidth, lineHeight, padding);
                    break;
            }

            // Selection handles
            if (IsSelected)
            {
                using (var handleBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget, SharpDX.Color.White))
                {
                    float hs = 4f;
                    renderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(x1, y1), hs, hs), handleBrush);
                    renderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(x2, y2), hs, hs), handleBrush);
                    renderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(x1, y2), hs, hs), handleBrush);
                    renderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(x2, y1), hs, hs), handleBrush);
                }
            }
        }

        private void RenderTextBlock(RenderTarget renderTarget, List<string> lines, float x, float y, float availableWidth, float lineHeight, float padding)
        {
            if (lines == null || lines.Count == 0) return;

            float totalHeight = lines.Count * lineHeight + padding * 2;
            var bgRect = new SharpDX.RectangleF(x, y, availableWidth, totalHeight);
            renderTarget.FillRectangle(bgRect, textBackgroundBrushDx);

            for (int i = 0; i < lines.Count; i++)
            {
                float lineY = y + padding + (i * lineHeight);

                using (var textLayout = new SharpDX.DirectWrite.TextLayout(
                    NinjaTrader.Core.Globals.DirectWriteFactory,
                    lines[i],
                    textFormatDx,
                    availableWidth,
                    lineHeight))
                {
                    textLayout.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
                    renderTarget.DrawTextLayout(new SharpDX.Vector2(x, lineY), textLayout, textBrushDx);
                }
            }
        }
        #endregion

        #region Resource Management
        private void UpdateResources(RenderTarget renderTarget)
        {
            DisposeResources();

            var bc = ((System.Windows.Media.SolidColorBrush)BorderColor).Color;
            borderBrushDx = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                new SharpDX.Color4(bc.R / 255f, bc.G / 255f, bc.B / 255f, BorderOpacity / 100f));

            var tc = ((System.Windows.Media.SolidColorBrush)TextColor).Color;
            textBrushDx = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                new SharpDX.Color4(tc.R / 255f, tc.G / 255f, tc.B / 255f, 1f));

            var tbg = ((System.Windows.Media.SolidColorBrush)TextBackgroundColor).Color;
            textBackgroundBrushDx = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                new SharpDX.Color4(tbg.R / 255f, tbg.G / 255f, tbg.B / 255f, TextBackgroundOpacity / 100f));

            textFormatDx = new SharpDX.DirectWrite.TextFormat(
                NinjaTrader.Core.Globals.DirectWriteFactory,
                "Consolas",
                SharpDX.DirectWrite.FontWeight.Normal,
                SharpDX.DirectWrite.FontStyle.Normal,
                FontSize);
        }

        private void DisposeResources()
        {
            if (borderBrushDx != null) { borderBrushDx.Dispose(); borderBrushDx = null; }
            if (textBrushDx != null) { textBrushDx.Dispose(); textBrushDx = null; }
            if (textBackgroundBrushDx != null) { textBackgroundBrushDx.Dispose(); textBackgroundBrushDx = null; }
            if (textFormatDx != null) { textFormatDx.Dispose(); textFormatDx = null; }
        }
        #endregion

        #region Helpers
        private string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalDays >= 1)
                return string.Format("{0}d {1:D2}:{2:D2}:{3:D2}", (int)ts.TotalDays, ts.Hours, ts.Minutes, ts.Seconds);
            else if (ts.TotalHours >= 1)
                return string.Format("{0}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            else
                return string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
        }

        private string FormatVolume(long volume)
        {
            long abs = Math.Abs(volume);
            string sign = volume < 0 ? "-" : "";
            if (abs >= 1000000)
                return string.Format("{0}{1:F1}M", sign, abs / 1000000.0);
            else if (abs >= 1000)
                return string.Format("{0}{1:F1}K", sign, abs / 1000.0);
            else
                return volume.ToString("N0");
        }
        #endregion
    }

}

