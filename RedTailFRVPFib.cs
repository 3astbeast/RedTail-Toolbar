#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
#endregion

//This code is subject to the terms of the Mozilla Public License 2.0 at https://mozilla.org/MPL/2.0/
//Created by RedTail Indicators - @_hawkeye_13
//RedTail Fixed Range Volume Profile with Fibonacci Levels
//Version: v2.0.0 - Matched profile rendering to RedTail Volume Profile V2 (gradient fill, adaptive rendering, polarity coloring)

namespace NinjaTrader.NinjaScript.DrawingTools
{
    // Enum must be at namespace level for proper XML template serialization
    public enum RedTailVPAlignment
    {
        Left,
        Right
    }

    public enum RedTailVolumeType
    {
        Standard,
        Bullish,
        Bearish,
        Both
    }

    public enum RedTailRenderQuality
    {
        Manual,
        Adaptive
    }

    public class RedTailFRVPFib : DrawingTool
    {
        #region Enums
        // Enum moved to namespace level above for serialization compatibility
        #endregion

        #region Variables
        private ChartAnchor startAnchor;
        private ChartAnchor endAnchor;

        private ChartAnchor editingAnchor;
        private bool isMoving;
        private DateTime moveStartTime;
        private DateTime moveEndTime;
        private double moveStartPrice;
        private double moveEndPrice;

        // Volume profile data
        private List<double> volumes = new List<double>();

        private double profileHighestPrice;
        private double profileLowestPrice;
        private double profilePriceInterval;
        private int pocIndex = -1;
        private int vaUpIndex = -1;
        private int vaDownIndex = -1;
        private double maxVolume = 0;
        private bool profileDirty = true;
        
        // Cached bar range for profile calculation
        private DateTime lastCalcStartTime = DateTime.MinValue;
        private DateTime lastCalcEndTime = DateTime.MinValue;
        
        // AVWAP data
        private List<KeyValuePair<int, double>> avwapPoints = new List<KeyValuePair<int, double>>();
        private int avwapAnchorBarIdx = -1;
        private DateTime lastAvwapCalcTime = DateTime.MinValue;
        
        // Cluster levels data
        private List<ClusterLevelInfo> clusterLevels = new List<ClusterLevelInfo>();
        
        // Volume polarity tracking (bullish/bearish dominant per row)
        private List<bool> volumePolarities = new List<bool>();
        
        private struct ClusterLevelInfo
        {
            public double POCPrice;
            public double POCVolume;
            public double ClusterHigh;
            public double ClusterLow;
            public double TotalVolume;
            public int BarCount;
        }
        #endregion

        #region Anchors
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolAnchor", GroupName = "NinjaScriptGeneral")]
        public ChartAnchor StartAnchor
        {
            get { if (startAnchor == null) startAnchor = new ChartAnchor(); return startAnchor; }
            set { startAnchor = value; }
        }

        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolAnchor", GroupName = "NinjaScriptGeneral")]
        public ChartAnchor EndAnchor
        {
            get { if (endAnchor == null) endAnchor = new ChartAnchor(); return endAnchor; }
            set { endAnchor = value; }
        }

        public override IEnumerable<ChartAnchor> Anchors
        {
            get { return new[] { StartAnchor, EndAnchor }; }
        }
        #endregion

        #region Fib Level Helper
        private struct FibLevelInfo
        {
            public double Ratio;
            public System.Windows.Media.Brush Color;
        }

        private List<FibLevelInfo> GetActiveFibLevels()
        {
            var list = new List<FibLevelInfo>();
            double[] vals = { FibLevel1, FibLevel2, FibLevel3, FibLevel4, FibLevel5, FibLevel6, FibLevel7, FibLevel8, FibLevel9, FibLevel10 };
            System.Windows.Media.Brush[] cols = { FibLevel1Color, FibLevel2Color, FibLevel3Color, FibLevel4Color, FibLevel5Color,
                                                   FibLevel6Color, FibLevel7Color, FibLevel8Color, FibLevel9Color, FibLevel10Color };
            for (int i = 0; i < 10; i++)
            {
                if (vals[i] >= 0)
                    list.Add(new FibLevelInfo { Ratio = vals[i] / 100.0, Color = cols[i] ?? Brushes.DodgerBlue });
            }
            return list;
        }
        #endregion

        #region State Management
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name            = "RedTailFRVPFib";
                Description     = "RedTail Fixed Range Volume Profile with Fibonacci Levels. Click two points to define the range.";
                DrawingState    = DrawingState.Building;

                // Volume Profile Settings =====
                NumberOfRows        = 250;
                ProfileWidth        = 30;
                VPAlignment         = RedTailVPAlignment.Left;
                BarColor            = Brushes.Gray;
                BarOpacity          = 40;
                BarThickness        = 2;
                
                // Volume Type (Polarity)
                VolumeType          = RedTailVolumeType.Standard;
                BullishBarColor     = Brushes.Green;
                BearishBarColor     = Brushes.Red;
                
                // Gradient Fill
                EnableGradientFill  = false;
                GradientIntensity   = 70;
                
                // Adaptive Rendering
                RenderQuality       = RedTailRenderQuality.Adaptive;
                SmoothingPasses     = 2;
                MinBarPixelHeight   = 2.0f;
                MaxBarPixelHeight   = 8.0f;

                // POC
                DisplayPoC          = true;
                PoCColor            = Brushes.Red;
                PoCLineWidth        = 2;
                PoCLineStyle        = DashStyleHelper.Solid;
                PoCOpacity          = 100;

                // Value Area
                DisplayValueArea    = true;
                ValueAreaPct        = 68;
                ValueAreaBarColor   = Brushes.RoyalBlue;
                DisplayVALines      = true;
                VALineColor         = Brushes.Gold;
                VALineWidth         = 1;
                VALineStyle         = DashStyleHelper.Dash;
                VALineOpacity       = 80;

                // Labels
                ShowLabels          = true;
                LabelFontSize       = 10;
                ShowPriceOnLabel    = true;

                // Boundary
                BoundaryColor       = Brushes.White;
                BoundaryOpacity     = 30;
                BoundaryWidth       = 1;

                // ===== Fibonacci Settings =====
                DisplayFibs         = true;
                FibLineWidth        = 1;
                FibLineDashStyle    = DashStyleHelper.Dot;
                FibOpacity          = 80;
                ExtendFibsRight     = false;
                FibLabelFontSize    = 10;
                ShowFibPrice        = true;

                FibLevel1  = 0;       FibLevel1Color  = Brushes.Gray;
                FibLevel2  = 23.6;    FibLevel2Color  = Brushes.DodgerBlue;
                FibLevel3  = 38.2;    FibLevel3Color  = Brushes.DodgerBlue;
                FibLevel4  = 50;      FibLevel4Color  = Brushes.Gold;
                FibLevel5  = 61.8;    FibLevel5Color  = Brushes.Red;
                FibLevel6  = 78.6;    FibLevel6Color  = Brushes.OrangeRed;
                FibLevel7  = 100;     FibLevel7Color  = Brushes.Gray;
                FibLevel8  = -1;      FibLevel8Color  = Brushes.Cyan;
                FibLevel9  = -1;      FibLevel9Color  = Brushes.Magenta;
                FibLevel10 = -1;      FibLevel10Color = Brushes.LimeGreen;

                // ===== AVWAP Settings =====
                DisplayAVWAP           = true;
                AVWAPColor             = Brushes.DodgerBlue;
                AVWAPLineWidth         = 2;
                AVWAPLineStyle         = DashStyleHelper.Solid;
                AVWAPOpacity           = 100;
                ExtendAVWAPRight       = true;
                ShowAVWAPLabel         = true;

                // ===== Cluster Levels Settings =====
                DisplayClusterLevels        = false;
                ClusterCount                = 5;
                ClusterIterations           = 50;
                ClusterRowsPerLevel         = 20;
                ClusterLineWidth            = 2;
                ClusterLineStyle            = DashStyleHelper.Dash;
                ClusterOpacity              = 80;
                ExtendClustersRight         = false;
                ShowClusterLabels           = true;
                Cluster1LevelColor          = Brushes.DodgerBlue;
                Cluster2LevelColor          = Brushes.Tomato;
                Cluster3LevelColor          = Brushes.LimeGreen;
                Cluster4LevelColor          = Brushes.Orange;
                Cluster5LevelColor          = Brushes.MediumPurple;
                Cluster6LevelColor          = Brushes.DarkCyan;
                Cluster7LevelColor          = Brushes.Gold;
                Cluster8LevelColor          = Brushes.DeepPink;
                Cluster9LevelColor          = Brushes.SaddleBrown;
                Cluster10LevelColor         = Brushes.SlateGray;

                startAnchor = new ChartAnchor();
                endAnchor   = new ChartAnchor();
            }
        }

        public override void CopyTo(NinjaScript ninjaScript)
        {
            base.CopyTo(ninjaScript);

            RedTailFRVPFib copy = ninjaScript as RedTailFRVPFib;
            if (copy == null) return;

            // Volume Profile
            copy.NumberOfRows       = NumberOfRows;
            copy.ProfileWidth       = ProfileWidth;
            copy.VPAlignment        = VPAlignment;
            copy.BarColor           = BarColor;
            copy.BarOpacity         = BarOpacity;
            copy.BarThickness       = BarThickness;
            copy.VolumeType         = VolumeType;
            copy.BullishBarColor    = BullishBarColor;
            copy.BearishBarColor    = BearishBarColor;
            copy.EnableGradientFill = EnableGradientFill;
            copy.GradientIntensity  = GradientIntensity;
            copy.RenderQuality      = RenderQuality;
            copy.SmoothingPasses    = SmoothingPasses;
            copy.MinBarPixelHeight  = MinBarPixelHeight;
            copy.MaxBarPixelHeight  = MaxBarPixelHeight;

            // POC
            copy.DisplayPoC         = DisplayPoC;
            copy.PoCColor           = PoCColor;
            copy.PoCLineWidth       = PoCLineWidth;
            copy.PoCLineStyle       = PoCLineStyle;
            copy.PoCOpacity         = PoCOpacity;

            // Value Area
            copy.DisplayValueArea   = DisplayValueArea;
            copy.ValueAreaPct       = ValueAreaPct;
            copy.ValueAreaBarColor  = ValueAreaBarColor;
            copy.DisplayVALines     = DisplayVALines;
            copy.VALineColor        = VALineColor;
            copy.VALineWidth        = VALineWidth;
            copy.VALineStyle        = VALineStyle;
            copy.VALineOpacity      = VALineOpacity;

            // Labels
            copy.ShowLabels         = ShowLabels;
            copy.LabelFontSize      = LabelFontSize;
            copy.ShowPriceOnLabel   = ShowPriceOnLabel;

            // Boundary
            copy.BoundaryColor      = BoundaryColor;
            copy.BoundaryOpacity    = BoundaryOpacity;
            copy.BoundaryWidth      = BoundaryWidth;

            // Fibonacci
            copy.DisplayFibs        = DisplayFibs;
            copy.FibLineWidth       = FibLineWidth;
            copy.FibLineDashStyle   = FibLineDashStyle;
            copy.FibOpacity         = FibOpacity;
            copy.ExtendFibsRight    = ExtendFibsRight;
            copy.FibLabelFontSize   = FibLabelFontSize;
            copy.ShowFibPrice       = ShowFibPrice;

            copy.FibLevel1  = FibLevel1;   copy.FibLevel1Color  = FibLevel1Color;
            copy.FibLevel2  = FibLevel2;   copy.FibLevel2Color  = FibLevel2Color;
            copy.FibLevel3  = FibLevel3;   copy.FibLevel3Color  = FibLevel3Color;
            copy.FibLevel4  = FibLevel4;   copy.FibLevel4Color  = FibLevel4Color;
            copy.FibLevel5  = FibLevel5;   copy.FibLevel5Color  = FibLevel5Color;
            copy.FibLevel6  = FibLevel6;   copy.FibLevel6Color  = FibLevel6Color;
            copy.FibLevel7  = FibLevel7;   copy.FibLevel7Color  = FibLevel7Color;
            copy.FibLevel8  = FibLevel8;   copy.FibLevel8Color  = FibLevel8Color;
            copy.FibLevel9  = FibLevel9;   copy.FibLevel9Color  = FibLevel9Color;
            copy.FibLevel10 = FibLevel10;  copy.FibLevel10Color = FibLevel10Color;

            // AVWAP
            copy.DisplayAVWAP       = DisplayAVWAP;
            copy.AVWAPColor         = AVWAPColor;
            copy.AVWAPLineWidth     = AVWAPLineWidth;
            copy.AVWAPLineStyle     = AVWAPLineStyle;
            copy.AVWAPOpacity       = AVWAPOpacity;
            copy.ExtendAVWAPRight   = ExtendAVWAPRight;
            copy.ShowAVWAPLabel     = ShowAVWAPLabel;

            // Cluster Levels
            copy.DisplayClusterLevels    = DisplayClusterLevels;
            copy.ClusterCount            = ClusterCount;
            copy.ClusterIterations       = ClusterIterations;
            copy.ClusterRowsPerLevel     = ClusterRowsPerLevel;
            copy.ClusterLineWidth        = ClusterLineWidth;
            copy.ClusterLineStyle        = ClusterLineStyle;
            copy.ClusterOpacity          = ClusterOpacity;
            copy.ExtendClustersRight     = ExtendClustersRight;
            copy.ShowClusterLabels       = ShowClusterLabels;
            copy.Cluster1LevelColor      = Cluster1LevelColor;
            copy.Cluster2LevelColor      = Cluster2LevelColor;
            copy.Cluster3LevelColor      = Cluster3LevelColor;
            copy.Cluster4LevelColor      = Cluster4LevelColor;
            copy.Cluster5LevelColor      = Cluster5LevelColor;
            copy.Cluster6LevelColor      = Cluster6LevelColor;
            copy.Cluster7LevelColor      = Cluster7LevelColor;
            copy.Cluster8LevelColor      = Cluster8LevelColor;
            copy.Cluster9LevelColor      = Cluster9LevelColor;
            copy.Cluster10LevelColor     = Cluster10LevelColor;
        }
        #endregion

        #region Mouse Events
        public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, System.Windows.Point point)
        {
            if (DrawingState == DrawingState.Building) return Cursors.Cross;
            if (DrawingState == DrawingState.Moving)   return Cursors.SizeAll;
            if (DrawingState == DrawingState.Editing)  return Cursors.SizeNWSE;

            if (IsSelected)
            {
                ChartAnchor closest = GetClosestAnchor(chartControl, chartPanel, chartScale, point, 15);
                if (closest != null) return Cursors.SizeNWSE;
            }

            if (IsPointNearDrawing(chartControl, chartPanel, chartScale, point))
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
                    IsSelected = false;
                    profileDirty = true;
                }
            }
            else if (DrawingState == DrawingState.Normal && IsSelected)
            {
                System.Windows.Point mousePoint = dataPoint.GetPoint(chartControl, chartPanel, chartScale);

                ChartAnchor closest = GetClosestAnchor(chartControl, chartPanel, chartScale, mousePoint, 15);
                if (closest != null)
                {
                    editingAnchor = closest;
                    editingAnchor.IsEditing = true;
                    DrawingState = DrawingState.Editing;
                }
                else if (IsPointNearDrawing(chartControl, chartPanel, chartScale, mousePoint))
                {
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
                profileDirty = true;
            }
            else if (DrawingState == DrawingState.Editing && editingAnchor != null)
            {
                dataPoint.CopyDataValues(editingAnchor);
                profileDirty = true;
            }
            else if (DrawingState == DrawingState.Moving && isMoving && editingAnchor != null)
            {
                double deltaPrice  = dataPoint.Price - editingAnchor.Price;
                TimeSpan deltaTime = dataPoint.Time  - editingAnchor.Time;
                startAnchor.Price  = moveStartPrice + deltaPrice;
                startAnchor.Time   = moveStartTime  + deltaTime;
                endAnchor.Price    = moveEndPrice   + deltaPrice;
                endAnchor.Time     = moveEndTime    + deltaTime;
                profileDirty = true;
            }
        }

        public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Editing)
            {
                if (editingAnchor != null) editingAnchor.IsEditing = false;
                editingAnchor = null;
                DrawingState  = DrawingState.Normal;
                profileDirty = true;
            }
            else if (DrawingState == DrawingState.Moving)
            {
                isMoving      = false;
                editingAnchor = null;
                DrawingState  = DrawingState.Normal;
                profileDirty = true;
            }
        }

        private bool IsPointNearDrawing(ChartControl cc, ChartPanel cp, ChartScale cs, System.Windows.Point pt)
        {
            if (startAnchor == null || endAnchor == null) return false;
            try
            {
                System.Windows.Point sp = startAnchor.GetPoint(cc, cp, cs);
                System.Windows.Point ep = endAnchor.GetPoint(cc, cp, cs);

                // Check if point is inside the bounding rectangle of the profile
                double xMin = Math.Min(sp.X, ep.X) - 5;
                double xMax = Math.Max(sp.X, ep.X) + 5;
                double yMin = Math.Min(sp.Y, ep.Y) - 5;
                double yMax = Math.Max(sp.Y, ep.Y) + 5;

                if (pt.X >= xMin && pt.X <= xMax && pt.Y >= yMin && pt.Y <= yMax)
                    return true;

                // Near any fib level line
                if (DisplayFibs)
                {
                    double startPrice = startAnchor.Price;
                    double endPrice   = endAnchor.Price;
                    double range      = startPrice - endPrice;
                    if (Math.Abs(range) < double.Epsilon) return false;

                    float lineXMin = (float)Math.Min(sp.X, ep.X) - 15;
                    float lineXMax = ExtendFibsRight ? (float)cp.W : (float)Math.Max(sp.X, ep.X) + 15;

                    foreach (var lv in GetActiveFibLevels())
                    {
                        double price = endPrice + range * lv.Ratio;
                        float y = cs.GetYByValue(price);
                        if (pt.X >= lineXMin && pt.X <= lineXMax && Math.Abs(pt.Y - y) < 10)
                            return true;
                    }
                }

                // Near AVWAP line
                if (DisplayAVWAP && avwapPoints.Count > 0)
                {
                    foreach (var kvp in avwapPoints)
                    {
                        try
                        {
                            float bx = cc.GetXByBarIndex(cc.BarsArray[0], kvp.Key);
                            float by = cs.GetYByValue(kvp.Value);
                            if (Math.Abs(pt.X - bx) < 10 && Math.Abs(pt.Y - by) < 10)
                                return true;
                        }
                        catch { }
                    }
                }

                // Near cluster level lines
                if (DisplayClusterLevels && clusterLevels.Count > 0)
                {
                    float lineXMin = (float)Math.Min(sp.X, ep.X) - 15;
                    float lineXMax = ExtendClustersRight ? (float)cp.W : (float)Math.Max(sp.X, ep.X) + 15;
                    foreach (var cl in clusterLevels)
                    {
                        float y = cs.GetYByValue(cl.POCPrice);
                        if (pt.X >= lineXMin && pt.X <= lineXMax && Math.Abs(pt.Y - y) < 10)
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private ChartAnchor GetClosestAnchor(ChartControl cc, ChartPanel cp, ChartScale cs, System.Windows.Point point, double maxDist)
        {
            try
            {
                System.Windows.Point sp = startAnchor.GetPoint(cc, cp, cs);
                System.Windows.Point ep = endAnchor.GetPoint(cc, cp, cs);
                double d1 = Math.Sqrt(Math.Pow(point.X - sp.X, 2) + Math.Pow(point.Y - sp.Y, 2));
                double d2 = Math.Sqrt(Math.Pow(point.X - ep.X, 2) + Math.Pow(point.Y - ep.Y, 2));
                if (d1 <= maxDist && d1 <= d2) return startAnchor;
                if (d2 <= maxDist) return endAnchor;
            }
            catch { }
            return null;
        }
        #endregion

        #region Hit Test
        public override bool IsVisibleOnChart(ChartControl chartControl, ChartScale chartScale, DateTime firstTimeOnChart, DateTime lastTimeOnChart) { return true; }
        public override bool IsAlertConditionTrue(AlertConditionItem alertConditionItem, Condition condition, ChartAlertValue[] values, ChartControl chartControl, ChartScale chartScale) { return false; }
        public override IEnumerable<AlertConditionItem> GetAlertConditionItems() { return new AlertConditionItem[] { }; }

        public override System.Windows.Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
        {
            if (startAnchor == null || endAnchor == null) return new System.Windows.Point[0];
            ChartPanel cp = chartControl.ChartPanels[chartScale.PanelIndex];
            return new[] { startAnchor.GetPoint(chartControl, cp, chartScale), endAnchor.GetPoint(chartControl, cp, chartScale) };
        }
        #endregion

        #region Volume Profile Calculation
        private void CalculateVolumeProfile(ChartControl chartControl, ChartBars chartBars)
        {
            if (chartControl == null || chartBars == null || startAnchor == null || endAnchor == null)
                return;

            Bars bars = chartBars.Bars;
            if (bars == null || bars.Count == 0)
                return;

            // Determine the time range
            DateTime rangeStart = startAnchor.Time < endAnchor.Time ? startAnchor.Time : endAnchor.Time;
            DateTime rangeEnd   = startAnchor.Time < endAnchor.Time ? endAnchor.Time : startAnchor.Time;

            // Check if we already calculated for this range
            if (!profileDirty && rangeStart == lastCalcStartTime && rangeEnd == lastCalcEndTime && volumes.Count > 0)
                return;

            lastCalcStartTime = rangeStart;
            lastCalcEndTime   = rangeEnd;
            profileDirty = false;
            lastAvwapCalcTime = DateTime.MinValue; // force AVWAP recalc when profile recalculates

            volumes.Clear();
            volumePolarities.Clear();
            pocIndex = -1;
            vaUpIndex = -1;
            vaDownIndex = -1;
            maxVolume = 0;

            for (int i = 0; i < NumberOfRows; i++)
            {
                volumes.Add(0);
                volumePolarities.Add(true); // Default bullish
            }
            
            // Separate bullish/bearish volume tracking for polarity
            double[] bullishVolume = new double[NumberOfRows];
            double[] bearishVolume = new double[NumberOfRows];

            // Find bars within the time range and determine price extremes
            profileHighestPrice = double.MinValue;
            profileLowestPrice  = double.MaxValue;

            // First pass: find price range
            int startBarIdx = -1;
            int endBarIdx   = -1;

            for (int i = 0; i < bars.Count; i++)
            {
                DateTime barTime = bars.GetTime(i);
                if (barTime < rangeStart) continue;
                if (barTime > rangeEnd) break;

                if (startBarIdx == -1) startBarIdx = i;
                endBarIdx = i;

                double high = bars.GetHigh(i);
                double low  = bars.GetLow(i);
                profileHighestPrice = Math.Max(profileHighestPrice, high);
                profileLowestPrice  = Math.Min(profileLowestPrice, low);
            }

            if (startBarIdx == -1 || endBarIdx == -1 || profileHighestPrice <= profileLowestPrice)
                return;

            profilePriceInterval = (profileHighestPrice - profileLowestPrice) / (NumberOfRows - 1);
            if (profilePriceInterval <= 0) return;

            for (int i = startBarIdx; i <= endBarIdx; i++)
            {
                DateTime barTime = bars.GetTime(i);
                if (barTime < rangeStart || barTime > rangeEnd) continue;

                double barLow    = bars.GetLow(i);
                double barHigh   = bars.GetHigh(i);
                double barOpen   = bars.GetOpen(i);
                double barClose  = bars.GetClose(i);
                double barVolume = bars.GetVolume(i);
                bool isBullish   = barClose >= barOpen;

                int minPriceIndex = Math.Max(0, Math.Min((int)Math.Floor((barLow - profileLowestPrice) / profilePriceInterval), NumberOfRows - 1));
                int maxPriceIndex = Math.Max(0, Math.Min((int)Math.Ceiling((barHigh - profileLowestPrice) / profilePriceInterval), NumberOfRows - 1));

                int touchedLevels = maxPriceIndex - minPriceIndex + 1;
                if (touchedLevels > 0)
                {
                    double volumePerLevel = barVolume / touchedLevels;
                    
                    bool includeVol = VolumeType == RedTailVolumeType.Standard ||
                                     VolumeType == RedTailVolumeType.Both ||
                                     (VolumeType == RedTailVolumeType.Bullish && isBullish) ||
                                     (VolumeType == RedTailVolumeType.Bearish && !isBullish);
                    
                    if (includeVol)
                    {
                        for (int j = minPriceIndex; j <= maxPriceIndex; j++)
                        {
                            volumes[j] += volumePerLevel;
                            if (isBullish)
                                bullishVolume[j] += volumePerLevel;
                            else
                                bearishVolume[j] += volumePerLevel;
                        }
                    }
                }
            }
            
            // Set polarity for each row
            for (int i = 0; i < NumberOfRows; i++)
            {
                volumePolarities[i] = bullishVolume[i] >= bearishVolume[i];
            }

            // Find POC
            maxVolume = 0;
            pocIndex = 0;

            for (int i = 0; i < NumberOfRows; i++)
            {
                if (volumes[i] > maxVolume)
                {
                    maxVolume = volumes[i];
                    pocIndex = i;
                }
            }

            // Calculate value area
            if (maxVolume > 0)
                CalculateValueArea();
        }

        private void CalculateValueArea()
        {
            double sumVolume = 0;
            for (int i = 0; i < volumes.Count; i++)
                sumVolume += volumes[i];

            double vaVolume = sumVolume * ValueAreaPct / 100.0;

            vaUpIndex   = pocIndex;
            vaDownIndex = pocIndex;
            double vaSum = maxVolume;

            while (vaSum < vaVolume)
            {
                double vUp   = (vaUpIndex < NumberOfRows - 1) ? volumes[vaUpIndex + 1] : 0.0;
                double vDown = (vaDownIndex > 0) ? volumes[vaDownIndex - 1] : 0.0;

                if (vUp == 0 && vDown == 0)
                    break;

                if (vUp >= vDown)
                {
                    vaSum += vUp;
                    vaUpIndex++;
                }
                else
                {
                    vaSum += vDown;
                    vaDownIndex--;
                }
            }
        }

        private void CalculateAVWAP(ChartControl chartControl, ChartBars chartBars)
        {
            if (!DisplayAVWAP || chartControl == null || chartBars == null || startAnchor == null)
                return;

            Bars bars = chartBars.Bars;
            if (bars == null || bars.Count == 0) return;

            // The AVWAP anchors at the start anchor's time (where user first clicked)
            DateTime anchorTime = startAnchor.Time;
            
            // Only recalculate if anchor moved
            if (anchorTime == lastAvwapCalcTime && avwapPoints.Count > 0)
                return;
            
            lastAvwapCalcTime = anchorTime;
            avwapPoints.Clear();
            avwapAnchorBarIdx = -1;

            // Find the anchor bar index
            int anchorIdx = -1;
            for (int i = 0; i < bars.Count; i++)
            {
                if (bars.GetTime(i) >= anchorTime)
                {
                    anchorIdx = i;
                    break;
                }
            }
            if (anchorIdx < 0) return;
            avwapAnchorBarIdx = anchorIdx;

            // Calculate AVWAP from anchor bar forward through ALL bars
            double cumVolume = 0;
            double cumTypicalVolume = 0;

            for (int i = anchorIdx; i < bars.Count; i++)
            {
                double high = bars.GetHigh(i);
                double low  = bars.GetLow(i);
                double open = bars.GetOpen(i);
                double close = bars.GetClose(i);
                double vol  = bars.GetVolume(i);
                double source = (open + high + low + close) / 4.0;

                cumVolume += vol;
                cumTypicalVolume += source * vol;

                if (cumVolume > 0)
                {
                    double vwapValue = cumTypicalVolume / cumVolume;
                    avwapPoints.Add(new KeyValuePair<int, double>(i, vwapValue));
                }
            }
        }
        
        private void CalculateClusterLevels(ChartControl chartControl, ChartBars chartBars)
        {
            clusterLevels.Clear();
            
            if (!DisplayClusterLevels || chartControl == null || chartBars == null || startAnchor == null || endAnchor == null)
                return;
            
            Bars bars = chartBars.Bars;
            if (bars == null || bars.Count == 0) return;
            
            DateTime rangeStart = startAnchor.Time < endAnchor.Time ? startAnchor.Time : endAnchor.Time;
            DateTime rangeEnd   = startAnchor.Time < endAnchor.Time ? endAnchor.Time   : startAnchor.Time;
            
            // Gather bar data within the range
            var prices  = new List<double>();
            var volList = new List<double>();
            var highList = new List<double>();
            var lowList  = new List<double>();
            
            for (int i = 0; i < bars.Count; i++)
            {
                DateTime barTime = bars.GetTime(i);
                if (barTime < rangeStart) continue;
                if (barTime > rangeEnd) break;
                
                double h = bars.GetHigh(i);
                double l = bars.GetLow(i);
                double v = bars.GetVolume(i);
                double p = (h + l) / 2.0;
                
                prices.Add(p);
                volList.Add(v);
                highList.Add(h);
                lowList.Add(l);
            }
            
            int n = prices.Count;
            if (n < 2) return;
            
            int k = Math.Min(ClusterCount, n);
            
            // Find price range for centroid initialization
            double minP = double.MaxValue, maxP = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (prices[i] < minP) minP = prices[i];
                if (prices[i] > maxP) maxP = prices[i];
            }
            if (maxP <= minP) return;
            
            // Initialize centroids evenly spaced
            double[] cents = new double[k];
            double step = (maxP - minP) / (k + 1);
            for (int i = 0; i < k; i++)
                cents[i] = minP + (i + 1) * step;
            
            // K-Means iterations (volume-weighted)
            int[] assign = new int[n];
            
            for (int iter = 0; iter < ClusterIterations; iter++)
            {
                // Assignment
                for (int i = 0; i < n; i++)
                {
                    int bestK = 0;
                    double minDist = double.MaxValue;
                    for (int j = 0; j < k; j++)
                    {
                        double dist = Math.Abs(prices[i] - cents[j]);
                        if (dist < minDist) { minDist = dist; bestK = j; }
                    }
                    assign[i] = bestK;
                }
                
                // Update centroids (volume-weighted)
                double[] sumPV = new double[k];
                double[] sumV  = new double[k];
                for (int i = 0; i < n; i++)
                {
                    int c = assign[i];
                    sumPV[c] += prices[i] * volList[i];
                    sumV[c]  += volList[i];
                }
                for (int j = 0; j < k; j++)
                {
                    if (sumV[j] > 0) cents[j] = sumPV[j] / sumV[j];
                }
            }
            
            // Build per-cluster volume profiles to find each cluster's POC
            for (int cId = 0; cId < k; cId++)
            {
                double cMin = double.MaxValue, cMax = double.MinValue;
                double cTotalVol = 0;
                int cBarCount = 0;
                
                var cHighs = new List<double>();
                var cLows  = new List<double>();
                var cVols  = new List<double>();
                
                for (int i = 0; i < n; i++)
                {
                    if (assign[i] != cId) continue;
                    
                    cHighs.Add(highList[i]);
                    cLows.Add(lowList[i]);
                    cVols.Add(volList[i]);
                    
                    if (lowList[i] < cMin) cMin = lowList[i];
                    if (highList[i] > cMax) cMax = highList[i];
                    cTotalVol += volList[i];
                    cBarCount++;
                }
                
                if (cBarCount == 0 || cMax <= cMin) continue;
                
                int rows = ClusterRowsPerLevel;
                double binSize = (cMax - cMin) / rows;
                if (binSize <= 0) continue;
                
                double[] binVols = new double[rows];
                
                for (int i = 0; i < cHighs.Count; i++)
                {
                    double bH = cHighs[i], bL = cLows[i], bV = cVols[i];
                    double wickRange = Math.Max(bH - bL, profilePriceInterval > 0 ? profilePriceInterval : 0.01);
                    
                    for (int bIdx = 0; bIdx < rows; bIdx++)
                    {
                        double binBot = cMin + bIdx * binSize;
                        double binTop = binBot + binSize;
                        double intersectL = Math.Max(bL, binBot);
                        double intersectH = Math.Min(bH, binTop);
                        
                        if (intersectH > intersectL)
                            binVols[bIdx] += bV * (intersectH - intersectL) / wickRange;
                    }
                }
                
                // Find POC bin
                double maxBinVol = 0;
                int pocIdx = 0;
                for (int bIdx = 0; bIdx < rows; bIdx++)
                {
                    if (binVols[bIdx] > maxBinVol) { maxBinVol = binVols[bIdx]; pocIdx = bIdx; }
                }
                
                double pocPrice = cMin + pocIdx * binSize + binSize / 2.0;
                
                clusterLevels.Add(new ClusterLevelInfo
                {
                    POCPrice    = pocPrice,
                    POCVolume   = maxBinVol,
                    ClusterHigh = cMax,
                    ClusterLow  = cMin,
                    TotalVolume = cTotalVol,
                    BarCount    = cBarCount
                });
            }
            
            // Sort by price for consistent display
            clusterLevels.Sort((a, b) => a.POCPrice.CompareTo(b.POCPrice));
        }
        #endregion

        #region Rendering
        public override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (chartControl == null || chartScale == null) return;
            SharpDX.Direct2D1.RenderTarget rt = RenderTarget;
            if (rt == null) return;
            ChartPanel cp = chartControl.ChartPanels[chartScale.PanelIndex];
            if (cp == null) return;

            System.Windows.Point startPt = startAnchor.GetPoint(chartControl, cp, chartScale);
            System.Windows.Point endPt   = endAnchor.GetPoint(chartControl, cp, chartScale);

            // Determine anchor-based coordinates
            float xLeft  = (float)Math.Min(startPt.X, endPt.X);
            float xRight = (float)Math.Max(startPt.X, endPt.X);
            float yTop   = (float)Math.Min(startPt.Y, endPt.Y);
            float yBot   = (float)Math.Max(startPt.Y, endPt.Y);
            
            float rangeWidth = xRight - xLeft;
            if (rangeWidth < 5) rangeWidth = 5;

            // Calculate volume profile from bar data
            if (chartControl.BarsArray != null && chartControl.BarsArray.Count > 0)
            {
                CalculateVolumeProfile(chartControl, chartControl.BarsArray[0]);
                CalculateAVWAP(chartControl, chartControl.BarsArray[0]);
                CalculateClusterLevels(chartControl, chartControl.BarsArray[0]);
            }

            // Determine profile pixel width as percentage of the range width
            float profileWidthPixels = rangeWidth * (ProfileWidth / 100f);

            // ===========================
            // 1. DRAW BOUNDARY RECTANGLE
            // ===========================
            float boundaryOpacity = BoundaryOpacity / 100f;
            SharpDX.Color4 boundaryC4 = BrushToColor4(BoundaryColor, boundaryOpacity);
            using (var boundaryBr = new SharpDX.Direct2D1.SolidColorBrush(rt, boundaryC4))
            {
                // Filled background
                using (var bgBr = new SharpDX.Direct2D1.SolidColorBrush(rt, new SharpDX.Color4(boundaryC4.Red, boundaryC4.Green, boundaryC4.Blue, boundaryOpacity * 0.1f)))
                    rt.FillRectangle(new SharpDX.RectangleF(xLeft, yTop, rangeWidth, yBot - yTop), bgBr);

                // Border
                rt.DrawRectangle(new SharpDX.RectangleF(xLeft, yTop, rangeWidth, yBot - yTop), boundaryBr, BoundaryWidth);
            }

            // ===========================
            // 2. DRAW VOLUME PROFILE
            // ===========================
            if (volumes.Count > 0 && maxVolume > 0)
            {
                float barOpacity = BarOpacity / 100f;
                float pocOpacity = PoCOpacity / 100f;

                float profileLeft, profileRight;
                if (VPAlignment == RedTailVPAlignment.Left)
                {
                    profileLeft  = xLeft;
                    profileRight = xLeft + profileWidthPixels;
                }
                else
                {
                    profileRight = xRight;
                    profileLeft  = xRight - profileWidthPixels;
                }

                // Adaptive rendering: smooth volumes and auto-size bars
                bool useAdaptive = RenderQuality == RedTailRenderQuality.Adaptive;
                double[] renderVolumes = useAdaptive && SmoothingPasses > 0
                    ? GetSmoothedVolumes(volumes, SmoothingPasses)
                    : volumes.ToArray();
                
                // Find max of (possibly smoothed) volumes for width scaling
                double renderMaxVol = 0;
                for (int i = 0; i < renderVolumes.Length; i++)
                    if (renderVolumes[i] > renderMaxVol) renderMaxVol = renderVolumes[i];
                if (renderMaxVol <= 0) renderMaxVol = maxVolume;
                
                // Calculate adaptive bar thickness
                float adaptiveThickness = useAdaptive 
                    ? CalculateAdaptiveBarThickness(chartScale, profileLowestPrice, profileHighestPrice, volumes.Count)
                    : 0;

                // Draw volume bars
                for (int i = 0; i < renderVolumes.Length; i++)
                {
                    double vol = renderVolumes[i];
                    if (vol <= 0) continue;

                    double priceLevel = profileLowestPrice + profilePriceInterval * i;
                    float y = chartScale.GetYByValue(priceLevel);

                    double volumeRatio = vol / renderMaxVol;
                    float barWidth = (float)(volumeRatio * profileWidthPixels);

                    float barLeft, barRight;
                    if (VPAlignment == RedTailVPAlignment.Left)
                    {
                        barLeft  = profileLeft;
                        barRight = profileLeft + barWidth;
                    }
                    else
                    {
                        barRight = profileRight;
                        barLeft  = profileRight - barWidth;
                    }

                    bool isPOC = (i == pocIndex && DisplayPoC);
                    bool isVA  = (DisplayValueArea && i >= vaDownIndex && i <= vaUpIndex);

                    // Determine source color based on volume type and polarity
                    System.Windows.Media.Brush sourceColor;
                    if (isPOC)
                    {
                        sourceColor = PoCColor;
                    }
                    else if (isVA && VolumeType == RedTailVolumeType.Standard)
                    {
                        sourceColor = ValueAreaBarColor;
                    }
                    else if (VolumeType == RedTailVolumeType.Standard)
                    {
                        sourceColor = BarColor;
                    }
                    else
                    {
                        // Polarity-based colors
                        if (VolumeType == RedTailVolumeType.Bullish)
                            sourceColor = BullishBarColor;
                        else if (VolumeType == RedTailVolumeType.Bearish)
                            sourceColor = BearishBarColor;
                        else // Both - show dominant polarity
                            sourceColor = (i < volumePolarities.Count && volumePolarities[i]) ? BullishBarColor : BearishBarColor;
                    }

                    float sourceOpacity = isPOC ? pocOpacity : barOpacity;

                    // Apply gradient or solid fill
                    SharpDX.Direct2D1.SolidColorBrush solidBarBr = null;
                    SharpDX.Direct2D1.LinearGradientBrush gradientBrush = null;
                    SharpDX.Direct2D1.Brush barBrush = null;

                    if (EnableGradientFill)
                    {
                        gradientBrush = CreateGradientBrush(rt, sourceColor, barLeft, barRight, y, sourceOpacity);
                        if (gradientBrush != null)
                            barBrush = gradientBrush;
                    }

                    if (barBrush == null)
                    {
                        SharpDX.Color4 barC4 = BrushToColor4(sourceColor, sourceOpacity);
                        solidBarBr = new SharpDX.Direct2D1.SolidColorBrush(rt, barC4);
                        barBrush = solidBarBr;
                    }

                    float effectiveThickness;
                    float gapSize;
                    if (useAdaptive)
                    {
                        effectiveThickness = adaptiveThickness;
                        gapSize = Math.Max(0.5f, adaptiveThickness * 0.1f);
                    }
                    else
                    {
                        gapSize = 1.0f;
                        effectiveThickness = Math.Max(1, BarThickness - gapSize);
                    }

                    float adjustedY = y + (gapSize / 2.0f);

                    rt.DrawLine(
                        new SharpDX.Vector2(barLeft, adjustedY),
                        new SharpDX.Vector2(barRight, adjustedY),
                        barBrush, effectiveThickness);

                    gradientBrush?.Dispose();
                    solidBarBr?.Dispose();
                }

                // Draw POC line
                if (DisplayPoC && pocIndex >= 0 && pocIndex < volumes.Count)
                {
                    double pocPrice = profileLowestPrice + profilePriceInterval * pocIndex;
                    float pocY = chartScale.GetYByValue(pocPrice);

                    SharpDX.Color4 pocC4 = BrushToColor4(PoCColor, pocOpacity);
                    using (var pocBr = new SharpDX.Direct2D1.SolidColorBrush(rt, pocC4))
                    using (var ss = CreateStrokeStyle(rt, PoCLineStyle))
                    {
                        rt.DrawLine(
                            new SharpDX.Vector2(xLeft, pocY),
                            new SharpDX.Vector2(xRight, pocY),
                            pocBr, PoCLineWidth, ss);
                    }
                }

                // Draw VA lines
                if (DisplayValueArea && DisplayVALines)
                {
                    float vaOpacity = VALineOpacity / 100f;
                    SharpDX.Color4 vaC4 = BrushToColor4(VALineColor, vaOpacity);

                    using (var vaBr = new SharpDX.Direct2D1.SolidColorBrush(rt, vaC4))
                    using (var ss = CreateStrokeStyle(rt, VALineStyle))
                    {
                        // VAH
                        if (vaUpIndex >= 0 && vaUpIndex < volumes.Count)
                        {
                            double vahPrice = profileLowestPrice + profilePriceInterval * vaUpIndex;
                            float vahY = chartScale.GetYByValue(vahPrice);
                            rt.DrawLine(new SharpDX.Vector2(xLeft, vahY), new SharpDX.Vector2(xRight, vahY), vaBr, VALineWidth, ss);
                        }
                        // VAL
                        if (vaDownIndex >= 0 && vaDownIndex < volumes.Count)
                        {
                            double valPrice = profileLowestPrice + profilePriceInterval * vaDownIndex;
                            float valY = chartScale.GetYByValue(valPrice);
                            rt.DrawLine(new SharpDX.Vector2(xLeft, valY), new SharpDX.Vector2(xRight, valY), vaBr, VALineWidth, ss);
                        }
                    }
                }

                // Draw VP labels (POC, VAH, VAL)
                if (ShowLabels)
                {
                    using (var bgBr = new SharpDX.Direct2D1.SolidColorBrush(rt, new SharpDX.Color4(0.05f, 0.05f, 0.1f, 0.8f)))
                    using (var fmt = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Consolas",
                        SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, (float)LabelFontSize))
                    {
                        // POC label
                        if (DisplayPoC && pocIndex >= 0 && pocIndex < volumes.Count)
                        {
                            double pocPrice = profileLowestPrice + profilePriceInterval * pocIndex;
                            float pocY = chartScale.GetYByValue(pocPrice);
                            string pocLabel = ShowPriceOnLabel ? "POC " + pocPrice.ToString("F2") : "POC";
                            DrawLabel(rt, fmt, bgBr, BrushToColor4(PoCColor, PoCOpacity / 100f), pocLabel, xLeft, pocY);
                        }

                        // VAH label
                        if (DisplayValueArea && DisplayVALines && vaUpIndex >= 0 && vaUpIndex < volumes.Count)
                        {
                            double vahPrice = profileLowestPrice + profilePriceInterval * vaUpIndex;
                            float vahY = chartScale.GetYByValue(vahPrice);
                            string vahLabel = ShowPriceOnLabel ? "VAH " + vahPrice.ToString("F2") : "VAH";
                            DrawLabel(rt, fmt, bgBr, BrushToColor4(VALineColor, VALineOpacity / 100f), vahLabel, xLeft, vahY);
                        }

                        // VAL label
                        if (DisplayValueArea && DisplayVALines && vaDownIndex >= 0 && vaDownIndex < volumes.Count)
                        {
                            double valPrice = profileLowestPrice + profilePriceInterval * vaDownIndex;
                            float valY = chartScale.GetYByValue(valPrice);
                            string valLabel = ShowPriceOnLabel ? "VAL " + valPrice.ToString("F2") : "VAL";
                            DrawLabel(rt, fmt, bgBr, BrushToColor4(VALineColor, VALineOpacity / 100f), valLabel, xLeft, valY);
                        }
                    }
                }
            }

            // ===========================
            // 3. DRAW FIBONACCI LEVELS
            // ===========================
            if (DisplayFibs)
            {
                double fibStartPrice = startAnchor.Price;
                double fibEndPrice   = endAnchor.Price;
                double fibRange      = fibStartPrice - fibEndPrice;

                if (Math.Abs(fibRange) > double.Epsilon)
                {
                    float fibOpacity = FibOpacity / 100f;

                    float fibXStart = xLeft;
                    float fibXEnd   = ExtendFibsRight ? (float)cp.W : xRight;

                    using (var bgBr = new SharpDX.Direct2D1.SolidColorBrush(rt, new SharpDX.Color4(0.05f, 0.05f, 0.1f, 0.8f)))
                    using (var fmt = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Consolas",
                        SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, (float)FibLabelFontSize))
                    {
                        foreach (var lv in GetActiveFibLevels())
                        {
                            double price = fibEndPrice + fibRange * lv.Ratio;
                            float y = chartScale.GetYByValue(price);
                            SharpDX.Color4 lvColor = BrushToColor4(lv.Color, fibOpacity);

                            // Fib level line
                            using (var lineBr = new SharpDX.Direct2D1.SolidColorBrush(rt, lvColor))
                            using (var ss = CreateStrokeStyle(rt, FibLineDashStyle))
                            {
                                rt.DrawLine(
                                    new SharpDX.Vector2(fibXStart, y),
                                    new SharpDX.Vector2(fibXEnd, y),
                                    lineBr, FibLineWidth, ss);
                            }

                            // Fib label (on the right edge)
                            try
                            {
                                string pctText = (lv.Ratio * 100).ToString("F1");
                                string labelText = ShowFibPrice
                                    ? pctText + "% [" + price.ToString("F2") + "]"
                                    : pctText + "%";

                                using (var tl = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, labelText, fmt, 400, 20))
                                {
                                    float tw = tl.Metrics.Width, th = tl.Metrics.Height;
                                    float lx = fibXEnd - tw - 8, ly = y - th - 2;
                                    rt.FillRoundedRectangle(new SharpDX.Direct2D1.RoundedRectangle
                                        { Rect = new SharpDX.RectangleF(lx - 2, ly, tw + 8, th + 4), RadiusX = 2, RadiusY = 2 }, bgBr);
                                    using (var txtBr = new SharpDX.Direct2D1.SolidColorBrush(rt, lvColor))
                                        rt.DrawTextLayout(new SharpDX.Vector2(lx + 2, ly + 1), tl, txtBr);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }

            // ===========================
            // 4. DRAW AVWAP LINE
            // ===========================
            if (DisplayAVWAP && avwapPoints.Count >= 2)
            {
                float avwapOpacity = AVWAPOpacity / 100f;
                SharpDX.Color4 avwapC4 = BrushToColor4(AVWAPColor, avwapOpacity);

                // Enable antialiasing for smooth line
                var oldAA = rt.AntialiasMode;
                rt.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.PerPrimitive;

                using (var avwapBr = new SharpDX.Direct2D1.SolidColorBrush(rt, avwapC4))
                using (var ss = CreateStrokeStyle(rt, AVWAPLineStyle))
                {
                    // Build points
                    var points = new List<SharpDX.Vector2>();
                    double lastVwapValue = 0;

                    foreach (var kvp in avwapPoints)
                    {
                        int barIdx = kvp.Key;
                        double vwapVal = kvp.Value;
                        lastVwapValue = vwapVal;

                        try
                        {
                            float bx = chartControl.GetXByBarIndex(chartControl.BarsArray[0], barIdx);
                            float by = chartScale.GetYByValue(vwapVal);

                            // Only add if within visible area or slightly outside (for line continuity)
                            if (!ExtendAVWAPRight && bx > xRight) break;

                            points.Add(new SharpDX.Vector2(bx, by));
                        }
                        catch { }
                    }

                    // Draw as PathGeometry for smooth rendering
                    if (points.Count >= 2)
                    {
                        using (var path = new SharpDX.Direct2D1.PathGeometry(rt.Factory))
                        {
                            using (var sink = path.Open())
                            {
                                sink.BeginFigure(points[0], SharpDX.Direct2D1.FigureBegin.Hollow);
                                for (int p = 1; p < points.Count; p++)
                                    sink.AddLine(points[p]);
                                sink.EndFigure(SharpDX.Direct2D1.FigureEnd.Open);
                                sink.Close();
                            }
                            rt.DrawGeometry(path, avwapBr, AVWAPLineWidth, ss);
                        }

                        // Draw AVWAP label at the last visible point
                        if (ShowAVWAPLabel && ShowLabels)
                        {
                            try
                            {
                                var lastPt = points[points.Count - 1];
                                string avwapLabel = ShowPriceOnLabel ? "AVWAP " + lastVwapValue.ToString("F2") : "AVWAP";

                                using (var bgBr = new SharpDX.Direct2D1.SolidColorBrush(rt, new SharpDX.Color4(0.05f, 0.05f, 0.1f, 0.8f)))
                                using (var fmt = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Consolas",
                                    SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, (float)LabelFontSize))
                                {
                                    DrawLabel(rt, fmt, bgBr, avwapC4, avwapLabel, lastPt.X - 60, lastPt.Y);
                                }
                            }
                            catch { }
                        }
                    }
                }

                rt.AntialiasMode = oldAA;
            }

            // ===========================
            // 5. DRAW CLUSTER LEVELS
            // ===========================
            if (DisplayClusterLevels && clusterLevels.Count > 0)
            {
                float clOpacity = ClusterOpacity / 100f;
                float clXStart = xLeft;
                float clXEnd   = ExtendClustersRight ? (float)cp.W : xRight;

                System.Windows.Media.Brush[] clColors = new System.Windows.Media.Brush[]
                {
                    Cluster1LevelColor, Cluster2LevelColor, Cluster3LevelColor, Cluster4LevelColor, Cluster5LevelColor,
                    Cluster6LevelColor, Cluster7LevelColor, Cluster8LevelColor, Cluster9LevelColor, Cluster10LevelColor
                };

                using (var bgBr = new SharpDX.Direct2D1.SolidColorBrush(rt, new SharpDX.Color4(0.05f, 0.05f, 0.1f, 0.8f)))
                using (var fmt = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Consolas",
                    SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, (float)LabelFontSize))
                {
                    for (int ci = 0; ci < clusterLevels.Count; ci++)
                    {
                        var cl = clusterLevels[ci];
                        System.Windows.Media.Brush clBrush = clColors[ci % clColors.Length];
                        SharpDX.Color4 clC4 = BrushToColor4(clBrush, clOpacity);
                        float y = chartScale.GetYByValue(cl.POCPrice);

                        // Cluster POC line
                        using (var lineBr = new SharpDX.Direct2D1.SolidColorBrush(rt, clC4))
                        using (var ss = CreateStrokeStyle(rt, ClusterLineStyle))
                        {
                            rt.DrawLine(
                                new SharpDX.Vector2(clXStart, y),
                                new SharpDX.Vector2(clXEnd, y),
                                lineBr, ClusterLineWidth, ss);
                        }

                        // Cluster label
                        if (ShowClusterLabels)
                        {
                            try
                            {
                                string labelText = ShowPriceOnLabel
                                    ? "C" + (ci + 1) + " POC " + cl.POCPrice.ToString("F2")
                                    : "C" + (ci + 1) + " POC";

                                DrawLabel(rt, fmt, bgBr, clC4, labelText, clXEnd - 120, y);
                            }
                            catch { }
                        }
                    }
                }
            }

            // ===========================
            // 6. DRAW SELECTION HANDLES
            // ===========================
            if (IsSelected)
            {
                using (var hb = new SharpDX.Direct2D1.SolidColorBrush(rt, SharpDX.Color.White))
                {
                    rt.FillEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2((float)startPt.X, (float)startPt.Y), 5, 5), hb);
                    rt.FillEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2((float)endPt.X, (float)endPt.Y), 5, 5), hb);
                }
            }
        }
        #endregion

        #region Helpers
        
        #region Gradient Rendering Helper
        private SharpDX.Direct2D1.LinearGradientBrush CreateGradientBrush(SharpDX.Direct2D1.RenderTarget rt,
            System.Windows.Media.Brush baseColor, float startX, float endX, float y, float baseOpacity)
        {
            if (!EnableGradientFill || GradientIntensity <= 0)
                return null;
            
            try
            {
                System.Windows.Media.Color mediaColor;
                if (baseColor is System.Windows.Media.SolidColorBrush solidBrush)
                    mediaColor = solidBrush.Color;
                else
                    mediaColor = System.Windows.Media.Colors.Gray;
                
                float intensityFactor = GradientIntensity / 100.0f;
                float startOpacity = baseOpacity * (1.0f - intensityFactor);
                float endOpacity = baseOpacity;
                
                var gradientStops = new SharpDX.Direct2D1.GradientStop[2];
                gradientStops[0] = new SharpDX.Direct2D1.GradientStop
                {
                    Position = 0.0f,
                    Color = new SharpDX.Color4(mediaColor.R / 255f, mediaColor.G / 255f, mediaColor.B / 255f, startOpacity)
                };
                gradientStops[1] = new SharpDX.Direct2D1.GradientStop
                {
                    Position = 1.0f,
                    Color = new SharpDX.Color4(mediaColor.R / 255f, mediaColor.G / 255f, mediaColor.B / 255f, endOpacity)
                };
                
                var gradientStopCollection = new SharpDX.Direct2D1.GradientStopCollection(rt, gradientStops);
                var gradientBrush = new SharpDX.Direct2D1.LinearGradientBrush(
                    rt,
                    new SharpDX.Direct2D1.LinearGradientBrushProperties
                    {
                        StartPoint = new SharpDX.Vector2(startX, y),
                        EndPoint = new SharpDX.Vector2(endX, y)
                    },
                    gradientStopCollection
                );
                gradientStopCollection.Dispose();
                return gradientBrush;
            }
            catch { return null; }
        }
        #endregion
        
        #region Adaptive Rendering Helpers
        private double[] GetSmoothedVolumes(List<double> rawVolumes, int passes)
        {
            if (rawVolumes == null || rawVolumes.Count == 0)
                return new double[0];
            
            double[] current = rawVolumes.ToArray();
            double[] buffer = new double[current.Length];
            
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < current.Length; i++)
                {
                    double sum = current[i] * 4.0;
                    double weightSum = 4.0;
                    
                    if (i - 1 >= 0) { sum += current[i - 1] * 2.0; weightSum += 2.0; }
                    if (i + 1 < current.Length) { sum += current[i + 1] * 2.0; weightSum += 2.0; }
                    if (i - 2 >= 0) { sum += current[i - 2] * 1.0; weightSum += 1.0; }
                    if (i + 2 < current.Length) { sum += current[i + 2] * 1.0; weightSum += 1.0; }
                    
                    buffer[i] = sum / weightSum;
                }
                
                double[] temp = current;
                current = buffer;
                buffer = temp;
            }
            
            return current;
        }
        
        private float CalculateAdaptiveBarThickness(ChartScale chartScale, double lowPrice, double highPrice, int rowCount)
        {
            float lowY = chartScale.GetYByValue(lowPrice);
            float highY = chartScale.GetYByValue(highPrice);
            float totalPixelHeight = Math.Abs(lowY - highY);
            
            float pixelsPerRow = totalPixelHeight / Math.Max(1, rowCount);
            float idealThickness = pixelsPerRow * 0.85f;
            
            return Math.Max(MinBarPixelHeight, Math.Min(idealThickness, MaxBarPixelHeight));
        }
        #endregion
        private void DrawLabel(SharpDX.Direct2D1.RenderTarget rt, SharpDX.DirectWrite.TextFormat fmt,
            SharpDX.Direct2D1.SolidColorBrush bgBr, SharpDX.Color4 textColor, string text, float x, float y)
        {
            try
            {
                using (var tl = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, text, fmt, 400, 20))
                {
                    float tw = tl.Metrics.Width, th = tl.Metrics.Height;
                    float lx = x + 4, ly = y - th - 2;
                    rt.FillRoundedRectangle(new SharpDX.Direct2D1.RoundedRectangle
                        { Rect = new SharpDX.RectangleF(lx - 2, ly, tw + 8, th + 4), RadiusX = 2, RadiusY = 2 }, bgBr);
                    using (var txtBr = new SharpDX.Direct2D1.SolidColorBrush(rt, textColor))
                        rt.DrawTextLayout(new SharpDX.Vector2(lx + 2, ly + 1), tl, txtBr);
                }
            }
            catch { }
        }

        private SharpDX.Color4 BrushToColor4(System.Windows.Media.Brush mb, float opacity)
        {
            if (mb is System.Windows.Media.SolidColorBrush scb)
            {
                System.Windows.Media.Color c = scb.Color;
                return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, opacity);
            }
            return new SharpDX.Color4(1f, 1f, 1f, opacity);
        }

        private SharpDX.Direct2D1.StrokeStyle CreateStrokeStyle(SharpDX.Direct2D1.RenderTarget rt, DashStyleHelper dashStyle)
        {
            float[] dashes;
            switch (dashStyle)
            {
                case DashStyleHelper.Dash:
                    dashes = new float[] { 4f, 3f };
                    break;
                case DashStyleHelper.Dot:
                    dashes = new float[] { 0.5f, 2f };
                    break;
                case DashStyleHelper.DashDot:
                    dashes = new float[] { 4f, 2f, 0.5f, 2f };
                    break;
                case DashStyleHelper.DashDotDot:
                    dashes = new float[] { 4f, 2f, 0.5f, 2f, 0.5f, 2f };
                    break;
                default:
                    return new SharpDX.Direct2D1.StrokeStyle(rt.Factory,
                        new SharpDX.Direct2D1.StrokeStyleProperties { DashStyle = SharpDX.Direct2D1.DashStyle.Solid });
            }

            return new SharpDX.Direct2D1.StrokeStyle(rt.Factory,
                new SharpDX.Direct2D1.StrokeStyleProperties
                {
                    DashStyle = SharpDX.Direct2D1.DashStyle.Custom,
                    DashCap   = SharpDX.Direct2D1.CapStyle.Round,
                    StartCap  = SharpDX.Direct2D1.CapStyle.Round,
                    EndCap    = SharpDX.Direct2D1.CapStyle.Round
                },
                dashes);
        }
        #endregion

        #region Properties — Volume Profile

        [Display(Name = "Number of Rows", Description = "Volume profile resolution", GroupName = "1. Volume Profile", Order = 1)]
        [Range(50, 500)]
        public int NumberOfRows { get; set; }

        [Display(Name = "Profile Width %", Description = "Width of profile as % of range width", GroupName = "1. Volume Profile", Order = 2)]
        [Range(5, 100)]
        public int ProfileWidth { get; set; }

        [Display(Name = "Alignment", GroupName = "1. Volume Profile", Order = 3)]
        public RedTailVPAlignment VPAlignment { get; set; }

        [Display(Name = "Volume Type", Description = "Standard, Bullish, Bearish, or Both (polarity coloring)", GroupName = "1. Volume Profile", Order = 4)]
        public RedTailVolumeType VolumeType { get; set; }

        [XmlIgnore][Display(Name = "Bar Color", GroupName = "1. Volume Profile", Order = 5)]
        public System.Windows.Media.Brush BarColor { get; set; }
        [Browsable(false)] public string BarColorSerialize { get { return Serialize.BrushToString(BarColor); } set { BarColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name = "Bullish Bar Color", Description = "Color for bullish volume (used in Bullish/Both modes)", GroupName = "1. Volume Profile", Order = 6)]
        public System.Windows.Media.Brush BullishBarColor { get; set; }
        [Browsable(false)] public string BullishBarColorSerialize { get { return Serialize.BrushToString(BullishBarColor); } set { BullishBarColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name = "Bearish Bar Color", Description = "Color for bearish volume (used in Bearish/Both modes)", GroupName = "1. Volume Profile", Order = 7)]
        public System.Windows.Media.Brush BearishBarColor { get; set; }
        [Browsable(false)] public string BearishBarColorSerialize { get { return Serialize.BrushToString(BearishBarColor); } set { BearishBarColor = Serialize.StringToBrush(value); } }

        [Display(Name = "Bar Opacity %", GroupName = "1. Volume Profile", Order = 8)]
        [Range(5, 100)]
        public int BarOpacity { get; set; }

        [Display(Name = "Bar Thickness", Description = "Used in Manual render quality mode", GroupName = "1. Volume Profile", Order = 9)]
        [Range(1, 10)]
        public int BarThickness { get; set; }

        #endregion

        #region Properties — Gradient Fill

        [Display(Name = "Enable Gradient Fill", Description = "Apply gradient effect to volume bars (fade from transparent to solid)", GroupName = "1b. Gradient Fill", Order = 1)]
        public bool EnableGradientFill { get; set; }

        [Display(Name = "Gradient Intensity", Description = "0=no fade (solid), 100=maximum fade effect", GroupName = "1b. Gradient Fill", Order = 2)]
        [Range(0, 100)]
        public int GradientIntensity { get; set; }

        #endregion

        #region Properties — Adaptive Rendering

        [Display(Name = "Render Quality", Description = "Manual = fixed bar thickness. Adaptive = auto-sizes bars and smooths profile shape.", GroupName = "1c. Adaptive Rendering", Order = 1)]
        public RedTailRenderQuality RenderQuality { get; set; }

        [Display(Name = "Smoothing Passes", Description = "Gaussian smoothing passes (0=raw, 2-3=recommended, 5=very smooth)", GroupName = "1c. Adaptive Rendering", Order = 2)]
        [Range(0, 5)]
        public int SmoothingPasses { get; set; }

        [Display(Name = "Min Bar Pixel Height", Description = "Minimum bar height in pixels (prevents bars from disappearing)", GroupName = "1c. Adaptive Rendering", Order = 3)]
        [Range(1.0f, 10.0f)]
        public float MinBarPixelHeight { get; set; }

        [Display(Name = "Max Bar Pixel Height", Description = "Maximum bar height in pixels (prevents bars from getting too thick)", GroupName = "1c. Adaptive Rendering", Order = 4)]
        [Range(2.0f, 20.0f)]
        public float MaxBarPixelHeight { get; set; }

        #endregion

        #region Properties — POC

        [Display(Name = "Display POC", GroupName = "2. Point of Control", Order = 1)]
        public bool DisplayPoC { get; set; }

        [XmlIgnore][Display(Name = "POC Color", GroupName = "2. Point of Control", Order = 2)]
        public System.Windows.Media.Brush PoCColor { get; set; }
        [Browsable(false)] public string PoCColorSerialize { get { return Serialize.BrushToString(PoCColor); } set { PoCColor = Serialize.StringToBrush(value); } }

        [Display(Name = "POC Line Width", GroupName = "2. Point of Control", Order = 3)]
        [Range(1, 5)]
        public int PoCLineWidth { get; set; }

        [Display(Name = "POC Line Style", GroupName = "2. Point of Control", Order = 4)]
        public DashStyleHelper PoCLineStyle { get; set; }

        [Display(Name = "POC Opacity %", GroupName = "2. Point of Control", Order = 5)]
        [Range(10, 100)]
        public int PoCOpacity { get; set; }

        #endregion

        #region Properties — Value Area

        [Display(Name = "Display Value Area", GroupName = "3. Value Area", Order = 1)]
        public bool DisplayValueArea { get; set; }

        [Display(Name = "Value Area %", GroupName = "3. Value Area", Order = 2)]
        [Range(10, 95)]
        public int ValueAreaPct { get; set; }

        [XmlIgnore][Display(Name = "VA Bar Color", GroupName = "3. Value Area", Order = 3)]
        public System.Windows.Media.Brush ValueAreaBarColor { get; set; }
        [Browsable(false)] public string ValueAreaBarColorSerialize { get { return Serialize.BrushToString(ValueAreaBarColor); } set { ValueAreaBarColor = Serialize.StringToBrush(value); } }

        [Display(Name = "Display VA Lines", GroupName = "3. Value Area", Order = 4)]
        public bool DisplayVALines { get; set; }

        [XmlIgnore][Display(Name = "VA Line Color", GroupName = "3. Value Area", Order = 5)]
        public System.Windows.Media.Brush VALineColor { get; set; }
        [Browsable(false)] public string VALineColorSerialize { get { return Serialize.BrushToString(VALineColor); } set { VALineColor = Serialize.StringToBrush(value); } }

        [Display(Name = "VA Line Width", GroupName = "3. Value Area", Order = 6)]
        [Range(1, 5)]
        public int VALineWidth { get; set; }

        [Display(Name = "VA Line Style", GroupName = "3. Value Area", Order = 7)]
        public DashStyleHelper VALineStyle { get; set; }

        [Display(Name = "VA Line Opacity %", GroupName = "3. Value Area", Order = 8)]
        [Range(10, 100)]
        public int VALineOpacity { get; set; }

        #endregion

        #region Properties — Labels

        [Display(Name = "Show Labels", GroupName = "4. Labels", Order = 1)]
        public bool ShowLabels { get; set; }

        [Display(Name = "Label Font Size", GroupName = "4. Labels", Order = 2)]
        [Range(8, 20)]
        public int LabelFontSize { get; set; }

        [Display(Name = "Show Price on Label", GroupName = "4. Labels", Order = 3)]
        public bool ShowPriceOnLabel { get; set; }

        #endregion

        #region Properties — Boundary

        [XmlIgnore][Display(Name = "Boundary Color", GroupName = "5. Boundary", Order = 1)]
        public System.Windows.Media.Brush BoundaryColor { get; set; }
        [Browsable(false)] public string BoundaryColorSerialize { get { return Serialize.BrushToString(BoundaryColor); } set { BoundaryColor = Serialize.StringToBrush(value); } }

        [Display(Name = "Boundary Opacity %", GroupName = "5. Boundary", Order = 2)]
        [Range(0, 100)]
        public int BoundaryOpacity { get; set; }

        [Display(Name = "Boundary Width", GroupName = "5. Boundary", Order = 3)]
        [Range(0, 5)]
        public int BoundaryWidth { get; set; }

        #endregion

        #region Properties — Fibonacci

        [Display(Name = "Display Fibs", GroupName = "6. Fibonacci", Order = 1)]
        public bool DisplayFibs { get; set; }

        [Display(Name = "Fib Line Width", GroupName = "6. Fibonacci", Order = 2)]
        [Range(1, 5)]
        public int FibLineWidth { get; set; }

        [Display(Name = "Fib Line Style", GroupName = "6. Fibonacci", Order = 3)]
        public DashStyleHelper FibLineDashStyle { get; set; }

        [Display(Name = "Fib Opacity %", GroupName = "6. Fibonacci", Order = 4)]
        [Range(10, 100)]
        public int FibOpacity { get; set; }

        [Display(Name = "Extend Fibs Right", GroupName = "6. Fibonacci", Order = 5)]
        public bool ExtendFibsRight { get; set; }

        [Display(Name = "Fib Label Font Size", GroupName = "6. Fibonacci", Order = 6)]
        [Range(8, 20)]
        public int FibLabelFontSize { get; set; }

        [Display(Name = "Show Fib Price", GroupName = "6. Fibonacci", Order = 7)]
        public bool ShowFibPrice { get; set; }

        // Fib Levels 1-10
        [Display(Name = "Level 1 (%)", Description = "-1 to disable", GroupName = "7. Fib Levels", Order = 1)]
        public double FibLevel1 { get; set; }
        [XmlIgnore][Display(Name = "Level 1 Color", GroupName = "7. Fib Levels", Order = 2)]
        public System.Windows.Media.Brush FibLevel1Color { get; set; }
        [Browsable(false)] public string FibLevel1ColorSerialize { get { return Serialize.BrushToString(FibLevel1Color); } set { FibLevel1Color = Serialize.StringToBrush(value); } }

        [Display(Name = "Level 2 (%)", Description = "-1 to disable", GroupName = "7. Fib Levels", Order = 3)]
        public double FibLevel2 { get; set; }
        [XmlIgnore][Display(Name = "Level 2 Color", GroupName = "7. Fib Levels", Order = 4)]
        public System.Windows.Media.Brush FibLevel2Color { get; set; }
        [Browsable(false)] public string FibLevel2ColorSerialize { get { return Serialize.BrushToString(FibLevel2Color); } set { FibLevel2Color = Serialize.StringToBrush(value); } }

        [Display(Name = "Level 3 (%)", Description = "-1 to disable", GroupName = "7. Fib Levels", Order = 5)]
        public double FibLevel3 { get; set; }
        [XmlIgnore][Display(Name = "Level 3 Color", GroupName = "7. Fib Levels", Order = 6)]
        public System.Windows.Media.Brush FibLevel3Color { get; set; }
        [Browsable(false)] public string FibLevel3ColorSerialize { get { return Serialize.BrushToString(FibLevel3Color); } set { FibLevel3Color = Serialize.StringToBrush(value); } }

        [Display(Name = "Level 4 (%)", Description = "-1 to disable", GroupName = "7. Fib Levels", Order = 7)]
        public double FibLevel4 { get; set; }
        [XmlIgnore][Display(Name = "Level 4 Color", GroupName = "7. Fib Levels", Order = 8)]
        public System.Windows.Media.Brush FibLevel4Color { get; set; }
        [Browsable(false)] public string FibLevel4ColorSerialize { get { return Serialize.BrushToString(FibLevel4Color); } set { FibLevel4Color = Serialize.StringToBrush(value); } }

        [Display(Name = "Level 5 (%)", Description = "-1 to disable", GroupName = "7. Fib Levels", Order = 9)]
        public double FibLevel5 { get; set; }
        [XmlIgnore][Display(Name = "Level 5 Color", GroupName = "7. Fib Levels", Order = 10)]
        public System.Windows.Media.Brush FibLevel5Color { get; set; }
        [Browsable(false)] public string FibLevel5ColorSerialize { get { return Serialize.BrushToString(FibLevel5Color); } set { FibLevel5Color = Serialize.StringToBrush(value); } }

        [Display(Name = "Level 6 (%)", Description = "-1 to disable", GroupName = "7. Fib Levels", Order = 11)]
        public double FibLevel6 { get; set; }
        [XmlIgnore][Display(Name = "Level 6 Color", GroupName = "7. Fib Levels", Order = 12)]
        public System.Windows.Media.Brush FibLevel6Color { get; set; }
        [Browsable(false)] public string FibLevel6ColorSerialize { get { return Serialize.BrushToString(FibLevel6Color); } set { FibLevel6Color = Serialize.StringToBrush(value); } }

        [Display(Name = "Level 7 (%)", Description = "-1 to disable", GroupName = "7. Fib Levels", Order = 13)]
        public double FibLevel7 { get; set; }
        [XmlIgnore][Display(Name = "Level 7 Color", GroupName = "7. Fib Levels", Order = 14)]
        public System.Windows.Media.Brush FibLevel7Color { get; set; }
        [Browsable(false)] public string FibLevel7ColorSerialize { get { return Serialize.BrushToString(FibLevel7Color); } set { FibLevel7Color = Serialize.StringToBrush(value); } }

        [Display(Name = "Level 8 (%)", Description = "-1 to disable", GroupName = "7. Fib Levels", Order = 15)]
        public double FibLevel8 { get; set; }
        [XmlIgnore][Display(Name = "Level 8 Color", GroupName = "7. Fib Levels", Order = 16)]
        public System.Windows.Media.Brush FibLevel8Color { get; set; }
        [Browsable(false)] public string FibLevel8ColorSerialize { get { return Serialize.BrushToString(FibLevel8Color); } set { FibLevel8Color = Serialize.StringToBrush(value); } }

        [Display(Name = "Level 9 (%)", Description = "-1 to disable", GroupName = "7. Fib Levels", Order = 17)]
        public double FibLevel9 { get; set; }
        [XmlIgnore][Display(Name = "Level 9 Color", GroupName = "7. Fib Levels", Order = 18)]
        public System.Windows.Media.Brush FibLevel9Color { get; set; }
        [Browsable(false)] public string FibLevel9ColorSerialize { get { return Serialize.BrushToString(FibLevel9Color); } set { FibLevel9Color = Serialize.StringToBrush(value); } }

        [Display(Name = "Level 10 (%)", Description = "-1 to disable", GroupName = "7. Fib Levels", Order = 19)]
        public double FibLevel10 { get; set; }
        [XmlIgnore][Display(Name = "Level 10 Color", GroupName = "7. Fib Levels", Order = 20)]
        public System.Windows.Media.Brush FibLevel10Color { get; set; }
        [Browsable(false)] public string FibLevel10ColorSerialize { get { return Serialize.BrushToString(FibLevel10Color); } set { FibLevel10Color = Serialize.StringToBrush(value); } }

        #endregion

        #region Properties — AVWAP

        [Display(Name = "Display AVWAP", GroupName = "8. Anchored VWAP", Order = 1)]
        public bool DisplayAVWAP { get; set; }

        [XmlIgnore][Display(Name = "AVWAP Color", GroupName = "8. Anchored VWAP", Order = 2)]
        public System.Windows.Media.Brush AVWAPColor { get; set; }
        [Browsable(false)] public string AVWAPColorSerialize { get { return Serialize.BrushToString(AVWAPColor); } set { AVWAPColor = Serialize.StringToBrush(value); } }

        [Display(Name = "AVWAP Line Width", GroupName = "8. Anchored VWAP", Order = 3)]
        [Range(1, 5)]
        public int AVWAPLineWidth { get; set; }

        [Display(Name = "AVWAP Line Style", GroupName = "8. Anchored VWAP", Order = 4)]
        public DashStyleHelper AVWAPLineStyle { get; set; }

        [Display(Name = "AVWAP Opacity %", GroupName = "8. Anchored VWAP", Order = 5)]
        [Range(10, 100)]
        public int AVWAPOpacity { get; set; }

        [Display(Name = "Extend AVWAP Right", Description = "Extend AVWAP line beyond the drawing range", GroupName = "8. Anchored VWAP", Order = 6)]
        public bool ExtendAVWAPRight { get; set; }

        [Display(Name = "Show AVWAP Label", GroupName = "8. Anchored VWAP", Order = 7)]
        public bool ShowAVWAPLabel { get; set; }

        #endregion

        #region Properties — Cluster Levels

        [Display(Name = "Display Cluster Levels", GroupName = "9. Cluster Levels", Order = 1)]
        public bool DisplayClusterLevels { get; set; }

        [Display(Name = "Number of Clusters", Description = "K-Means cluster count (2-10)", GroupName = "9. Cluster Levels", Order = 2)]
        [Range(2, 10)]
        public int ClusterCount { get; set; }

        [Display(Name = "K-Means Iterations", GroupName = "9. Cluster Levels", Order = 3)]
        [Range(5, 50)]
        public int ClusterIterations { get; set; }

        [Display(Name = "Rows per Cluster", Description = "VP resolution per cluster for POC detection", GroupName = "9. Cluster Levels", Order = 4)]
        [Range(5, 100)]
        public int ClusterRowsPerLevel { get; set; }

        [Display(Name = "Line Width", GroupName = "9. Cluster Levels", Order = 5)]
        [Range(1, 5)]
        public int ClusterLineWidth { get; set; }

        [Display(Name = "Line Style", GroupName = "9. Cluster Levels", Order = 6)]
        public DashStyleHelper ClusterLineStyle { get; set; }

        [Display(Name = "Opacity %", GroupName = "9. Cluster Levels", Order = 7)]
        [Range(10, 100)]
        public int ClusterOpacity { get; set; }

        [Display(Name = "Extend Right", GroupName = "9. Cluster Levels", Order = 8)]
        public bool ExtendClustersRight { get; set; }

        [Display(Name = "Show Labels", GroupName = "9. Cluster Levels", Order = 9)]
        public bool ShowClusterLabels { get; set; }

        [XmlIgnore][Display(Name = "Cluster 1 Color", GroupName = "10. Cluster Colors", Order = 1)]
        public System.Windows.Media.Brush Cluster1LevelColor { get; set; }
        [Browsable(false)] public string Cluster1LevelColorSerialize { get { return Serialize.BrushToString(Cluster1LevelColor); } set { Cluster1LevelColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name = "Cluster 2 Color", GroupName = "10. Cluster Colors", Order = 2)]
        public System.Windows.Media.Brush Cluster2LevelColor { get; set; }
        [Browsable(false)] public string Cluster2LevelColorSerialize { get { return Serialize.BrushToString(Cluster2LevelColor); } set { Cluster2LevelColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name = "Cluster 3 Color", GroupName = "10. Cluster Colors", Order = 3)]
        public System.Windows.Media.Brush Cluster3LevelColor { get; set; }
        [Browsable(false)] public string Cluster3LevelColorSerialize { get { return Serialize.BrushToString(Cluster3LevelColor); } set { Cluster3LevelColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name = "Cluster 4 Color", GroupName = "10. Cluster Colors", Order = 4)]
        public System.Windows.Media.Brush Cluster4LevelColor { get; set; }
        [Browsable(false)] public string Cluster4LevelColorSerialize { get { return Serialize.BrushToString(Cluster4LevelColor); } set { Cluster4LevelColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name = "Cluster 5 Color", GroupName = "10. Cluster Colors", Order = 5)]
        public System.Windows.Media.Brush Cluster5LevelColor { get; set; }
        [Browsable(false)] public string Cluster5LevelColorSerialize { get { return Serialize.BrushToString(Cluster5LevelColor); } set { Cluster5LevelColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name = "Cluster 6 Color", GroupName = "10. Cluster Colors", Order = 6)]
        public System.Windows.Media.Brush Cluster6LevelColor { get; set; }
        [Browsable(false)] public string Cluster6LevelColorSerialize { get { return Serialize.BrushToString(Cluster6LevelColor); } set { Cluster6LevelColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name = "Cluster 7 Color", GroupName = "10. Cluster Colors", Order = 7)]
        public System.Windows.Media.Brush Cluster7LevelColor { get; set; }
        [Browsable(false)] public string Cluster7LevelColorSerialize { get { return Serialize.BrushToString(Cluster7LevelColor); } set { Cluster7LevelColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name = "Cluster 8 Color", GroupName = "10. Cluster Colors", Order = 8)]
        public System.Windows.Media.Brush Cluster8LevelColor { get; set; }
        [Browsable(false)] public string Cluster8LevelColorSerialize { get { return Serialize.BrushToString(Cluster8LevelColor); } set { Cluster8LevelColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name = "Cluster 9 Color", GroupName = "10. Cluster Colors", Order = 9)]
        public System.Windows.Media.Brush Cluster9LevelColor { get; set; }
        [Browsable(false)] public string Cluster9LevelColorSerialize { get { return Serialize.BrushToString(Cluster9LevelColor); } set { Cluster9LevelColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name = "Cluster 10 Color", GroupName = "10. Cluster Colors", Order = 10)]
        public System.Windows.Media.Brush Cluster10LevelColor { get; set; }
        [Browsable(false)] public string Cluster10LevelColorSerialize { get { return Serialize.BrushToString(Cluster10LevelColor); } set { Cluster10LevelColor = Serialize.StringToBrush(value); } }

        #endregion
    }
}