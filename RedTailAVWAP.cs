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
using SharpDX;
#endregion

//Created by RedTail Indicators - @_hawkeye_13
//RedTail Anchored VWAP Drawing Tool
//Version: v1.0.0

namespace NinjaTrader.NinjaScript.DrawingTools
{
    public class RedTailAVWAP : DrawingTool
    {
        #region Variables
        private ChartAnchor startAnchor;
        
        // AVWAP data
        private List<KeyValuePair<int, double>> avwapPoints = new List<KeyValuePair<int, double>>();
        private int avwapAnchorBarIdx = -1;
        private DateTime lastAvwapCalcTime = DateTime.MinValue;
        private int lastAvwapBarCount = -1;
        
        // Standard deviation band data
        private List<KeyValuePair<int, double>> upperBand1Points = new List<KeyValuePair<int, double>>();
        private List<KeyValuePair<int, double>> lowerBand1Points = new List<KeyValuePair<int, double>>();
        private List<KeyValuePair<int, double>> upperBand2Points = new List<KeyValuePair<int, double>>();
        private List<KeyValuePair<int, double>> lowerBand2Points = new List<KeyValuePair<int, double>>();
        private List<KeyValuePair<int, double>> upperBand3Points = new List<KeyValuePair<int, double>>();
        private List<KeyValuePair<int, double>> lowerBand3Points = new List<KeyValuePair<int, double>>();
        #endregion

        #region Anchors
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolAnchor", GroupName = "NinjaScriptGeneral")]
        public ChartAnchor StartAnchor
        {
            get { if (startAnchor == null) startAnchor = new ChartAnchor(); return startAnchor; }
            set { startAnchor = value; }
        }

        public override IEnumerable<ChartAnchor> Anchors
        {
            get { return new[] { StartAnchor }; }
        }
        #endregion

        #region State Management
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name            = "RedTailAVWAP";
                Description     = "RedTail Anchored VWAP. Click on any candle to anchor the VWAP from that point forward.";
                DrawingState    = DrawingState.Building;

                // AVWAP Line Settings
                AVWAPColor          = Brushes.DodgerBlue;
                AVWAPLineWidth      = 2;
                AVWAPLineStyle      = DashStyleHelper.Solid;
                AVWAPOpacity        = 100;
                ShowLabel           = true;
                LabelFontSize       = 11;
                
                // VWAP Source
                VWAPSource          = RedTailAVWAPSource.OHLC4;
                
                // Standard Deviation Bands
                ShowBand1           = false;
                Band1Multiplier     = 1.0;
                Band1Color          = Brushes.DodgerBlue;
                Band1Opacity        = 60;
                Band1LineWidth      = 1;
                Band1LineStyle      = DashStyleHelper.Dash;
                
                ShowBand2           = false;
                Band2Multiplier     = 2.0;
                Band2Color          = Brushes.MediumPurple;
                Band2Opacity        = 50;
                Band2LineWidth      = 1;
                Band2LineStyle      = DashStyleHelper.Dash;
                
                ShowBand3           = false;
                Band3Multiplier     = 3.0;
                Band3Color          = Brushes.Tomato;
                Band3Opacity        = 40;
                Band3LineWidth      = 1;
                Band3LineStyle      = DashStyleHelper.Dash;

                startAnchor = new ChartAnchor();
            }
        }

        public override void CopyTo(NinjaScript ninjaScript)
        {
            base.CopyTo(ninjaScript);

            RedTailAVWAP copy = ninjaScript as RedTailAVWAP;
            if (copy == null) return;

            copy.AVWAPColor      = AVWAPColor;
            copy.AVWAPLineWidth  = AVWAPLineWidth;
            copy.AVWAPLineStyle  = AVWAPLineStyle;
            copy.AVWAPOpacity    = AVWAPOpacity;
            copy.ShowLabel       = ShowLabel;
            copy.LabelFontSize   = LabelFontSize;
            copy.VWAPSource      = VWAPSource;
            
            copy.ShowBand1       = ShowBand1;
            copy.Band1Multiplier = Band1Multiplier;
            copy.Band1Color      = Band1Color;
            copy.Band1Opacity    = Band1Opacity;
            copy.Band1LineWidth  = Band1LineWidth;
            copy.Band1LineStyle  = Band1LineStyle;
            
            copy.ShowBand2       = ShowBand2;
            copy.Band2Multiplier = Band2Multiplier;
            copy.Band2Color      = Band2Color;
            copy.Band2Opacity    = Band2Opacity;
            copy.Band2LineWidth  = Band2LineWidth;
            copy.Band2LineStyle  = Band2LineStyle;
            
            copy.ShowBand3       = ShowBand3;
            copy.Band3Multiplier = Band3Multiplier;
            copy.Band3Color      = Band3Color;
            copy.Band3Opacity    = Band3Opacity;
            copy.Band3LineWidth  = Band3LineWidth;
            copy.Band3LineStyle  = Band3LineStyle;
        }
        #endregion

        #region Mouse Events
        public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, System.Windows.Point point)
        {
            if (DrawingState == DrawingState.Building) return Cursors.Cross;
            if (DrawingState == DrawingState.Moving)   return Cursors.SizeAll;

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
                    DrawingState = DrawingState.Normal;
                    IsSelected = false;
                    lastAvwapCalcTime = DateTime.MinValue;
                    lastAvwapBarCount = -1;
                }
            }
            else if (DrawingState == DrawingState.Normal && IsSelected)
            {
                System.Windows.Point mousePoint = dataPoint.GetPoint(chartControl, chartPanel, chartScale);

                ChartAnchor closest = GetClosestAnchor(chartControl, chartPanel, chartScale, mousePoint, 15);
                if (closest != null)
                {
                    closest.IsEditing = true;
                    DrawingState = DrawingState.Editing;
                }
                else if (IsPointNearDrawing(chartControl, chartPanel, chartScale, mousePoint))
                {
                    DrawingState = DrawingState.Moving;
                }
            }
        }

        public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Building && startAnchor.IsEditing)
            {
                dataPoint.CopyDataValues(startAnchor);
                lastAvwapCalcTime = DateTime.MinValue;
                lastAvwapBarCount = -1;
            }
            else if (DrawingState == DrawingState.Editing && startAnchor.IsEditing)
            {
                dataPoint.CopyDataValues(startAnchor);
                lastAvwapCalcTime = DateTime.MinValue;
                lastAvwapBarCount = -1;
            }
            else if (DrawingState == DrawingState.Moving)
            {
                dataPoint.CopyDataValues(startAnchor);
                lastAvwapCalcTime = DateTime.MinValue;
                lastAvwapBarCount = -1;
            }
        }

        public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Editing)
            {
                startAnchor.IsEditing = false;
                DrawingState = DrawingState.Normal;
                lastAvwapCalcTime = DateTime.MinValue;
                lastAvwapBarCount = -1;
            }
            else if (DrawingState == DrawingState.Moving)
            {
                DrawingState = DrawingState.Normal;
                lastAvwapCalcTime = DateTime.MinValue;
                lastAvwapBarCount = -1;
            }
        }

        private bool IsPointNearDrawing(ChartControl cc, ChartPanel cp, ChartScale cs, System.Windows.Point pt)
        {
            if (startAnchor == null || avwapPoints.Count == 0) return false;
            try
            {
                // Check proximity to anchor point
                System.Windows.Point anchorPt = startAnchor.GetPoint(cc, cp, cs);
                double anchorDist = Math.Sqrt(Math.Pow(pt.X - anchorPt.X, 2) + Math.Pow(pt.Y - anchorPt.Y, 2));
                if (anchorDist < 15) return true;

                // Check proximity to AVWAP line
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
                
                // Check proximity to band lines
                var allBands = new List<List<KeyValuePair<int, double>>>();
                if (ShowBand1) { allBands.Add(upperBand1Points); allBands.Add(lowerBand1Points); }
                if (ShowBand2) { allBands.Add(upperBand2Points); allBands.Add(lowerBand2Points); }
                if (ShowBand3) { allBands.Add(upperBand3Points); allBands.Add(lowerBand3Points); }
                
                foreach (var bandPoints in allBands)
                {
                    foreach (var kvp in bandPoints)
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
            }
            catch { }
            return false;
        }

        private ChartAnchor GetClosestAnchor(ChartControl cc, ChartPanel cp, ChartScale cs, System.Windows.Point point, double maxDist)
        {
            try
            {
                System.Windows.Point sp = startAnchor.GetPoint(cc, cp, cs);
                double d = Math.Sqrt(Math.Pow(point.X - sp.X, 2) + Math.Pow(point.Y - sp.Y, 2));
                if (d <= maxDist) return startAnchor;
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
            if (startAnchor == null) return new System.Windows.Point[0];
            ChartPanel cp = chartControl.ChartPanels[chartScale.PanelIndex];
            return new[] { startAnchor.GetPoint(chartControl, cp, chartScale) };
        }
        #endregion

        #region AVWAP Calculation
        private double GetSourcePrice(Bars bars, int index)
        {
            double high  = bars.GetHigh(index);
            double low   = bars.GetLow(index);
            double open  = bars.GetOpen(index);
            double close = bars.GetClose(index);
            
            switch (VWAPSource)
            {
                case RedTailAVWAPSource.HLC3:
                    return (high + low + close) / 3.0;
                case RedTailAVWAPSource.OHLC4:
                    return (open + high + low + close) / 4.0;
                case RedTailAVWAPSource.HL2:
                    return (high + low) / 2.0;
                case RedTailAVWAPSource.Close:
                    return close;
                default:
                    return (open + high + low + close) / 4.0;
            }
        }
        
        private void CalculateAVWAP(ChartControl chartControl, ChartBars chartBars)
        {
            if (chartControl == null || chartBars == null || startAnchor == null)
                return;

            Bars bars = chartBars.Bars;
            if (bars == null || bars.Count == 0) return;

            DateTime anchorTime = startAnchor.Time;

            // Recalculate if anchor moved or new bars arrived
            if (anchorTime == lastAvwapCalcTime && bars.Count == lastAvwapBarCount && avwapPoints.Count > 0)
                return;

            lastAvwapCalcTime = anchorTime;
            lastAvwapBarCount = bars.Count;
            avwapPoints.Clear();
            upperBand1Points.Clear(); lowerBand1Points.Clear();
            upperBand2Points.Clear(); lowerBand2Points.Clear();
            upperBand3Points.Clear(); lowerBand3Points.Clear();
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

            // Calculate AVWAP from anchor bar forward through all bars
            double cumVolume = 0;
            double cumTypicalVolume = 0;
            double cumSquaredDevVolume = 0;
            
            bool needBands = ShowBand1 || ShowBand2 || ShowBand3;

            for (int i = anchorIdx; i < bars.Count; i++)
            {
                double vol    = bars.GetVolume(i);
                double source = GetSourcePrice(bars, i);

                cumVolume += vol;
                cumTypicalVolume += source * vol;

                if (cumVolume > 0)
                {
                    double vwapValue = cumTypicalVolume / cumVolume;
                    avwapPoints.Add(new KeyValuePair<int, double>(i, vwapValue));
                    
                    // Standard deviation bands calculation
                    if (needBands)
                    {
                        double deviation = source - vwapValue;
                        cumSquaredDevVolume += deviation * deviation * vol;
                        double variance = cumSquaredDevVolume / cumVolume;
                        double stdDev = Math.Sqrt(variance);
                        
                        if (ShowBand1)
                        {
                            upperBand1Points.Add(new KeyValuePair<int, double>(i, vwapValue + stdDev * Band1Multiplier));
                            lowerBand1Points.Add(new KeyValuePair<int, double>(i, vwapValue - stdDev * Band1Multiplier));
                        }
                        if (ShowBand2)
                        {
                            upperBand2Points.Add(new KeyValuePair<int, double>(i, vwapValue + stdDev * Band2Multiplier));
                            lowerBand2Points.Add(new KeyValuePair<int, double>(i, vwapValue - stdDev * Band2Multiplier));
                        }
                        if (ShowBand3)
                        {
                            upperBand3Points.Add(new KeyValuePair<int, double>(i, vwapValue + stdDev * Band3Multiplier));
                            lowerBand3Points.Add(new KeyValuePair<int, double>(i, vwapValue - stdDev * Band3Multiplier));
                        }
                    }
                }
            }
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

            // Calculate AVWAP
            if (chartControl.BarsArray != null && chartControl.BarsArray.Count > 0 && chartControl.BarsArray[0] != null)
            {
                CalculateAVWAP(chartControl, chartControl.BarsArray[0]);
            }

            if (avwapPoints.Count < 2) return;

            // Get visible bar range for clipping
            int firstVisibleBar = (int)chartControl.GetSlotIndexByX((int)cp.X);
            int lastVisibleBar  = (int)chartControl.GetSlotIndexByX((int)(cp.X + cp.W));

            // Render standard deviation bands (behind main AVWAP line)
            if (ShowBand3 && upperBand3Points.Count > 1)
            {
                RenderLine(rt, chartControl, chartScale, cp, upperBand3Points, Band3Color, Band3Opacity, Band3LineWidth, Band3LineStyle, firstVisibleBar, lastVisibleBar);
                RenderLine(rt, chartControl, chartScale, cp, lowerBand3Points, Band3Color, Band3Opacity, Band3LineWidth, Band3LineStyle, firstVisibleBar, lastVisibleBar);
            }
            if (ShowBand2 && upperBand2Points.Count > 1)
            {
                RenderLine(rt, chartControl, chartScale, cp, upperBand2Points, Band2Color, Band2Opacity, Band2LineWidth, Band2LineStyle, firstVisibleBar, lastVisibleBar);
                RenderLine(rt, chartControl, chartScale, cp, lowerBand2Points, Band2Color, Band2Opacity, Band2LineWidth, Band2LineStyle, firstVisibleBar, lastVisibleBar);
            }
            if (ShowBand1 && upperBand1Points.Count > 1)
            {
                RenderLine(rt, chartControl, chartScale, cp, upperBand1Points, Band1Color, Band1Opacity, Band1LineWidth, Band1LineStyle, firstVisibleBar, lastVisibleBar);
                RenderLine(rt, chartControl, chartScale, cp, lowerBand1Points, Band1Color, Band1Opacity, Band1LineWidth, Band1LineStyle, firstVisibleBar, lastVisibleBar);
            }

            // Render main AVWAP line
            RenderLine(rt, chartControl, chartScale, cp, avwapPoints, AVWAPColor, AVWAPOpacity, AVWAPLineWidth, AVWAPLineStyle, firstVisibleBar, lastVisibleBar);

            // Render anchor marker
            RenderAnchorMarker(rt, chartControl, chartScale, cp);

            // Render label at the end of the AVWAP line
            if (ShowLabel && avwapPoints.Count > 0)
            {
                RenderLabel(rt, chartControl, chartScale, cp);
            }
        }

        private void RenderLine(SharpDX.Direct2D1.RenderTarget rt, ChartControl chartControl, ChartScale chartScale, ChartPanel cp,
            List<KeyValuePair<int, double>> points, System.Windows.Media.Brush colorBrush, int opacity, int lineWidth, DashStyleHelper lineStyle,
            int firstVisibleBar, int lastVisibleBar)
        {
            if (points == null || points.Count < 2) return;

            byte alpha = (byte)(255 * opacity / 100);
            System.Windows.Media.Color mediaColor = ((System.Windows.Media.SolidColorBrush)colorBrush).Color;
            SharpDX.Color dxColor = new SharpDX.Color((byte)mediaColor.R, (byte)mediaColor.G, (byte)mediaColor.B, (byte)alpha);

            using (var brush = new SharpDX.Direct2D1.SolidColorBrush(rt, dxColor))
            {
                var strokeStyle = lineStyle == DashStyleHelper.Solid ? null :
                    new SharpDX.Direct2D1.StrokeStyle(rt.Factory, new SharpDX.Direct2D1.StrokeStyleProperties
                    {
                        DashStyle = lineStyle == DashStyleHelper.Dash ? SharpDX.Direct2D1.DashStyle.Dash :
                                    lineStyle == DashStyleHelper.DashDot ? SharpDX.Direct2D1.DashStyle.DashDot :
                                    lineStyle == DashStyleHelper.DashDotDot ? SharpDX.Direct2D1.DashStyle.DashDotDot :
                                    lineStyle == DashStyleHelper.Dot ? SharpDX.Direct2D1.DashStyle.Dot :
                                    SharpDX.Direct2D1.DashStyle.Solid
                    });

                try
                {
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        int barIdx1 = points[i].Key;
                        int barIdx2 = points[i + 1].Key;

                        // Skip segments fully outside visible range (with padding)
                        if (barIdx2 < firstVisibleBar - 5 || barIdx1 > lastVisibleBar + 5) continue;

                        float x1 = chartControl.GetXByBarIndex(chartControl.BarsArray[0], barIdx1);
                        float y1 = chartScale.GetYByValue(points[i].Value);
                        float x2 = chartControl.GetXByBarIndex(chartControl.BarsArray[0], barIdx2);
                        float y2 = chartScale.GetYByValue(points[i + 1].Value);

                        if (float.IsNaN(x1) || float.IsNaN(y1) || float.IsNaN(x2) || float.IsNaN(y2)) continue;
                        if (float.IsInfinity(x1) || float.IsInfinity(y1) || float.IsInfinity(x2) || float.IsInfinity(y2)) continue;

                        rt.DrawLine(
                            new SharpDX.Vector2(x1, y1),
                            new SharpDX.Vector2(x2, y2),
                            brush, lineWidth, strokeStyle);
                    }
                }
                finally
                {
                    if (strokeStyle != null) strokeStyle.Dispose();
                }
            }
        }

        private void RenderAnchorMarker(SharpDX.Direct2D1.RenderTarget rt, ChartControl chartControl, ChartScale chartScale, ChartPanel cp)
        {
            if (avwapAnchorBarIdx < 0 || avwapPoints.Count == 0) return;

            try
            {
                float ax = chartControl.GetXByBarIndex(chartControl.BarsArray[0], avwapAnchorBarIdx);
                float ay = chartScale.GetYByValue(avwapPoints[0].Value);

                if (float.IsNaN(ax) || float.IsNaN(ay)) return;

                System.Windows.Media.Color mediaColor = ((System.Windows.Media.SolidColorBrush)AVWAPColor).Color;
                byte alpha = (byte)(255 * AVWAPOpacity / 100);
                SharpDX.Color dxColor = new SharpDX.Color((byte)mediaColor.R, (byte)mediaColor.G, (byte)mediaColor.B, (byte)alpha);

                using (var brush = new SharpDX.Direct2D1.SolidColorBrush(rt, dxColor))
                {
                    // Draw diamond anchor marker
                    float size = 5f;
                    var geo = new SharpDX.Direct2D1.PathGeometry(rt.Factory);
                    var sink = geo.Open();
                    sink.BeginFigure(new SharpDX.Vector2(ax, ay - size), SharpDX.Direct2D1.FigureBegin.Filled);
                    sink.AddLine(new SharpDX.Vector2(ax + size, ay));
                    sink.AddLine(new SharpDX.Vector2(ax, ay + size));
                    sink.AddLine(new SharpDX.Vector2(ax - size, ay));
                    sink.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
                    sink.Close();

                    rt.FillGeometry(geo, brush);
                    rt.DrawGeometry(geo, brush, 1f);
                    geo.Dispose();
                }
            }
            catch { }
        }

        private void RenderLabel(SharpDX.Direct2D1.RenderTarget rt, ChartControl chartControl, ChartScale chartScale, ChartPanel cp)
        {
            if (avwapPoints.Count == 0) return;

            try
            {
                var lastPoint = avwapPoints[avwapPoints.Count - 1];
                float lx = chartControl.GetXByBarIndex(chartControl.BarsArray[0], lastPoint.Key);
                float ly = chartScale.GetYByValue(lastPoint.Value);

                if (float.IsNaN(lx) || float.IsNaN(ly)) return;

                System.Windows.Media.Color mediaColor = ((System.Windows.Media.SolidColorBrush)AVWAPColor).Color;
                byte alpha = (byte)(255 * AVWAPOpacity / 100);
                SharpDX.Color dxColor = new SharpDX.Color((byte)mediaColor.R, (byte)mediaColor.G, (byte)mediaColor.B, (byte)alpha);

                string labelText = "AVWAP " + lastPoint.Value.ToString("F2");

                using (var textFormat = new SharpDX.DirectWrite.TextFormat(
                    NinjaTrader.Core.Globals.DirectWriteFactory, "Arial", LabelFontSize))
                using (var textLayout = new SharpDX.DirectWrite.TextLayout(
                    NinjaTrader.Core.Globals.DirectWriteFactory, labelText, textFormat, 200, 20))
                using (var bgBrush = new SharpDX.Direct2D1.SolidColorBrush(rt, new SharpDX.Color((byte)0, (byte)0, (byte)0, (byte)(alpha * 0.6))))
                using (var textBrush = new SharpDX.Direct2D1.SolidColorBrush(rt, dxColor))
                {
                    float textX = lx + 8;
                    float textY = ly - textLayout.Metrics.Height / 2;

                    // Background
                    var bgRect = new SharpDX.RectangleF(textX - 3, textY - 2,
                        textLayout.Metrics.Width + 6, textLayout.Metrics.Height + 4);
                    rt.FillRectangle(bgRect, bgBrush);

                    // Text
                    rt.DrawTextLayout(new SharpDX.Vector2(textX, textY), textLayout, textBrush);
                }
            }
            catch { }
        }
        #endregion

        #region Properties

        // ===== AVWAP Line Settings =====
        [Display(Name = "VWAP Color", GroupName = "1. AVWAP Line", Order = 1)]
        [XmlIgnore]
        public System.Windows.Media.Brush AVWAPColor { get; set; }
        [Browsable(false)]
        public string AVWAPColorSerialize
        {
            get { return Serialize.BrushToString(AVWAPColor); }
            set { AVWAPColor = Serialize.StringToBrush(value); }
        }

        [Display(Name = "Line Width", GroupName = "1. AVWAP Line", Order = 2)]
        [Range(1, 10)]
        public int AVWAPLineWidth { get; set; }

        [Display(Name = "Line Style", GroupName = "1. AVWAP Line", Order = 3)]
        public DashStyleHelper AVWAPLineStyle { get; set; }

        [Display(Name = "Opacity", GroupName = "1. AVWAP Line", Order = 4)]
        [Range(1, 100)]
        public int AVWAPOpacity { get; set; }

        [Display(Name = "Show Label", GroupName = "1. AVWAP Line", Order = 5)]
        public bool ShowLabel { get; set; }

        [Display(Name = "Label Font Size", GroupName = "1. AVWAP Line", Order = 6)]
        [Range(6, 24)]
        public int LabelFontSize { get; set; }
        
        [Display(Name = "VWAP Source", GroupName = "1. AVWAP Line", Order = 7)]
        public RedTailAVWAPSource VWAPSource { get; set; }

        // ===== Band 1 Settings =====
        [Display(Name = "Show Band 1", GroupName = "2. Std Dev Band 1", Order = 1)]
        public bool ShowBand1 { get; set; }

        [Display(Name = "Multiplier", GroupName = "2. Std Dev Band 1", Order = 2)]
        [Range(0.1, 10.0)]
        public double Band1Multiplier { get; set; }

        [Display(Name = "Color", GroupName = "2. Std Dev Band 1", Order = 3)]
        [XmlIgnore]
        public System.Windows.Media.Brush Band1Color { get; set; }
        [Browsable(false)]
        public string Band1ColorSerialize
        {
            get { return Serialize.BrushToString(Band1Color); }
            set { Band1Color = Serialize.StringToBrush(value); }
        }

        [Display(Name = "Opacity", GroupName = "2. Std Dev Band 1", Order = 4)]
        [Range(1, 100)]
        public int Band1Opacity { get; set; }

        [Display(Name = "Line Width", GroupName = "2. Std Dev Band 1", Order = 5)]
        [Range(1, 10)]
        public int Band1LineWidth { get; set; }

        [Display(Name = "Line Style", GroupName = "2. Std Dev Band 1", Order = 6)]
        public DashStyleHelper Band1LineStyle { get; set; }

        // ===== Band 2 Settings =====
        [Display(Name = "Show Band 2", GroupName = "3. Std Dev Band 2", Order = 1)]
        public bool ShowBand2 { get; set; }

        [Display(Name = "Multiplier", GroupName = "3. Std Dev Band 2", Order = 2)]
        [Range(0.1, 10.0)]
        public double Band2Multiplier { get; set; }

        [Display(Name = "Color", GroupName = "3. Std Dev Band 2", Order = 3)]
        [XmlIgnore]
        public System.Windows.Media.Brush Band2Color { get; set; }
        [Browsable(false)]
        public string Band2ColorSerialize
        {
            get { return Serialize.BrushToString(Band2Color); }
            set { Band2Color = Serialize.StringToBrush(value); }
        }

        [Display(Name = "Opacity", GroupName = "3. Std Dev Band 2", Order = 4)]
        [Range(1, 100)]
        public int Band2Opacity { get; set; }

        [Display(Name = "Line Width", GroupName = "3. Std Dev Band 2", Order = 5)]
        [Range(1, 10)]
        public int Band2LineWidth { get; set; }

        [Display(Name = "Line Style", GroupName = "3. Std Dev Band 2", Order = 6)]
        public DashStyleHelper Band2LineStyle { get; set; }

        // ===== Band 3 Settings =====
        [Display(Name = "Show Band 3", GroupName = "4. Std Dev Band 3", Order = 1)]
        public bool ShowBand3 { get; set; }

        [Display(Name = "Multiplier", GroupName = "4. Std Dev Band 3", Order = 2)]
        [Range(0.1, 10.0)]
        public double Band3Multiplier { get; set; }

        [Display(Name = "Color", GroupName = "4. Std Dev Band 3", Order = 3)]
        [XmlIgnore]
        public System.Windows.Media.Brush Band3Color { get; set; }
        [Browsable(false)]
        public string Band3ColorSerialize
        {
            get { return Serialize.BrushToString(Band3Color); }
            set { Band3Color = Serialize.StringToBrush(value); }
        }

        [Display(Name = "Opacity", GroupName = "4. Std Dev Band 3", Order = 4)]
        [Range(1, 100)]
        public int Band3Opacity { get; set; }

        [Display(Name = "Line Width", GroupName = "4. Std Dev Band 3", Order = 5)]
        [Range(1, 10)]
        public int Band3LineWidth { get; set; }

        [Display(Name = "Line Style", GroupName = "4. Std Dev Band 3", Order = 6)]
        public DashStyleHelper Band3LineStyle { get; set; }

        #endregion
    }
    
    // Enum at namespace level for proper XML serialization
    public enum RedTailAVWAPSource
    {
        OHLC4,
        HLC3,
        HL2,
        Close
    }
}