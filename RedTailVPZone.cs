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
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
#endregion

// RedTail VP Zone - Fixed Range Volume Profile Zone Drawing Tool
// Created by RedTail Indicators - @_hawkeye_13
// Click start bar, click end bar -> draws VAH/VAL zone with POC, extends right indefinitely
// v2.0 - Added togglable FRVP-style volume profile rendering within selection range

namespace NinjaTrader.NinjaScript.DrawingTools
{
    // Enums at namespace level for proper XML template serialization
    public enum VPZoneAlignment
    {
        Left,
        Right
    }

    public enum VPZoneVolumeType
    {
        Standard,
        Bullish,
        Bearish,
        Both
    }

    public enum VPZoneRenderQuality
    {
        Manual,
        Adaptive
    }

    public class RedTailVPZone : DrawingTool
    {
        #region Variables
        
        private List<double> volumes = new List<double>();
        private double highestPrice;
        private double lowestPrice;
        private double priceInterval;
        
        private bool isCalculated  = false;
        private int  buildingStep  = 0;
        
        // Store calculated prices so they survive property edits
        private double cahcedVAH = double.NaN;
        private double cachedPOC = double.NaN;
        private double cachedVAL = double.NaN;
        
        // Store the bar index of the absolute high and low wick candles
        private int    cachedHighBarIdx = -1;
        private int    cachedLowBarIdx  = -1;
        
        // Volume profile visual data (for FRVP-style rendering)
        private int    vpPocIndex   = -1;
        private int    vpVAUp       = -1;
        private int    vpVADown     = -1;
        private double vpMaxVolume  = 0;
        private List<bool> volumePolarities = new List<bool>();
        
        #endregion
        
        #region Anchors
        
        [Display(Order = 1)]
        public ChartAnchor StartAnchor { get; set; }
        
        [Display(Order = 2)]
        public ChartAnchor EndAnchor { get; set; }
        
        public override IEnumerable<ChartAnchor> Anchors
        {
            get { return new[] { StartAnchor, EndAnchor }; }
        }
        
        #endregion
        
        #region Properties
        
        [NinjaScriptProperty]
        [Range(10, 500)]
        [Display(Name = "Number of Volume Rows", Order = 1, GroupName = "1. Volume Profile")]
        public int NumberOfRows { get; set; }
        
        [NinjaScriptProperty]
        [Range(50, 90)]
        [Display(Name = "Value Area %", Order = 2, GroupName = "1. Volume Profile")]
        public int ValueAreaPercentage { get; set; }
        
        [XmlIgnore]
        [Display(Name = "Zone Fill Color", Order = 1, GroupName = "2. Zone Appearance")]
        public System.Windows.Media.Brush ZoneFillBrush { get; set; }
        
        [Browsable(false)]
        public string ZoneFillBrushSerializable
        {
            get { return Serialize.BrushToString(ZoneFillBrush); }
            set { ZoneFillBrush = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Zone Opacity %", Order = 2, GroupName = "2. Zone Appearance")]
        public int ZoneOpacity { get; set; }
        

        
        [XmlIgnore]
        [Display(Name = "POC Line Color", Order = 1, GroupName = "3. POC Line")]
        public System.Windows.Media.Brush POCBrush { get; set; }
        
        [Browsable(false)]
        public string POCBrushSerializable
        {
            get { return Serialize.BrushToString(POCBrush); }
            set { POCBrush = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [Range(1, 5)]
        [Display(Name = "POC Line Thickness", Order = 2, GroupName = "3. POC Line")]
        public int POCThickness { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "POC Line Style", Order = 3, GroupName = "3. POC Line")]
        public DashStyleHelper POCLineStyle { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show VAH/VAL Lines", Order = 1, GroupName = "4. VAH/VAL Lines")]
        public bool ShowVALines { get; set; }
        
        [XmlIgnore]
        [Display(Name = "VAH Line Color", Order = 2, GroupName = "4. VAH/VAL Lines")]
        public System.Windows.Media.Brush VAHBrush { get; set; }
        
        [Browsable(false)]
        public string VAHBrushSerializable
        {
            get { return Serialize.BrushToString(VAHBrush); }
            set { VAHBrush = Serialize.StringToBrush(value); }
        }
        
        [XmlIgnore]
        [Display(Name = "VAL Line Color", Order = 3, GroupName = "4. VAH/VAL Lines")]
        public System.Windows.Media.Brush VALBrush { get; set; }
        
        [Browsable(false)]
        public string VALBrushSerializable
        {
            get { return Serialize.BrushToString(VALBrush); }
            set { VALBrush = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [Range(1, 5)]
        [Display(Name = "VAH/VAL Line Thickness", Order = 4, GroupName = "4. VAH/VAL Lines")]
        public int VALineThickness { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Selection Rectangle", Order = 1, GroupName = "5. Selection Rectangle")]
        public bool ShowSelectionRect { get; set; }
        
        [XmlIgnore]
        [Display(Name = "Rectangle Color", Order = 2, GroupName = "5. Selection Rectangle")]
        public System.Windows.Media.Brush SelectionRectBrush { get; set; }
        
        [Browsable(false)]
        public string SelectionRectBrushSerializable
        {
            get { return Serialize.BrushToString(SelectionRectBrush); }
            set { SelectionRectBrush = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [Range(5, 100)]
        [Display(Name = "Rectangle Opacity %", Order = 3, GroupName = "5. Selection Rectangle")]
        public int SelectionRectOpacity { get; set; }
        
        // --- Range High / Low Lines ---
        
        [NinjaScriptProperty]
        [Display(Name = "Show Range High Line", Order = 1, GroupName = "6. Range High/Low Lines")]
        public bool ShowRangeHighLine { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Range Low Line", Order = 2, GroupName = "6. Range High/Low Lines")]
        public bool ShowRangeLowLine { get; set; }
        
        [XmlIgnore]
        [Display(Name = "Range High Line Color", Order = 3, GroupName = "6. Range High/Low Lines")]
        public System.Windows.Media.Brush RangeHighBrush { get; set; }
        
        [Browsable(false)]
        public string RangeHighBrushSerializable
        {
            get { return Serialize.BrushToString(RangeHighBrush); }
            set { RangeHighBrush = Serialize.StringToBrush(value); }
        }
        
        [XmlIgnore]
        [Display(Name = "Range Low Line Color", Order = 4, GroupName = "6. Range High/Low Lines")]
        public System.Windows.Media.Brush RangeLowBrush { get; set; }
        
        [Browsable(false)]
        public string RangeLowBrushSerializable
        {
            get { return Serialize.BrushToString(RangeLowBrush); }
            set { RangeLowBrush = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [Range(1, 5)]
        [Display(Name = "Range Line Thickness", Order = 5, GroupName = "6. Range High/Low Lines")]
        public int RangeLineThickness { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Range Line Style", Order = 6, GroupName = "6. Range High/Low Lines")]
        public DashStyleHelper RangeLineStyle { get; set; }
        
        // ===== Volume Profile Histogram (FRVP-style) =====
        
        [NinjaScriptProperty]
        [Display(Name = "Show Volume Profile", Description = "Display FRVP-style volume histogram within the selection range", Order = 1, GroupName = "7. Volume Profile Histogram")]
        public bool ShowVolumeProfile { get; set; }
        
        [NinjaScriptProperty]
        [Range(5, 100)]
        [Display(Name = "Profile Width %", Description = "Width of histogram as % of selection range width", Order = 2, GroupName = "7. Volume Profile Histogram")]
        public int ProfileWidth { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Alignment", Order = 3, GroupName = "7. Volume Profile Histogram")]
        public VPZoneAlignment VPAlignment { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Volume Type", Description = "Standard, Bullish, Bearish, or Both (polarity coloring)", Order = 4, GroupName = "7. Volume Profile Histogram")]
        public VPZoneVolumeType VolumeType { get; set; }
        
        [XmlIgnore]
        [Display(Name = "Bar Color", Order = 5, GroupName = "7. Volume Profile Histogram")]
        public System.Windows.Media.Brush VPBarColor { get; set; }
        
        [Browsable(false)]
        public string VPBarColorSerializable
        {
            get { return Serialize.BrushToString(VPBarColor); }
            set { VPBarColor = Serialize.StringToBrush(value); }
        }
        
        [XmlIgnore]
        [Display(Name = "Bullish Bar Color", Description = "Color for bullish-dominant rows (Bullish/Both modes)", Order = 6, GroupName = "7. Volume Profile Histogram")]
        public System.Windows.Media.Brush VPBullishBarColor { get; set; }
        
        [Browsable(false)]
        public string VPBullishBarColorSerializable
        {
            get { return Serialize.BrushToString(VPBullishBarColor); }
            set { VPBullishBarColor = Serialize.StringToBrush(value); }
        }
        
        [XmlIgnore]
        [Display(Name = "Bearish Bar Color", Description = "Color for bearish-dominant rows (Bearish/Both modes)", Order = 7, GroupName = "7. Volume Profile Histogram")]
        public System.Windows.Media.Brush VPBearishBarColor { get; set; }
        
        [Browsable(false)]
        public string VPBearishBarColorSerializable
        {
            get { return Serialize.BrushToString(VPBearishBarColor); }
            set { VPBearishBarColor = Serialize.StringToBrush(value); }
        }
        
        [XmlIgnore]
        [Display(Name = "Value Area Bar Color", Description = "Color for bars within value area (Standard mode)", Order = 8, GroupName = "7. Volume Profile Histogram")]
        public System.Windows.Media.Brush VPValueAreaBarColor { get; set; }
        
        [Browsable(false)]
        public string VPValueAreaBarColorSerializable
        {
            get { return Serialize.BrushToString(VPValueAreaBarColor); }
            set { VPValueAreaBarColor = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [Range(5, 100)]
        [Display(Name = "Bar Opacity %", Order = 9, GroupName = "7. Volume Profile Histogram")]
        public int VPBarOpacity { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Bar Thickness", Description = "Used in Manual render quality mode", Order = 10, GroupName = "7. Volume Profile Histogram")]
        public int VPBarThickness { get; set; }
        
        // --- Gradient Fill ---
        
        [NinjaScriptProperty]
        [Display(Name = "Enable Gradient Fill", Description = "Apply gradient effect to volume bars", Order = 1, GroupName = "7b. VP Gradient Fill")]
        public bool VPEnableGradientFill { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Gradient Intensity", Description = "0=solid, 100=maximum fade effect", Order = 2, GroupName = "7b. VP Gradient Fill")]
        public int VPGradientIntensity { get; set; }
        
        // --- Adaptive Rendering ---
        
        [NinjaScriptProperty]
        [Display(Name = "Render Quality", Description = "Manual = fixed thickness. Adaptive = auto-sizes bars and smooths profile.", Order = 1, GroupName = "7c. VP Adaptive Rendering")]
        public VPZoneRenderQuality VPRenderQuality { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 5)]
        [Display(Name = "Smoothing Passes", Description = "Gaussian smoothing (0=raw, 2-3=recommended)", Order = 2, GroupName = "7c. VP Adaptive Rendering")]
        public int VPSmoothingPasses { get; set; }
        
        [NinjaScriptProperty]
        [Range(1.0f, 10.0f)]
        [Display(Name = "Min Bar Pixel Height", Order = 3, GroupName = "7c. VP Adaptive Rendering")]
        public float VPMinBarPixelHeight { get; set; }
        
        [NinjaScriptProperty]
        [Range(2.0f, 20.0f)]
        [Display(Name = "Max Bar Pixel Height", Order = 4, GroupName = "7c. VP Adaptive Rendering")]
        public float VPMaxBarPixelHeight { get; set; }
        
        #endregion
        
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description  = "Fixed Range Volume Profile Zone - Draws VAH/VAL rectangle with POC line, optional FRVP-style histogram";
                Name         = "RedTail VP Zone";
                IsAutoScale  = false;
                DrawingState = DrawingState.Building;
                
                NumberOfRows        = 100;
                ValueAreaPercentage = 68;
                
                ZoneFillBrush       = System.Windows.Media.Brushes.DodgerBlue;
                ZoneOpacity         = 15;
                POCBrush     = System.Windows.Media.Brushes.Red;
                POCThickness = 2;
                POCLineStyle = DashStyleHelper.Solid;
                
                ShowVALines     = true;
                VAHBrush        = System.Windows.Media.Brushes.DodgerBlue;
                VALBrush        = System.Windows.Media.Brushes.DodgerBlue;
                VALineThickness = 1;
                
                ShowSelectionRect    = true;
                SelectionRectBrush   = System.Windows.Media.Brushes.Gray;
                SelectionRectOpacity = 25;
                
                ShowRangeHighLine    = false;
                ShowRangeLowLine     = false;
                RangeHighBrush       = System.Windows.Media.Brushes.White;
                RangeLowBrush        = System.Windows.Media.Brushes.White;
                RangeLineThickness   = 1;
                RangeLineStyle       = DashStyleHelper.Solid;
                
                // Volume Profile Histogram defaults
                ShowVolumeProfile    = false;
                ProfileWidth         = 30;
                VPAlignment          = VPZoneAlignment.Left;
                VolumeType           = VPZoneVolumeType.Standard;
                VPBarColor           = System.Windows.Media.Brushes.Gray;
                VPBullishBarColor    = System.Windows.Media.Brushes.Green;
                VPBearishBarColor    = System.Windows.Media.Brushes.Red;
                VPValueAreaBarColor  = System.Windows.Media.Brushes.RoyalBlue;
                VPBarOpacity         = 40;
                VPBarThickness       = 2;
                VPEnableGradientFill = false;
                VPGradientIntensity  = 70;
                VPRenderQuality      = VPZoneRenderQuality.Adaptive;
                VPSmoothingPasses    = 2;
                VPMinBarPixelHeight  = 2.0f;
                VPMaxBarPixelHeight  = 8.0f;
                
                StartAnchor = new ChartAnchor
                {
                    IsEditing   = true,
                    DrawingTool = this,
                    DisplayName = NinjaTrader.Custom.Resource.NinjaScriptDrawingToolAnchor,
                };
                EndAnchor = new ChartAnchor
                {
                    IsEditing   = true,
                    DrawingTool = this,
                    DisplayName = NinjaTrader.Custom.Resource.NinjaScriptDrawingToolAnchorEnd,
                };
            }
            else if (State == State.Configure)
            {
                buildingStep = 0;
            }
        }
        
        #region Mouse Interaction
        
        public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, System.Windows.Point point)
        {
            if (DrawingState == DrawingState.Building)
                return Cursors.Pen;
            
            if (DrawingState == DrawingState.Moving || DrawingState == DrawingState.Editing)
                return Cursors.SizeAll;
            
            if (isCalculated && !double.IsNaN(cahcedVAH) && !double.IsNaN(cachedVAL))
            {
                float vahY    = chartScale.GetYByValue(cahcedVAH);
                float valY    = chartScale.GetYByValue(cachedVAL);
                float topY    = Math.Min(vahY, valY);
                float bottomY = Math.Max(vahY, valY);
                
                System.Windows.Point startPt = StartAnchor.GetPoint(chartControl, chartPanel, chartScale);
                System.Windows.Point endPt   = EndAnchor.GetPoint(chartControl, chartPanel, chartScale);
                float leftX = (float)Math.Min(startPt.X, endPt.X);
                
                if (point.Y >= topY && point.Y <= bottomY && point.X >= leftX)
                    return Cursors.SizeAll;
            }
            
            return null;
        }
        
        public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Building)
            {
                if (buildingStep == 0)
                {
                    dataPoint.CopyDataValues(StartAnchor);
                    StartAnchor.IsEditing = false;
                    
                    dataPoint.CopyDataValues(EndAnchor);
                    EndAnchor.IsEditing = true;
                    
                    buildingStep = 1;
                }
                else if (buildingStep == 1)
                {
                    dataPoint.CopyDataValues(EndAnchor);
                    EndAnchor.IsEditing = false;
                    
                    DrawingState = DrawingState.Normal;
                    IsSelected   = false;
                    buildingStep = 0;
                    
                    CalculateVolumeProfile(chartControl);
                }
            }
            else if (DrawingState == DrawingState.Normal)
            {
                System.Windows.Point clickPt = dataPoint.GetPoint(chartControl, chartPanel, chartScale);
                System.Windows.Point sPt     = StartAnchor.GetPoint(chartControl, chartPanel, chartScale);
                System.Windows.Point ePt     = EndAnchor.GetPoint(chartControl, chartPanel, chartScale);
                
                if ((clickPt - sPt).Length < 15)
                {
                    DrawingState = DrawingState.Editing;
                    StartAnchor.IsEditing = true;
                }
                else if ((clickPt - ePt).Length < 15)
                {
                    DrawingState = DrawingState.Editing;
                    EndAnchor.IsEditing = true;
                }
                else if (IsPointOnDrawing(chartControl, chartPanel, chartScale, clickPt))
                {
                    DrawingState = DrawingState.Moving;
                }
                else
                {
                    // Click was outside the drawing — deselect
                    IsSelected   = false;
                    DrawingState = DrawingState.Normal;
                }
            }
        }
        
        public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Building && buildingStep == 1)
            {
                dataPoint.CopyDataValues(EndAnchor);
                CalculateVolumeProfile(chartControl);
            }
            else if (DrawingState == DrawingState.Editing)
            {
                if (StartAnchor.IsEditing)
                {
                    dataPoint.CopyDataValues(StartAnchor);
                    CalculateVolumeProfile(chartControl);
                }
                else if (EndAnchor.IsEditing)
                {
                    dataPoint.CopyDataValues(EndAnchor);
                    CalculateVolumeProfile(chartControl);
                }
            }
        }
        
        public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Building)
                return;
            
            if (DrawingState == DrawingState.Editing || DrawingState == DrawingState.Moving)
            {
                StartAnchor.IsEditing = false;
                EndAnchor.IsEditing   = false;
                DrawingState = DrawingState.Normal;
                CalculateVolumeProfile(chartControl);
            }
        }
        
        private bool IsPointOnDrawing(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, System.Windows.Point point)
        {
            if (!isCalculated || double.IsNaN(cahcedVAH) || double.IsNaN(cachedVAL))
                return false;
            
            float vahY    = chartScale.GetYByValue(cahcedVAH);
            float valY    = chartScale.GetYByValue(cachedVAL);
            float topY    = Math.Min(vahY, valY);
            float bottomY = Math.Max(vahY, valY);
            
            System.Windows.Point startPt = StartAnchor.GetPoint(chartControl, chartPanel, chartScale);
            System.Windows.Point endPt   = EndAnchor.GetPoint(chartControl, chartPanel, chartScale);
            float leftX = (float)Math.Min(startPt.X, endPt.X);
            float rightEdge = chartPanel.X + chartPanel.W;
            
            return point.Y >= topY && point.Y <= bottomY && point.X >= leftX && point.X <= rightEdge;
        }
        
        #endregion
        
        #region Volume Profile Calculation
        
        private void CalculateVolumeProfile(ChartControl chartControl)
        {
            isCalculated = false;
            
            try
            {
                if (chartControl == null || chartControl.BarsArray == null || chartControl.BarsArray.Count == 0)
                    return;
                
                ChartBars chartBars = chartControl.BarsArray[0];
                if (chartBars == null || chartBars.Bars == null || chartBars.Bars.Count < 2)
                    return;
                
                NinjaTrader.Data.Bars bars = chartBars.Bars;
                
                if (StartAnchor.Time == DateTime.MinValue || EndAnchor.Time == DateTime.MinValue)
                    return;
                if (StartAnchor.Time == EndAnchor.Time)
                    return;
                
                int startIdx = bars.GetBar(StartAnchor.Time);
                int endIdx   = bars.GetBar(EndAnchor.Time);
                
                int fromBar = Math.Min(startIdx, endIdx);
                int toBar   = Math.Max(startIdx, endIdx);
                
                fromBar = Math.Max(0, fromBar);
                toBar   = Math.Min(bars.Count - 1, toBar);
                
                if (fromBar >= toBar)
                    return;
                
                highestPrice = double.MinValue;
                lowestPrice  = double.MaxValue;
                cachedHighBarIdx = fromBar;
                cachedLowBarIdx  = fromBar;
                
                for (int i = fromBar; i <= toBar; i++)
                {
                    double h = bars.GetHigh(i);
                    double l = bars.GetLow(i);
                    if (h > highestPrice) { highestPrice = h; cachedHighBarIdx = i; }
                    if (l < lowestPrice)  { lowestPrice  = l; cachedLowBarIdx  = i; }
                }
                
                if (highestPrice <= lowestPrice)
                    return;
                
                priceInterval = (highestPrice - lowestPrice) / (NumberOfRows - 1);
                if (priceInterval <= 0)
                    return;
                
                volumes.Clear();
                volumePolarities.Clear();
                for (int i = 0; i < NumberOfRows; i++)
                {
                    volumes.Add(0);
                    volumePolarities.Add(true);
                }
                
                // Separate bullish/bearish volume tracking for polarity
                double[] bullishVolume = new double[NumberOfRows];
                double[] bearishVolume = new double[NumberOfRows];
                
                for (int i = fromBar; i <= toBar; i++)
                {
                    double barLow    = bars.GetLow(i);
                    double barHigh   = bars.GetHigh(i);
                    double barOpen   = bars.GetOpen(i);
                    double barClose  = bars.GetClose(i);
                    double barVolume = bars.GetVolume(i);
                    bool   isBullish = barClose >= barOpen;
                    
                    int minPriceIndex = (int)Math.Floor((barLow - lowestPrice) / priceInterval);
                    int maxPriceIndex = (int)Math.Ceiling((barHigh - lowestPrice) / priceInterval);
                    
                    minPriceIndex = Math.Max(0, Math.Min(minPriceIndex, NumberOfRows - 1));
                    maxPriceIndex = Math.Max(0, Math.Min(maxPriceIndex, NumberOfRows - 1));
                    
                    int touchedLevels = maxPriceIndex - minPriceIndex + 1;
                    if (touchedLevels > 0)
                    {
                        double volumePerLevel = barVolume / touchedLevels;
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
                
                // Set polarity for each row
                for (int i = 0; i < NumberOfRows; i++)
                    volumePolarities[i] = bullishVolume[i] >= bearishVolume[i];
                
                vpMaxVolume = 0;
                vpPocIndex  = 0;
                for (int i = 0; i < volumes.Count; i++)
                {
                    if (volumes[i] > vpMaxVolume)
                    {
                        vpMaxVolume = volumes[i];
                        vpPocIndex  = i;
                    }
                }
                
                if (vpMaxVolume == 0)
                    return;
                
                double sumVolume = 0;
                for (int i = 0; i < volumes.Count; i++)
                    sumVolume += volumes[i];
                
                double vaVolume = sumVolume * ValueAreaPercentage / 100.0;
                vpVAUp   = vpPocIndex;
                vpVADown = vpPocIndex;
                double vaSum = vpMaxVolume;
                
                while (vaSum < vaVolume)
                {
                    double vUp   = (vpVAUp < NumberOfRows - 1) ? volumes[vpVAUp + 1] : 0.0;
                    double vDown = (vpVADown > 0) ? volumes[vpVADown - 1] : 0.0;
                    
                    if (vUp == 0 && vDown == 0) break;
                    
                    if (vUp >= vDown) { vaSum += vUp; vpVAUp++; }
                    else              { vaSum += vDown; vpVADown--; }
                }
                
                cachedPOC = lowestPrice + priceInterval * vpPocIndex;
                cahcedVAH = lowestPrice + priceInterval * vpVAUp;
                cachedVAL = lowestPrice + priceInterval * vpVADown;
                
                isCalculated = true;
            }
            catch (Exception)
            {
                isCalculated = false;
            }
        }
        
        #endregion
        
        #region Volume Profile Histogram Helpers (FRVP-style)
        
        private double[] GetSmoothedVolumes(List<double> rawVolumes, int passes)
        {
            if (rawVolumes == null || rawVolumes.Count == 0)
                return new double[0];
            
            double[] current = rawVolumes.ToArray();
            double[] buffer  = new double[current.Length];
            
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < current.Length; i++)
                {
                    double sum       = current[i] * 4.0;
                    double weightSum = 4.0;
                    
                    if (i - 1 >= 0)              { sum += current[i - 1] * 2.0; weightSum += 2.0; }
                    if (i + 1 < current.Length)   { sum += current[i + 1] * 2.0; weightSum += 2.0; }
                    if (i - 2 >= 0)              { sum += current[i - 2] * 1.0; weightSum += 1.0; }
                    if (i + 2 < current.Length)   { sum += current[i + 2] * 1.0; weightSum += 1.0; }
                    
                    buffer[i] = sum / weightSum;
                }
                
                double[] temp = current;
                current = buffer;
                buffer  = temp;
            }
            
            return current;
        }
        
        private float CalculateAdaptiveBarThickness(ChartScale chartScale, double lowPrice, double highPrice, int rowCount)
        {
            float lowY  = chartScale.GetYByValue(lowPrice);
            float highY = chartScale.GetYByValue(highPrice);
            float totalPixelHeight = Math.Abs(lowY - highY);
            
            float pixelsPerRow    = totalPixelHeight / Math.Max(1, rowCount);
            float idealThickness  = pixelsPerRow * 0.85f;
            
            return Math.Max(VPMinBarPixelHeight, Math.Min(idealThickness, VPMaxBarPixelHeight));
        }
        
        private SharpDX.Direct2D1.LinearGradientBrush CreateGradientBrush(
            SharpDX.Direct2D1.RenderTarget rt,
            System.Windows.Media.Brush baseColor, float startX, float endX, float y, float baseOpacity)
        {
            if (!VPEnableGradientFill || VPGradientIntensity <= 0)
                return null;
            
            try
            {
                System.Windows.Media.Color mediaColor;
                if (baseColor is System.Windows.Media.SolidColorBrush solidBrush)
                    mediaColor = solidBrush.Color;
                else
                    mediaColor = System.Windows.Media.Colors.Gray;
                
                float intensityFactor = VPGradientIntensity / 100.0f;
                float startOpacity    = baseOpacity * (1.0f - intensityFactor);
                float endOpacity      = baseOpacity;
                
                var gradientStops = new SharpDX.Direct2D1.GradientStop[2];
                gradientStops[0] = new SharpDX.Direct2D1.GradientStop
                {
                    Position = 0.0f,
                    Color    = new SharpDX.Color4(mediaColor.R / 255f, mediaColor.G / 255f, mediaColor.B / 255f, startOpacity)
                };
                gradientStops[1] = new SharpDX.Direct2D1.GradientStop
                {
                    Position = 1.0f,
                    Color    = new SharpDX.Color4(mediaColor.R / 255f, mediaColor.G / 255f, mediaColor.B / 255f, endOpacity)
                };
                
                var gradientStopCollection = new SharpDX.Direct2D1.GradientStopCollection(rt, gradientStops);
                var gradientBrush = new SharpDX.Direct2D1.LinearGradientBrush(
                    rt,
                    new SharpDX.Direct2D1.LinearGradientBrushProperties
                    {
                        StartPoint = new SharpDX.Vector2(startX, y),
                        EndPoint   = new SharpDX.Vector2(endX, y)
                    },
                    gradientStopCollection
                );
                gradientStopCollection.Dispose();
                return gradientBrush;
            }
            catch { return null; }
        }
        
        #endregion
        
        #region Rendering
        
        public override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            // Auto-recalculate if we lost state (e.g. after property dialog Apply)
            // but still have valid anchors
            if (!isCalculated && DrawingState != DrawingState.Building
                && StartAnchor.Time != DateTime.MinValue 
                && EndAnchor.Time != DateTime.MinValue
                && StartAnchor.Time != EndAnchor.Time)
            {
                CalculateVolumeProfile(chartControl);
            }
            
            // Also recalc during live preview while building
            if (DrawingState == DrawingState.Building && buildingStep == 1 && !isCalculated)
            {
                CalculateVolumeProfile(chartControl);
            }
            
            if (!isCalculated || double.IsNaN(cahcedVAH) || double.IsNaN(cachedVAL) || double.IsNaN(cachedPOC))
                return;
            
            if (RenderTarget == null || chartControl == null || chartScale == null)
                return;
            
            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
            if (chartPanel == null)
                return;
            
            float vahY = chartScale.GetYByValue(cahcedVAH);
            float valY = chartScale.GetYByValue(cachedVAL);
            float pocY = chartScale.GetYByValue(cachedPOC);
            
            float startX     = (float)StartAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
            float endDrawX   = (float)EndAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
            float zoneStartX = Math.Min(startX, endDrawX);
            float zoneEndX   = Math.Max(startX, endDrawX);
            float rightEdge  = chartPanel.X + chartPanel.W;
            
            float topY    = Math.Min(vahY, valY);
            float bottomY = Math.Max(vahY, valY);
            
            if (bottomY - topY < 1) return;
            
            SharpDX.Direct2D1.SolidColorBrush dxFillBrush   = null;
            SharpDX.Direct2D1.SolidColorBrush dxPocBrush    = null;
            SharpDX.Direct2D1.SolidColorBrush dxVahBrush    = null;
            SharpDX.Direct2D1.SolidColorBrush dxValBrush    = null;
            StrokeStyle pocStrokeStyle = null;
            
            try
            {
                // ===========================
                // VOLUME PROFILE HISTOGRAM (rendered first, behind everything)
                // ===========================
                if (ShowVolumeProfile && volumes.Count > 0 && vpMaxVolume > 0)
                {
                    RenderVolumeProfileHistogram(chartControl, chartScale, chartPanel, zoneStartX, zoneEndX);
                }
                
                // Zone fill
                dxFillBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                    WpfToColor4(ZoneFillBrush, ZoneOpacity / 100.0f));
                
                SharpDX.RectangleF zoneRect = new SharpDX.RectangleF(
                    zoneStartX, topY, rightEdge - zoneStartX, bottomY - topY);
                RenderTarget.FillRectangle(zoneRect, dxFillBrush);
                
                // POC line
                dxPocBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, WpfToColor4(POCBrush));
                
                SharpDX.Vector2 pocStart = new SharpDX.Vector2(zoneStartX, pocY);
                SharpDX.Vector2 pocEnd   = new SharpDX.Vector2(rightEdge, pocY);
                
                if (POCLineStyle != DashStyleHelper.Solid)
                {
                    StrokeStyleProperties strokeProps = new StrokeStyleProperties();
                    switch (POCLineStyle)
                    {
                        case DashStyleHelper.Dash:       strokeProps.DashStyle = SharpDX.Direct2D1.DashStyle.Dash; break;
                        case DashStyleHelper.Dot:        strokeProps.DashStyle = SharpDX.Direct2D1.DashStyle.Dot; break;
                        case DashStyleHelper.DashDot:    strokeProps.DashStyle = SharpDX.Direct2D1.DashStyle.DashDot; break;
                        case DashStyleHelper.DashDotDot: strokeProps.DashStyle = SharpDX.Direct2D1.DashStyle.DashDotDot; break;
                    }
                    pocStrokeStyle = new StrokeStyle(RenderTarget.Factory, strokeProps);
                    RenderTarget.DrawLine(pocStart, pocEnd, dxPocBrush, POCThickness, pocStrokeStyle);
                }
                else
                {
                    RenderTarget.DrawLine(pocStart, pocEnd, dxPocBrush, POCThickness);
                }
                
                // VAH/VAL lines
                if (ShowVALines)
                {
                    dxVahBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, WpfToColor4(VAHBrush));
                    dxValBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, WpfToColor4(VALBrush));
                    
                    RenderTarget.DrawLine(new SharpDX.Vector2(zoneStartX, vahY), new SharpDX.Vector2(rightEdge, vahY), dxVahBrush, VALineThickness);
                    RenderTarget.DrawLine(new SharpDX.Vector2(zoneStartX, valY), new SharpDX.Vector2(rightEdge, valY), dxValBrush, VALineThickness);
                }
                
                // Selection rectangle - faint outline around the candles used for calculation
                if (ShowSelectionRect)
                {
                    float selLeftX  = Math.Min(startX, endDrawX);
                    float selRightX = Math.Max(startX, endDrawX);
                    float selTopY   = chartScale.GetYByValue(highestPrice);
                    float selBottomY = chartScale.GetYByValue(lowestPrice);
                    
                    using (var dxSelBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                        WpfToColor4(SelectionRectBrush, SelectionRectOpacity / 100.0f)))
                    using (var selStroke = new StrokeStyle(RenderTarget.Factory,
                        new StrokeStyleProperties { DashStyle = SharpDX.Direct2D1.DashStyle.Dash }))
                    {
                        SharpDX.RectangleF selRect = new SharpDX.RectangleF(
                            selLeftX, selTopY, selRightX - selLeftX, selBottomY - selTopY);
                        RenderTarget.DrawRectangle(selRect, dxSelBrush, 1f, selStroke);
                    }
                }
                
                // Range High / Low horizontal lines starting from the wick candle
                if (ShowRangeHighLine || ShowRangeLowLine)
                {
                    ChartBars chartBars = chartControl.BarsArray[0];
                    NinjaTrader.Data.Bars bars = (chartBars != null) ? chartBars.Bars : null;
                    
                    StrokeStyle rangeStroke = null;
                    if (RangeLineStyle != DashStyleHelper.Solid)
                    {
                        StrokeStyleProperties rsp = new StrokeStyleProperties();
                        switch (RangeLineStyle)
                        {
                            case DashStyleHelper.Dash:       rsp.DashStyle = SharpDX.Direct2D1.DashStyle.Dash; break;
                            case DashStyleHelper.Dot:        rsp.DashStyle = SharpDX.Direct2D1.DashStyle.Dot; break;
                            case DashStyleHelper.DashDot:    rsp.DashStyle = SharpDX.Direct2D1.DashStyle.DashDot; break;
                            case DashStyleHelper.DashDotDot: rsp.DashStyle = SharpDX.Direct2D1.DashStyle.DashDotDot; break;
                        }
                        rangeStroke = new StrokeStyle(RenderTarget.Factory, rsp);
                    }
                    
                    try
                    {
                        if (ShowRangeHighLine && cachedHighBarIdx >= 0 && bars != null)
                        {
                            float highY = chartScale.GetYByValue(highestPrice);
                            float highX = chartControl.GetXByBarIndex(chartBars, cachedHighBarIdx);
                            
                            using (var dxHighBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, WpfToColor4(RangeHighBrush)))
                            {
                                if (rangeStroke != null)
                                    RenderTarget.DrawLine(new SharpDX.Vector2(highX, highY), new SharpDX.Vector2(rightEdge, highY), dxHighBrush, RangeLineThickness, rangeStroke);
                                else
                                    RenderTarget.DrawLine(new SharpDX.Vector2(highX, highY), new SharpDX.Vector2(rightEdge, highY), dxHighBrush, RangeLineThickness);
                            }
                        }
                        
                        if (ShowRangeLowLine && cachedLowBarIdx >= 0 && bars != null)
                        {
                            float lowY = chartScale.GetYByValue(lowestPrice);
                            float lowX = chartControl.GetXByBarIndex(chartBars, cachedLowBarIdx);
                            
                            using (var dxLowBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, WpfToColor4(RangeLowBrush)))
                            {
                                if (rangeStroke != null)
                                    RenderTarget.DrawLine(new SharpDX.Vector2(lowX, lowY), new SharpDX.Vector2(rightEdge, lowY), dxLowBrush, RangeLineThickness, rangeStroke);
                                else
                                    RenderTarget.DrawLine(new SharpDX.Vector2(lowX, lowY), new SharpDX.Vector2(rightEdge, lowY), dxLowBrush, RangeLineThickness);
                            }
                        }
                    }
                    finally
                    {
                        if (rangeStroke != null) rangeStroke.Dispose();
                    }
                }
            }
            finally
            {
                if (dxFillBrush != null)   dxFillBrush.Dispose();
                if (dxPocBrush != null)    dxPocBrush.Dispose();
                if (dxVahBrush != null)    dxVahBrush.Dispose();
                if (dxValBrush != null)    dxValBrush.Dispose();
                if (pocStrokeStyle != null) pocStrokeStyle.Dispose();
            }
        }
        
        /// <summary>
        /// Renders the FRVP-style volume profile histogram within the selection range.
        /// Uses the same visual logic as RedTailFRVPFib: gradient fill, adaptive rendering, polarity coloring.
        /// </summary>
        private void RenderVolumeProfileHistogram(ChartControl chartControl, ChartScale chartScale, ChartPanel chartPanel,
            float selLeftX, float selRightX)
        {
            float rangeWidth = selRightX - selLeftX;
            if (rangeWidth < 5) rangeWidth = 5;
            
            float profileWidthPixels = rangeWidth * (ProfileWidth / 100f);
            float barOpacity = VPBarOpacity / 100f;
            
            float profileLeft, profileRight;
            if (VPAlignment == VPZoneAlignment.Left)
            {
                profileLeft  = selLeftX;
                profileRight = selLeftX + profileWidthPixels;
            }
            else
            {
                profileRight = selRightX;
                profileLeft  = selRightX - profileWidthPixels;
            }
            
            // Adaptive rendering: smooth volumes and auto-size bars
            bool useAdaptive = VPRenderQuality == VPZoneRenderQuality.Adaptive;
            double[] renderVolumes = useAdaptive && VPSmoothingPasses > 0
                ? GetSmoothedVolumes(volumes, VPSmoothingPasses)
                : volumes.ToArray();
            
            // Find max of (possibly smoothed) volumes for width scaling
            double renderMaxVol = 0;
            for (int i = 0; i < renderVolumes.Length; i++)
                if (renderVolumes[i] > renderMaxVol) renderMaxVol = renderVolumes[i];
            if (renderMaxVol <= 0) renderMaxVol = vpMaxVolume;
            
            // Calculate adaptive bar thickness
            float adaptiveThickness = useAdaptive
                ? CalculateAdaptiveBarThickness(chartScale, lowestPrice, highestPrice, volumes.Count)
                : 0;
            
            // Draw volume bars
            for (int i = 0; i < renderVolumes.Length; i++)
            {
                double vol = renderVolumes[i];
                if (vol <= 0) continue;
                
                double priceLevel = lowestPrice + priceInterval * i;
                float y = chartScale.GetYByValue(priceLevel);
                
                double volumeRatio = vol / renderMaxVol;
                float barWidth = (float)(volumeRatio * profileWidthPixels);
                
                float barLeft, barRight;
                if (VPAlignment == VPZoneAlignment.Left)
                {
                    barLeft  = profileLeft;
                    barRight = profileLeft + barWidth;
                }
                else
                {
                    barRight = profileRight;
                    barLeft  = profileRight - barWidth;
                }
                
                bool isPOC = (i == vpPocIndex);
                bool isVA  = (i >= vpVADown && i <= vpVAUp);
                
                // Determine source color based on volume type and polarity (matches FRVP logic)
                System.Windows.Media.Brush sourceColor;
                if (isPOC)
                {
                    sourceColor = POCBrush;
                }
                else if (isVA && VolumeType == VPZoneVolumeType.Standard)
                {
                    sourceColor = VPValueAreaBarColor;
                }
                else if (VolumeType == VPZoneVolumeType.Standard)
                {
                    sourceColor = VPBarColor;
                }
                else
                {
                    // Polarity-based colors
                    if (VolumeType == VPZoneVolumeType.Bullish)
                        sourceColor = VPBullishBarColor;
                    else if (VolumeType == VPZoneVolumeType.Bearish)
                        sourceColor = VPBearishBarColor;
                    else // Both - show dominant polarity
                        sourceColor = (i < volumePolarities.Count && volumePolarities[i]) ? VPBullishBarColor : VPBearishBarColor;
                }
                
                float sourceOpacity = isPOC ? 1.0f : barOpacity;
                
                // Apply gradient or solid fill
                SharpDX.Direct2D1.SolidColorBrush solidBarBr     = null;
                SharpDX.Direct2D1.LinearGradientBrush gradientBr = null;
                SharpDX.Direct2D1.Brush barBrush = null;
                
                if (VPEnableGradientFill)
                {
                    gradientBr = CreateGradientBrush(RenderTarget, sourceColor, barLeft, barRight, y, sourceOpacity);
                    if (gradientBr != null)
                        barBrush = gradientBr;
                }
                
                if (barBrush == null)
                {
                    SharpDX.Color4 barC4 = WpfToColor4(sourceColor, sourceOpacity);
                    solidBarBr = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, barC4);
                    barBrush   = solidBarBr;
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
                    effectiveThickness = Math.Max(1, VPBarThickness - gapSize);
                }
                
                float adjustedY = y + (gapSize / 2.0f);
                
                RenderTarget.DrawLine(
                    new SharpDX.Vector2(barLeft, adjustedY),
                    new SharpDX.Vector2(barRight, adjustedY),
                    barBrush, effectiveThickness);
                
                gradientBr?.Dispose();
                solidBarBr?.Dispose();
            }
        }
        
        private static SharpDX.Color4 WpfToColor4(System.Windows.Media.Brush wpfBrush, float opacity = 1.0f)
        {
            System.Windows.Media.SolidColorBrush scb = wpfBrush as System.Windows.Media.SolidColorBrush;
            if (scb != null)
            {
                return new SharpDX.Color4(
                    scb.Color.R / 255.0f,
                    scb.Color.G / 255.0f,
                    scb.Color.B / 255.0f,
                    opacity * (scb.Color.A / 255.0f));
            }
            return new SharpDX.Color4(1f, 1f, 1f, opacity);
        }
        
        #endregion
        
        #region Overrides
        
        public override bool IsVisibleOnChart(ChartControl chartControl, ChartScale chartScale, DateTime firstTimeOnChart, DateTime lastTimeOnChart)
        {
            if (DrawingState == DrawingState.Building)
                return true;
            
            // Even if isCalculated is false, return true if anchors are valid
            // so OnRender gets a chance to recalculate
            if (StartAnchor.Time != DateTime.MinValue && EndAnchor.Time != DateTime.MinValue
                && StartAnchor.Time != EndAnchor.Time)
                return true;
            
            return isCalculated;
        }
        
        public override System.Windows.Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
        {
            if (!isCalculated)
                return new System.Windows.Point[0];
            
            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
            
            System.Windows.Point startPoint = StartAnchor.GetPoint(chartControl, chartPanel, chartScale);
            System.Windows.Point endPoint   = EndAnchor.GetPoint(chartControl, chartPanel, chartScale);
            
            float pocYSel = chartScale.GetYByValue(cachedPOC);
            
            return new[]
            {
                startPoint,
                endPoint,
                new System.Windows.Point((startPoint.X + endPoint.X) / 2, pocYSel)
            };
        }
        
        #endregion
    }
}