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

namespace NinjaTrader.NinjaScript.DrawingTools
{
    public class RedTailMTFFib : DrawingTool
    {
        #region Enums
        public enum FibTimeframe
        {
            Monthly, Weekly, Daily, H4, H1, M30, M15, M5, M1
        }
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

        #region Level Helper
        private struct LevelInfo
        {
            public double Ratio;
            public System.Windows.Media.Brush Color;
        }

        private List<LevelInfo> GetActiveLevels()
        {
            var list = new List<LevelInfo>();
            double[] vals = { Level1, Level2, Level3, Level4, Level5, Level6, Level7, Level8, Level9, Level10 };
            System.Windows.Media.Brush[] cols = { Level1Color, Level2Color, Level3Color, Level4Color, Level5Color,
                                                   Level6Color, Level7Color, Level8Color, Level9Color, Level10Color };
            for (int i = 0; i < 10; i++)
            {
                if (vals[i] >= 0)
                    list.Add(new LevelInfo { Ratio = vals[i] / 100.0, Color = cols[i] ?? Brushes.DodgerBlue });
            }
            return list;
        }
        #endregion

        #region State Management
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name            = "RedTailMTFFib";
                Description     = "RedTail MTF Fibonacci with timeframe labels and per-level colors. Set level to -1 to disable.";
                DrawingState    = DrawingState.Building;

                SelectedTimeframe   = FibTimeframe.Daily;
                LabelFontSize       = 11;
                ShowPriceOnLabel    = true;
                ExtendLinesRight    = true;
                FibLineWidth        = 1;
                FibLineDashStyle    = DashStyleHelper.Solid;
                FibOpacity          = 100;
                AnchorLineColor     = Brushes.White;
                AnchorLineWidth     = 2;

                Level1  = 0;       Level1Color = Brushes.Gray;
                Level2  = 23.6;    Level2Color = Brushes.DodgerBlue;
                Level3  = 38.2;    Level3Color = Brushes.DodgerBlue;
                Level4  = 50;      Level4Color = Brushes.Gold;
                Level5  = 61.8;    Level5Color = Brushes.Red;
                Level6  = 78.6;    Level6Color = Brushes.OrangeRed;
                Level7  = 100;     Level7Color = Brushes.Gray;
                Level8  = -1;      Level8Color = Brushes.Cyan;
                Level9  = -1;      Level9Color = Brushes.Magenta;
                Level10 = -1;      Level10Color = Brushes.LimeGreen;

                startAnchor = new ChartAnchor();
                endAnchor   = new ChartAnchor();
            }
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
                dataPoint.CopyDataValues(endAnchor);
            else if (DrawingState == DrawingState.Editing && editingAnchor != null)
                dataPoint.CopyDataValues(editingAnchor);
            else if (DrawingState == DrawingState.Moving && isMoving && editingAnchor != null)
            {
                double deltaPrice  = dataPoint.Price - editingAnchor.Price;
                TimeSpan deltaTime = dataPoint.Time  - editingAnchor.Time;
                startAnchor.Price  = moveStartPrice + deltaPrice;
                startAnchor.Time   = moveStartTime  + deltaTime;
                endAnchor.Price    = moveEndPrice   + deltaPrice;
                endAnchor.Time     = moveEndTime    + deltaTime;
            }
        }

        public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Editing)
            {
                if (editingAnchor != null) editingAnchor.IsEditing = false;
                editingAnchor = null;
                DrawingState  = DrawingState.Normal;
            }
            else if (DrawingState == DrawingState.Moving)
            {
                isMoving      = false;
                editingAnchor = null;
                DrawingState  = DrawingState.Normal;
            }
        }

        private bool IsPointNearDrawing(ChartControl cc, ChartPanel cp, ChartScale cs, System.Windows.Point pt)
        {
            if (startAnchor == null || endAnchor == null) return false;
            try
            {
                System.Windows.Point sp = startAnchor.GetPoint(cc, cp, cs);
                System.Windows.Point ep = endAnchor.GetPoint(cc, cp, cs);

                // Near diagonal anchor line
                double dx = ep.X - sp.X, dy = ep.Y - sp.Y;
                double lenSq = dx * dx + dy * dy;
                if (lenSq < 0.001)
                    return Math.Sqrt(Math.Pow(pt.X - sp.X, 2) + Math.Pow(pt.Y - sp.Y, 2)) < 15;
                double t = Math.Max(0, Math.Min(1, ((pt.X - sp.X) * dx + (pt.Y - sp.Y) * dy) / lenSq));
                double projX = sp.X + t * dx, projY = sp.Y + t * dy;
                if (Math.Sqrt(Math.Pow(pt.X - projX, 2) + Math.Pow(pt.Y - projY, 2)) < 15)
                    return true;

                // Near any horizontal fib level
                double startPrice = startAnchor.Price;
                double endPrice   = endAnchor.Price;
                double range      = startPrice - endPrice;
                if (Math.Abs(range) < double.Epsilon) return false;

                float xMin = (float)Math.Min(sp.X, ep.X) - 15;
                float xMax = ExtendLinesRight ? (float)cp.W : (float)Math.Max(sp.X, ep.X) + 15;

                foreach (var lv in GetActiveLevels())
                {
                    double price = endPrice + range * lv.Ratio;
                    float y = cs.GetYByValue(price);
                    if (pt.X >= xMin && pt.X <= xMax && Math.Abs(pt.Y - y) < 15)
                        return true;
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

            double startPrice = startAnchor.Price;
            double endPrice   = endAnchor.Price;
            double range      = startPrice - endPrice;
            if (Math.Abs(range) < double.Epsilon) return;

            float xS = (float)Math.Min(startPt.X, endPt.X);
            float xE = ExtendLinesRight ? (float)cp.W : (float)Math.Max(startPt.X, endPt.X);

            string tfLabel = GetTFLabel(SelectedTimeframe);
            float opacity  = FibOpacity / 100f;

            // All DX resources created per-render inside using blocks — no class-level caching
            using (var bgBr = new SharpDX.Direct2D1.SolidColorBrush(rt, new SharpDX.Color4(0.05f, 0.05f, 0.1f, 0.8f)))
            using (var fmt = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Consolas",
                SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, (float)LabelFontSize))
            {
                foreach (var lv in GetActiveLevels())
                {
                    double price = endPrice + range * lv.Ratio;
                    float y = chartScale.GetYByValue(price);
                    SharpDX.Color4 lvColor = BrushToColor4(lv.Color, opacity);

                    // Level line
                    using (var lineBr = new SharpDX.Direct2D1.SolidColorBrush(rt, lvColor))
                    {
                        if (FibLineDashStyle == DashStyleHelper.Solid)
                        {
                            rt.DrawLine(new SharpDX.Vector2(xS, y), new SharpDX.Vector2(xE, y), lineBr, FibLineWidth);
                        }
                        else
                        {
                            using (var ss = CreateStrokeStyle(rt))
                                rt.DrawLine(new SharpDX.Vector2(xS, y), new SharpDX.Vector2(xE, y), lineBr, FibLineWidth, ss);
                        }
                    }

                    // Label
                    try
                    {
                        string pctText = (lv.Ratio * 100).ToString("F1");
                        string labelText = ShowPriceOnLabel
                            ? tfLabel + " " + pctText + "  [" + price.ToString("F2") + "]"
                            : tfLabel + " " + pctText;

                        using (var tl = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, labelText, fmt, 400, 20))
                        {
                            float tw = tl.Metrics.Width, th = tl.Metrics.Height;
                            float lx = xE - tw - 8, ly = y - th - 2;
                            rt.FillRoundedRectangle(new SharpDX.Direct2D1.RoundedRectangle
                                { Rect = new SharpDX.RectangleF(lx - 2, ly, tw + 8, th + 4), RadiusX = 2, RadiusY = 2 }, bgBr);
                            using (var txtBr = new SharpDX.Direct2D1.SolidColorBrush(rt, lvColor))
                                rt.DrawTextLayout(new SharpDX.Vector2(lx + 2, ly + 1), tl, txtBr);
                        }
                    }
                    catch { }
                }
            }

            // Anchor diagonal line
            SharpDX.Color4 anchorC4 = BrushToColor4(AnchorLineColor, opacity);
            using (var dimBr = new SharpDX.Direct2D1.SolidColorBrush(rt, anchorC4))
            using (var ds = new SharpDX.Direct2D1.StrokeStyle(rt.Factory,
                new SharpDX.Direct2D1.StrokeStyleProperties { DashStyle = SharpDX.Direct2D1.DashStyle.Dash }))
                rt.DrawLine(new SharpDX.Vector2((float)startPt.X, (float)startPt.Y),
                            new SharpDX.Vector2((float)endPt.X, (float)endPt.Y), dimBr, AnchorLineWidth, ds);

            // Selection handles
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
        private SharpDX.Color4 BrushToColor4(System.Windows.Media.Brush mb, float opacity)
        {
            if (mb is System.Windows.Media.SolidColorBrush scb)
            {
                System.Windows.Media.Color c = scb.Color;
                return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, opacity);
            }
            return new SharpDX.Color4(1f, 1f, 1f, opacity);
        }

        private SharpDX.Direct2D1.StrokeStyle CreateStrokeStyle(SharpDX.Direct2D1.RenderTarget rt)
        {
            // Use custom dash patterns for reliable rendering at all widths
            float[] dashes;
            switch (FibLineDashStyle)
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

        private string GetTFLabel(FibTimeframe tf)
        {
            switch (tf)
            {
                case FibTimeframe.Monthly: return "M";   case FibTimeframe.Weekly: return "W";
                case FibTimeframe.Daily:   return "D";   case FibTimeframe.H4:     return "H4";
                case FibTimeframe.H1:      return "H1";  case FibTimeframe.M30:    return "M30";
                case FibTimeframe.M15:     return "M15"; case FibTimeframe.M5:     return "M5";
                case FibTimeframe.M1:      return "M1";  default:                   return "?";
            }
        }
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Timeframe", Order = 1)]
        public FibTimeframe SelectedTimeframe { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Label Font Size", Order = 2)]
        [Range(8, 20)]
        public int LabelFontSize { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Price on Label", Order = 3)]
        public bool ShowPriceOnLabel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Extend Lines Right", Order = 4)]
        public bool ExtendLinesRight { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Line Width", Order = 5)]
        [Range(1, 5)]
        public int FibLineWidth { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Line Style", Order = 6)]
        public DashStyleHelper FibLineDashStyle { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Opacity %", Order = 7)]
        [Range(10, 100)]
        public int FibOpacity { get; set; }

        [XmlIgnore][Display(Name = "Anchor Line Color", Order = 8)]
        public System.Windows.Media.Brush AnchorLineColor { get; set; }
        [Browsable(false)] public string AnchorLineColorSerialize { get { return Serialize.BrushToString(AnchorLineColor); } set { AnchorLineColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Anchor Line Width", Order = 9)]
        [Range(1, 5)]
        public int AnchorLineWidth { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Level 1 (%)", Description = "-1 to disable", Order = 10)]
        public double Level1 { get; set; }
        [XmlIgnore][Display(Name = "Level 1 Color", Order = 11)]
        public System.Windows.Media.Brush Level1Color { get; set; }
        [Browsable(false)] public string Level1ColorSerialize { get { return Serialize.BrushToString(Level1Color); } set { Level1Color = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Level 2 (%)", Description = "-1 to disable", Order = 12)]
        public double Level2 { get; set; }
        [XmlIgnore][Display(Name = "Level 2 Color", Order = 13)]
        public System.Windows.Media.Brush Level2Color { get; set; }
        [Browsable(false)] public string Level2ColorSerialize { get { return Serialize.BrushToString(Level2Color); } set { Level2Color = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Level 3 (%)", Description = "-1 to disable", Order = 14)]
        public double Level3 { get; set; }
        [XmlIgnore][Display(Name = "Level 3 Color", Order = 15)]
        public System.Windows.Media.Brush Level3Color { get; set; }
        [Browsable(false)] public string Level3ColorSerialize { get { return Serialize.BrushToString(Level3Color); } set { Level3Color = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Level 4 (%)", Description = "-1 to disable", Order = 16)]
        public double Level4 { get; set; }
        [XmlIgnore][Display(Name = "Level 4 Color", Order = 17)]
        public System.Windows.Media.Brush Level4Color { get; set; }
        [Browsable(false)] public string Level4ColorSerialize { get { return Serialize.BrushToString(Level4Color); } set { Level4Color = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Level 5 (%)", Description = "-1 to disable", Order = 18)]
        public double Level5 { get; set; }
        [XmlIgnore][Display(Name = "Level 5 Color", Order = 19)]
        public System.Windows.Media.Brush Level5Color { get; set; }
        [Browsable(false)] public string Level5ColorSerialize { get { return Serialize.BrushToString(Level5Color); } set { Level5Color = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Level 6 (%)", Description = "-1 to disable", Order = 20)]
        public double Level6 { get; set; }
        [XmlIgnore][Display(Name = "Level 6 Color", Order = 21)]
        public System.Windows.Media.Brush Level6Color { get; set; }
        [Browsable(false)] public string Level6ColorSerialize { get { return Serialize.BrushToString(Level6Color); } set { Level6Color = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Level 7 (%)", Description = "-1 to disable", Order = 22)]
        public double Level7 { get; set; }
        [XmlIgnore][Display(Name = "Level 7 Color", Order = 23)]
        public System.Windows.Media.Brush Level7Color { get; set; }
        [Browsable(false)] public string Level7ColorSerialize { get { return Serialize.BrushToString(Level7Color); } set { Level7Color = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Level 8 (%)", Description = "-1 to disable", Order = 24)]
        public double Level8 { get; set; }
        [XmlIgnore][Display(Name = "Level 8 Color", Order = 25)]
        public System.Windows.Media.Brush Level8Color { get; set; }
        [Browsable(false)] public string Level8ColorSerialize { get { return Serialize.BrushToString(Level8Color); } set { Level8Color = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Level 9 (%)", Description = "-1 to disable", Order = 26)]
        public double Level9 { get; set; }
        [XmlIgnore][Display(Name = "Level 9 Color", Order = 27)]
        public System.Windows.Media.Brush Level9Color { get; set; }
        [Browsable(false)] public string Level9ColorSerialize { get { return Serialize.BrushToString(Level9Color); } set { Level9Color = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Level 10 (%)", Description = "-1 to disable", Order = 28)]
        public double Level10 { get; set; }
        [XmlIgnore][Display(Name = "Level 10 Color", Order = 29)]
        public System.Windows.Media.Brush Level10Color { get; set; }
        [Browsable(false)] public string Level10ColorSerialize { get { return Serialize.BrushToString(Level10Color); } set { Level10Color = Serialize.StringToBrush(value); } }
        #endregion
    }
}
