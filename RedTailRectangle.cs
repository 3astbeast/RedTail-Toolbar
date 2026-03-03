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
using SharpDX;
using SharpDX.Direct2D1;
#endregion

namespace NinjaTrader.NinjaScript.DrawingTools
{
    [CLSCompliant(false)]
    public class RedTailRectangle : DrawingTool
    {
        #region Enums
        public enum RectFillMode
        {
            Solid,
            Gradient,
            None
        }

        public enum LabelPosition
        {
            TopLeft,
            TopCenter,
            TopRight,
            MiddleLeft,
            MiddleCenter,
            MiddleRight,
            BottomLeft,
            BottomCenter,
            BottomRight,
            None
        }
        #endregion

        #region Variables
        private ChartAnchor startAnchor;
        private ChartAnchor endAnchor;
        private SharpDX.Direct2D1.Brush borderBrushDx;
        private SharpDX.Direct2D1.Brush midLineBrushDx;
        private SharpDX.Direct2D1.Brush fillBrushDx;
        private SharpDX.Direct2D1.Brush textBrushDx;
        private SharpDX.Direct2D1.Brush extLineBrushDx;
        private SharpDX.DirectWrite.TextFormat textFormatDx;
        #endregion

        #region Properties

        // --- Border ---
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Border Color", Description = "Rectangle border color", Order = 1, GroupName = "1. Border")]
        public System.Windows.Media.Brush BorderBrush { get; set; }

        [Browsable(false)]
        public string BorderBrushSerialize
        {
            get { return Serialize.BrushToString(BorderBrush); }
            set { BorderBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Border Width", Description = "Rectangle border width", Order = 2, GroupName = "1. Border")]
        public int BorderWidth { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Border Opacity", Description = "Border opacity (1-100)", Order = 3, GroupName = "1. Border")]
        public int BorderOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Border Dash Style", Description = "Border line dash style", Order = 4, GroupName = "1. Border")]
        public DashStyleHelper BorderDashStyle { get; set; }

        // --- Mid Line ---
        [NinjaScriptProperty]
        [Display(Name = "Show Mid Line", Description = "Show the mid line", Order = 1, GroupName = "2. Mid Line")]
        public bool ShowMidLine { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Mid Line Color", Description = "Mid line color", Order = 2, GroupName = "2. Mid Line")]
        public System.Windows.Media.Brush MidLineBrush { get; set; }

        [Browsable(false)]
        public string MidLineBrushSerialize
        {
            get { return Serialize.BrushToString(MidLineBrush); }
            set { MidLineBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Mid Line Width", Description = "Mid line width", Order = 3, GroupName = "2. Mid Line")]
        public int MidLineWidth { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Mid Line Opacity", Description = "Mid line opacity (1-100)", Order = 4, GroupName = "2. Mid Line")]
        public int MidLineOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mid Line Dash Style", Description = "Mid line dash style", Order = 5, GroupName = "2. Mid Line")]
        public DashStyleHelper MidLineDashStyle { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Extend Mid Line Right", Description = "Extend the mid line to the right edge of the chart", Order = 6, GroupName = "2. Mid Line")]
        public bool ExtendMidLineRight { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Extend Mid Line Left", Description = "Extend the mid line to the left edge of the chart", Order = 7, GroupName = "2. Mid Line")]
        public bool ExtendMidLineLeft { get; set; }

        // --- Extension Lines ---
        [NinjaScriptProperty]
        [Display(Name = "Extend Top Right", Description = "Extend top border line right", Order = 1, GroupName = "3. Extension Lines")]
        public bool ExtendTopRight { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Extend Top Left", Description = "Extend top border line left", Order = 2, GroupName = "3. Extension Lines")]
        public bool ExtendTopLeft { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Extend Bottom Right", Description = "Extend bottom border line right", Order = 3, GroupName = "3. Extension Lines")]
        public bool ExtendBottomRight { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Extend Bottom Left", Description = "Extend bottom border line left", Order = 4, GroupName = "3. Extension Lines")]
        public bool ExtendBottomLeft { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Extension Line Color", Description = "Color of extension lines", Order = 5, GroupName = "3. Extension Lines")]
        public System.Windows.Media.Brush ExtensionLineBrush { get; set; }

        [Browsable(false)]
        public string ExtensionLineBrushSerialize
        {
            get { return Serialize.BrushToString(ExtensionLineBrush); }
            set { ExtensionLineBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Extension Line Width", Description = "Width of extension lines", Order = 6, GroupName = "3. Extension Lines")]
        public int ExtensionLineWidth { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Extension Line Dash Style", Description = "Extension line dash style", Order = 7, GroupName = "3. Extension Lines")]
        public DashStyleHelper ExtensionLineDashStyle { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Extension Line Opacity", Description = "Extension line opacity (1-100)", Order = 8, GroupName = "3. Extension Lines")]
        public int ExtensionLineOpacity { get; set; }

        // --- Fill ---
        [NinjaScriptProperty]
        [Display(Name = "Fill Mode", Description = "Fill mode for the rectangle", Order = 1, GroupName = "4. Fill")]
        public RectFillMode FillMode { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Fill Color", Description = "Rectangle fill color", Order = 2, GroupName = "4. Fill")]
        public System.Windows.Media.Brush FillBrush { get; set; }

        [Browsable(false)]
        public string FillBrushSerialize
        {
            get { return Serialize.BrushToString(FillBrush); }
            set { FillBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Fill Opacity", Description = "Fill opacity (1-100)", Order = 3, GroupName = "4. Fill")]
        public int FillOpacity { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Gradient Color 2", Description = "Second color for gradient fill", Order = 4, GroupName = "4. Fill")]
        public System.Windows.Media.Brush FillBrush2 { get; set; }

        [Browsable(false)]
        public string FillBrush2Serialize
        {
            get { return Serialize.BrushToString(FillBrush2); }
            set { FillBrush2 = Serialize.StringToBrush(value); }
        }

        // --- Label ---
        [NinjaScriptProperty]
        [Display(Name = "Show Label", Description = "Show a text label", Order = 1, GroupName = "5. Label")]
        public bool ShowLabel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Label Text", Description = "Custom label text", Order = 2, GroupName = "5. Label")]
        public string LabelText { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Label Position", Description = "Position of the label", Order = 3, GroupName = "5. Label")]
        public LabelPosition LabelPos { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Label Color", Description = "Label text color", Order = 4, GroupName = "5. Label")]
        public System.Windows.Media.Brush LabelBrush { get; set; }

        [Browsable(false)]
        public string LabelBrushSerialize
        {
            get { return Serialize.BrushToString(LabelBrush); }
            set { LabelBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(8, 36)]
        [Display(Name = "Label Font Size", Description = "Label font size", Order = 5, GroupName = "5. Label")]
        public int LabelFontSize { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Price Range", Description = "Show price range (height) in label", Order = 6, GroupName = "5. Label")]
        public bool ShowPriceRange { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Mid Price", Description = "Show mid price in label", Order = 7, GroupName = "5. Label")]
        public bool ShowMidPrice { get; set; }

        // --- Anchors ---
        [Display(Name = "Start Anchor", Order = 1, GroupName = "Anchors")]
        public ChartAnchor StartAnchor
        {
            get { return startAnchor; }
            set { startAnchor = value; }
        }

        [Display(Name = "End Anchor", Order = 2, GroupName = "Anchors")]
        public ChartAnchor EndAnchor
        {
            get { return endAnchor; }
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
                Name            = "RedTailRectangle";
                Description     = "Rectangle with mid line and full styling options by RedTail";
                DrawingState    = DrawingState.Building;
                IsAutoScale     = false;

                // Border defaults
                BorderBrush     = System.Windows.Media.Brushes.DodgerBlue;
                BorderWidth     = 2;
                BorderOpacity   = 100;
                BorderDashStyle = DashStyleHelper.Solid;

                // Mid Line defaults
                ShowMidLine         = true;
                MidLineBrush        = System.Windows.Media.Brushes.Yellow;
                MidLineWidth        = 1;
                MidLineOpacity      = 100;
                MidLineDashStyle    = DashStyleHelper.Dash;
                ExtendMidLineRight  = false;
                ExtendMidLineLeft   = false;

                // Extension Lines defaults
                ExtendTopRight          = false;
                ExtendTopLeft           = false;
                ExtendBottomRight       = false;
                ExtendBottomLeft        = false;
                ExtensionLineBrush      = System.Windows.Media.Brushes.DodgerBlue;
                ExtensionLineWidth      = 1;
                ExtensionLineDashStyle  = DashStyleHelper.Dot;
                ExtensionLineOpacity    = 60;

                // Fill defaults
                FillMode    = RectFillMode.Solid;
                FillBrush   = System.Windows.Media.Brushes.DodgerBlue;
                FillOpacity = 15;
                FillBrush2  = System.Windows.Media.Brushes.Transparent;

                // Label defaults
                ShowLabel       = false;
                LabelText       = "";
                LabelPos        = LabelPosition.TopLeft;
                LabelBrush      = System.Windows.Media.Brushes.White;
                LabelFontSize   = 12;
                ShowPriceRange  = false;
                ShowMidPrice    = false;

                startAnchor = new ChartAnchor
                {
                    IsEditing   = true,
                    DrawingTool = this
                };
                endAnchor = new ChartAnchor
                {
                    IsEditing   = true,
                    DrawingTool = this
                };
            }
            else if (State == State.Terminated)
            {
                DisposeResources();
            }
        }

        private void DisposeResources()
        {
            if (borderBrushDx != null)  { borderBrushDx.Dispose();  borderBrushDx = null; }
            if (midLineBrushDx != null)  { midLineBrushDx.Dispose();  midLineBrushDx = null; }
            if (fillBrushDx != null)     { fillBrushDx.Dispose();     fillBrushDx = null; }
            if (textBrushDx != null)     { textBrushDx.Dispose();     textBrushDx = null; }
            if (extLineBrushDx != null)  { extLineBrushDx.Dispose();  extLineBrushDx = null; }
            if (textFormatDx != null)    { textFormatDx.Dispose();    textFormatDx = null; }
        }
        #endregion

        #region Helper Methods
        private SharpDX.Direct2D1.Brush CreateDxBrush(RenderTarget rt, System.Windows.Media.Brush wpfBrush, int opacity)
        {
            if (wpfBrush == null) return null;
            System.Windows.Media.SolidColorBrush scb = wpfBrush as System.Windows.Media.SolidColorBrush;
            if (scb == null) return null;
            System.Windows.Media.Color c = scb.Color;
            float a = (float)(opacity / 100.0) * (c.A / 255.0f);
            return new SharpDX.Direct2D1.SolidColorBrush(rt, new SharpDX.Color(c.R, c.G, c.B, (byte)(a * 255)));
        }

        private SharpDX.Direct2D1.StrokeStyle CreateStrokeStyle(RenderTarget rt, DashStyleHelper dashStyle)
        {
            SharpDX.Direct2D1.DashStyle ds;
            switch (dashStyle)
            {
                case DashStyleHelper.Dash:          ds = SharpDX.Direct2D1.DashStyle.Dash; break;
                case DashStyleHelper.DashDot:       ds = SharpDX.Direct2D1.DashStyle.DashDot; break;
                case DashStyleHelper.DashDotDot:    ds = SharpDX.Direct2D1.DashStyle.DashDotDot; break;
                case DashStyleHelper.Dot:           ds = SharpDX.Direct2D1.DashStyle.Dot; break;
                default:                            ds = SharpDX.Direct2D1.DashStyle.Solid; break;
            }
            return new SharpDX.Direct2D1.StrokeStyle(rt.Factory, new StrokeStyleProperties { DashStyle = ds });
        }

        private void GetRectPixelCoords(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale,
            out float x1, out float y1, out float x2, out float y2)
        {
            System.Windows.Point startPoint = StartAnchor.GetPoint(chartControl, chartPanel, chartScale);
            System.Windows.Point endPoint   = EndAnchor.GetPoint(chartControl, chartPanel, chartScale);

            x1 = (float)Math.Min(startPoint.X, endPoint.X);
            y1 = (float)Math.Min(startPoint.Y, endPoint.Y);
            x2 = (float)Math.Max(startPoint.X, endPoint.X);
            y2 = (float)Math.Max(startPoint.Y, endPoint.Y);
        }
        #endregion

        #region Mouse Events
        // Track what part of the rectangle is being edited
        private enum EditMode { None, TopLeft, TopRight, BottomLeft, BottomRight, TopEdge, BottomEdge, LeftEdge, RightEdge, Move }
        private EditMode    currentEditMode;
        private ChartAnchor lastMoveDataPoint;

        public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, System.Windows.Point point)
        {
            if (DrawingState == DrawingState.Building)
                return Cursors.Pen;

            if (DrawingState == DrawingState.Moving)
                return Cursors.SizeAll;

            if (IsLocked)
                return Cursors.Arrow;

            float x1, y1, x2, y2;
            GetRectPixelCoords(chartControl, chartPanel, chartScale, out x1, out y1, out x2, out y2);

            float px = (float)point.X;
            float py = (float)point.Y;
            float tolerance = 8;

            // Check corners
            if (Math.Abs(px - x1) < tolerance && Math.Abs(py - y1) < tolerance) return Cursors.SizeNWSE;
            if (Math.Abs(px - x2) < tolerance && Math.Abs(py - y1) < tolerance) return Cursors.SizeNESW;
            if (Math.Abs(px - x1) < tolerance && Math.Abs(py - y2) < tolerance) return Cursors.SizeNESW;
            if (Math.Abs(px - x2) < tolerance && Math.Abs(py - y2) < tolerance) return Cursors.SizeNWSE;

            // Check edges
            if (px >= x1 - tolerance && px <= x2 + tolerance && Math.Abs(py - y1) < tolerance) return Cursors.SizeNS;
            if (px >= x1 - tolerance && px <= x2 + tolerance && Math.Abs(py - y2) < tolerance) return Cursors.SizeNS;
            if (py >= y1 - tolerance && py <= y2 + tolerance && Math.Abs(px - x1) < tolerance) return Cursors.SizeWE;
            if (py >= y1 - tolerance && py <= y2 + tolerance && Math.Abs(px - x2) < tolerance) return Cursors.SizeWE;

            // Inside
            if (px >= x1 && px <= x2 && py >= y1 && py <= y2) return Cursors.SizeAll;

            return null;
        }

        public override IEnumerable<AlertConditionItem> GetAlertConditionItems()
        {
            return new AlertConditionItem[0];
        }

        public override bool IsAlertConditionTrue(AlertConditionItem conditionItem, Condition condition, ChartAlertValue[] values, ChartControl chartControl, ChartScale chartScale)
        {
            return false;
        }

        public override bool IsVisibleOnChart(ChartControl chartControl, ChartScale chartScale, DateTime firstTimeOnChart, DateTime lastTimeOnChart)
        {
            if (DrawingState == DrawingState.Building) return true;
            if (Anchors.All(a => a.Time < firstTimeOnChart) || Anchors.All(a => a.Time > lastTimeOnChart))
            {
                if (ExtendTopRight || ExtendBottomRight || ExtendMidLineRight ||
                    ExtendTopLeft || ExtendBottomLeft || ExtendMidLineLeft)
                    return true;
                return false;
            }
            return true;
        }

        public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Building)
            {
                if (StartAnchor.IsEditing)
                {
                    dataPoint.CopyDataValues(StartAnchor);
                    StartAnchor.IsEditing = false;
                    dataPoint.CopyDataValues(EndAnchor);
                }
                else if (EndAnchor.IsEditing)
                {
                    dataPoint.CopyDataValues(EndAnchor);
                    EndAnchor.IsEditing = false;
                    DrawingState = DrawingState.Normal;
                    IsSelected = false;
                }
                return;
            }

            if (DrawingState == DrawingState.Normal && IsSelected)
            {
                System.Windows.Point p = dataPoint.GetPoint(chartControl, chartPanel, chartScale);
                float x1, y1, x2, y2;
                GetRectPixelCoords(chartControl, chartPanel, chartScale, out x1, out y1, out x2, out y2);

                float px = (float)p.X;
                float py = (float)p.Y;
                float tolerance = 8;

                // Corners - allow resizing from any corner
                if (Math.Abs(px - x1) < tolerance && Math.Abs(py - y1) < tolerance)
                    { currentEditMode = EditMode.TopLeft; DrawingState = DrawingState.Editing; return; }
                if (Math.Abs(px - x2) < tolerance && Math.Abs(py - y1) < tolerance)
                    { currentEditMode = EditMode.TopRight; DrawingState = DrawingState.Editing; return; }
                if (Math.Abs(px - x1) < tolerance && Math.Abs(py - y2) < tolerance)
                    { currentEditMode = EditMode.BottomLeft; DrawingState = DrawingState.Editing; return; }
                if (Math.Abs(px - x2) < tolerance && Math.Abs(py - y2) < tolerance)
                    { currentEditMode = EditMode.BottomRight; DrawingState = DrawingState.Editing; return; }

                // Edges - allow resizing one dimension
                if (px >= x1 - tolerance && px <= x2 + tolerance && Math.Abs(py - y1) < tolerance)
                    { currentEditMode = EditMode.TopEdge; DrawingState = DrawingState.Editing; return; }
                if (px >= x1 - tolerance && px <= x2 + tolerance && Math.Abs(py - y2) < tolerance)
                    { currentEditMode = EditMode.BottomEdge; DrawingState = DrawingState.Editing; return; }
                if (py >= y1 - tolerance && py <= y2 + tolerance && Math.Abs(px - x1) < tolerance)
                    { currentEditMode = EditMode.LeftEdge; DrawingState = DrawingState.Editing; return; }
                if (py >= y1 - tolerance && py <= y2 + tolerance && Math.Abs(px - x2) < tolerance)
                    { currentEditMode = EditMode.RightEdge; DrawingState = DrawingState.Editing; return; }

                // Inside = Move the whole thing
                if (px >= x1 && px <= x2 && py >= y1 && py <= y2)
                {
                    currentEditMode = EditMode.Move;
                    lastMoveDataPoint = new ChartAnchor();
                    dataPoint.CopyDataValues(lastMoveDataPoint);
                    lastMoveDataPoint.DrawingTool = this;
                    DrawingState = DrawingState.Moving;
                    return;
                }
            }
        }

        public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            // === BUILDING ===
            if (DrawingState == DrawingState.Building && !StartAnchor.IsEditing)
            {
                dataPoint.CopyDataValues(EndAnchor);
                return;
            }

            // === EDITING: resize corners or edges ===
            if (DrawingState == DrawingState.Editing)
            {
                // Determine which anchor holds the min/max time and price
                // StartAnchor and EndAnchor define two opposite corners
                // We need to figure out which anchor's Time/Price to update
                bool startIsLeft  = StartAnchor.Time <= EndAnchor.Time;
                bool startIsTop   = StartAnchor.Price >= EndAnchor.Price;

                // Map screen corners to anchor properties:
                // TopLeft     = (earlier time, higher price)
                // TopRight    = (later time, higher price)
                // BottomLeft  = (earlier time, lower price)
                // BottomRight = (later time, lower price)

                ChartAnchor leftAnchor   = startIsLeft ? StartAnchor : EndAnchor;
                ChartAnchor rightAnchor  = startIsLeft ? EndAnchor : StartAnchor;
                ChartAnchor topAnchor    = startIsTop ? StartAnchor : EndAnchor;
                ChartAnchor bottomAnchor = startIsTop ? EndAnchor : StartAnchor;

                switch (currentEditMode)
                {
                    case EditMode.TopLeft:
                        leftAnchor.Time      = dataPoint.Time;
                        leftAnchor.SlotIndex = dataPoint.SlotIndex;
                        topAnchor.Price      = dataPoint.Price;
                        break;
                    case EditMode.TopRight:
                        rightAnchor.Time      = dataPoint.Time;
                        rightAnchor.SlotIndex = dataPoint.SlotIndex;
                        topAnchor.Price       = dataPoint.Price;
                        break;
                    case EditMode.BottomLeft:
                        leftAnchor.Time        = dataPoint.Time;
                        leftAnchor.SlotIndex   = dataPoint.SlotIndex;
                        bottomAnchor.Price     = dataPoint.Price;
                        break;
                    case EditMode.BottomRight:
                        rightAnchor.Time      = dataPoint.Time;
                        rightAnchor.SlotIndex = dataPoint.SlotIndex;
                        bottomAnchor.Price    = dataPoint.Price;
                        break;
                    case EditMode.TopEdge:
                        topAnchor.Price = dataPoint.Price;
                        break;
                    case EditMode.BottomEdge:
                        bottomAnchor.Price = dataPoint.Price;
                        break;
                    case EditMode.LeftEdge:
                        leftAnchor.Time      = dataPoint.Time;
                        leftAnchor.SlotIndex = dataPoint.SlotIndex;
                        break;
                    case EditMode.RightEdge:
                        rightAnchor.Time      = dataPoint.Time;
                        rightAnchor.SlotIndex = dataPoint.SlotIndex;
                        break;
                }
                return;
            }

            // === MOVING: translate both anchors ===
            if (DrawingState == DrawingState.Moving && lastMoveDataPoint != null)
            {
                foreach (ChartAnchor anchor in Anchors)
                    anchor.MoveAnchor(lastMoveDataPoint, dataPoint, chartControl, chartPanel, chartScale, this);

                dataPoint.CopyDataValues(lastMoveDataPoint);
                return;
            }
        }

        public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Building)
                return;

            if (DrawingState == DrawingState.Editing)
            {
                currentEditMode = EditMode.None;
                DrawingState = DrawingState.Normal;
                IsSelected = true;
                return;
            }

            if (DrawingState == DrawingState.Moving)
            {
                currentEditMode = EditMode.None;
                lastMoveDataPoint = null;
                DrawingState = DrawingState.Normal;
                IsSelected = true;
                return;
            }
        }
        #endregion

        #region Hit Test
        public override System.Windows.Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
        {
            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
            float x1, y1, x2, y2;
            GetRectPixelCoords(chartControl, chartPanel, chartScale, out x1, out y1, out x2, out y2);

            // Just the 4 corners for click-to-select hit testing
            return new System.Windows.Point[]
            {
                new System.Windows.Point(x1, y1),
                new System.Windows.Point(x2, y1),
                new System.Windows.Point(x1, y2),
                new System.Windows.Point(x2, y2),
            };
        }
        #endregion

        #region Rendering
        public override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (chartControl == null || chartScale == null) return;

            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
            if (chartPanel == null) return;

            RenderTarget rt = RenderTarget;
            if (rt == null || rt.IsDisposed) return;

            // Dispose old brushes
            DisposeResources();

            // Create brushes
            borderBrushDx   = CreateDxBrush(rt, BorderBrush, BorderOpacity);
            midLineBrushDx  = CreateDxBrush(rt, MidLineBrush, MidLineOpacity);
            fillBrushDx     = CreateDxBrush(rt, FillBrush, FillOpacity);
            textBrushDx     = CreateDxBrush(rt, LabelBrush, 100);
            extLineBrushDx  = CreateDxBrush(rt, ExtensionLineBrush, ExtensionLineOpacity);

            if (borderBrushDx == null) return;

            // Get pixel coordinates
            float x1, y1, x2, y2;
            GetRectPixelCoords(chartControl, chartPanel, chartScale, out x1, out y1, out x2, out y2);

            float midY = (y1 + y2) / 2f;
            float panelW = (float)chartPanel.W;

            // --- Draw Fill ---
            if (FillMode == RectFillMode.Solid && fillBrushDx != null)
            {
                rt.FillRectangle(new SharpDX.RectangleF(x1, y1, x2 - x1, y2 - y1), fillBrushDx);
            }
            else if (FillMode == RectFillMode.Gradient)
            {
                System.Windows.Media.SolidColorBrush scb1 = FillBrush as System.Windows.Media.SolidColorBrush;
                System.Windows.Media.SolidColorBrush scb2 = FillBrush2 as System.Windows.Media.SolidColorBrush;
                if (scb1 != null && scb2 != null)
                {
                    float fAlpha = FillOpacity / 100.0f;
                    SharpDX.Color c1 = new SharpDX.Color(scb1.Color.R, scb1.Color.G, scb1.Color.B, (byte)(fAlpha * scb1.Color.A));
                    SharpDX.Color c2 = new SharpDX.Color(scb2.Color.R, scb2.Color.G, scb2.Color.B, (byte)(fAlpha * scb2.Color.A));

                    SharpDX.Direct2D1.LinearGradientBrushProperties lgbp = new SharpDX.Direct2D1.LinearGradientBrushProperties
                    {
                        StartPoint = new SharpDX.Vector2(x1, y1),
                        EndPoint   = new SharpDX.Vector2(x1, y2)
                    };

                    SharpDX.Direct2D1.GradientStopCollection gsc = new SharpDX.Direct2D1.GradientStopCollection(rt,
                        new SharpDX.Direct2D1.GradientStop[]
                        {
                            new SharpDX.Direct2D1.GradientStop { Color = c1, Position = 0f },
                            new SharpDX.Direct2D1.GradientStop { Color = c2, Position = 1f }
                        });

                    SharpDX.Direct2D1.LinearGradientBrush lgb = new SharpDX.Direct2D1.LinearGradientBrush(rt, lgbp, gsc);
                    rt.FillRectangle(new SharpDX.RectangleF(x1, y1, x2 - x1, y2 - y1), lgb);
                    lgb.Dispose();
                    gsc.Dispose();
                }
            }

            // --- Draw Border ---
            SharpDX.Direct2D1.StrokeStyle borderStyle = CreateStrokeStyle(rt, BorderDashStyle);
            rt.DrawLine(new SharpDX.Vector2(x1, y1), new SharpDX.Vector2(x2, y1), borderBrushDx, BorderWidth, borderStyle);
            rt.DrawLine(new SharpDX.Vector2(x1, y2), new SharpDX.Vector2(x2, y2), borderBrushDx, BorderWidth, borderStyle);
            rt.DrawLine(new SharpDX.Vector2(x1, y1), new SharpDX.Vector2(x1, y2), borderBrushDx, BorderWidth, borderStyle);
            rt.DrawLine(new SharpDX.Vector2(x2, y1), new SharpDX.Vector2(x2, y2), borderBrushDx, BorderWidth, borderStyle);
            borderStyle.Dispose();

            // --- Draw Extension Lines ---
            if (extLineBrushDx != null)
            {
                SharpDX.Direct2D1.StrokeStyle extStyle = CreateStrokeStyle(rt, ExtensionLineDashStyle);
                if (ExtendTopRight)
                    rt.DrawLine(new SharpDX.Vector2(x2, y1), new SharpDX.Vector2(panelW, y1), extLineBrushDx, ExtensionLineWidth, extStyle);
                if (ExtendTopLeft)
                    rt.DrawLine(new SharpDX.Vector2(x1, y1), new SharpDX.Vector2(0, y1), extLineBrushDx, ExtensionLineWidth, extStyle);
                if (ExtendBottomRight)
                    rt.DrawLine(new SharpDX.Vector2(x2, y2), new SharpDX.Vector2(panelW, y2), extLineBrushDx, ExtensionLineWidth, extStyle);
                if (ExtendBottomLeft)
                    rt.DrawLine(new SharpDX.Vector2(x1, y2), new SharpDX.Vector2(0, y2), extLineBrushDx, ExtensionLineWidth, extStyle);
                extStyle.Dispose();
            }

            // --- Draw Mid Line ---
            if (ShowMidLine && midLineBrushDx != null)
            {
                SharpDX.Direct2D1.StrokeStyle midStyle = CreateStrokeStyle(rt, MidLineDashStyle);
                float mlX1 = ExtendMidLineLeft ? 0 : x1;
                float mlX2 = ExtendMidLineRight ? panelW : x2;
                rt.DrawLine(new SharpDX.Vector2(mlX1, midY), new SharpDX.Vector2(mlX2, midY), midLineBrushDx, MidLineWidth, midStyle);
                midStyle.Dispose();
            }

            // --- Draw Label ---
            if (ShowLabel && textBrushDx != null && LabelPos != LabelPosition.None)
            {
                textFormatDx = new SharpDX.DirectWrite.TextFormat(
                    NinjaTrader.Core.Globals.DirectWriteFactory,
                    "Arial",
                    SharpDX.DirectWrite.FontWeight.Normal,
                    SharpDX.DirectWrite.FontStyle.Normal,
                    LabelFontSize);

                string displayText = "";
                if (!string.IsNullOrEmpty(LabelText))
                    displayText = LabelText;

                double topPrice = Math.Max(StartAnchor.Price, EndAnchor.Price);
                double botPrice = Math.Min(StartAnchor.Price, EndAnchor.Price);
                double midPrice = (topPrice + botPrice) / 2.0;
                double range    = topPrice - botPrice;

                if (ShowMidPrice)
                {
                    if (displayText.Length > 0) displayText += "  ";
                    displayText += "Mid: " + midPrice.ToString("F2");
                }
                if (ShowPriceRange)
                {
                    if (displayText.Length > 0) displayText += "  ";
                    displayText += "Range: " + range.ToString("F2");
                }

                if (!string.IsNullOrEmpty(displayText))
                {
                    SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(
                        NinjaTrader.Core.Globals.DirectWriteFactory,
                        displayText, textFormatDx, panelW, (float)chartPanel.H);

                    float textW   = textLayout.Metrics.Width;
                    float textH   = textLayout.Metrics.Height;
                    float padding = 4;

                    float lx = x1 + padding;
                    float ly = y1 + padding;

                    switch (LabelPos)
                    {
                        case LabelPosition.TopLeft:         lx = x1 + padding;                   ly = y1 + padding; break;
                        case LabelPosition.TopCenter:       lx = (x1 + x2) / 2f - textW / 2f;   ly = y1 + padding; break;
                        case LabelPosition.TopRight:        lx = x2 - textW - padding;            ly = y1 + padding; break;
                        case LabelPosition.MiddleLeft:      lx = x1 + padding;                   ly = midY - textH / 2f; break;
                        case LabelPosition.MiddleCenter:    lx = (x1 + x2) / 2f - textW / 2f;   ly = midY - textH / 2f; break;
                        case LabelPosition.MiddleRight:     lx = x2 - textW - padding;            ly = midY - textH / 2f; break;
                        case LabelPosition.BottomLeft:      lx = x1 + padding;                   ly = y2 - textH - padding; break;
                        case LabelPosition.BottomCenter:    lx = (x1 + x2) / 2f - textW / 2f;   ly = y2 - textH - padding; break;
                        case LabelPosition.BottomRight:     lx = x2 - textW - padding;            ly = y2 - textH - padding; break;
                    }

                    rt.DrawTextLayout(new SharpDX.Vector2(lx, ly), textLayout, textBrushDx);
                    textLayout.Dispose();
                }
            }

            // --- Draw Anchor Points when selected ---
            if (IsSelected || DrawingState == DrawingState.Editing)
            {
                float anchorSize = 6f;

                // 4 corner handles - white fill with blue ring
                SharpDX.Vector2[] corners = new SharpDX.Vector2[]
                {
                    new SharpDX.Vector2(x1, y1),
                    new SharpDX.Vector2(x2, y1),
                    new SharpDX.Vector2(x1, y2),
                    new SharpDX.Vector2(x2, y2),
                };

                foreach (SharpDX.Vector2 corner in corners)
                {
                    SharpDX.Direct2D1.Ellipse ellipse = new SharpDX.Direct2D1.Ellipse(corner, anchorSize, anchorSize);
                    using (SharpDX.Direct2D1.SolidColorBrush fillBr = new SharpDX.Direct2D1.SolidColorBrush(rt, new SharpDX.Color4(1f, 1f, 1f, 1f)))
                        rt.FillEllipse(ellipse, fillBr);
                    using (SharpDX.Direct2D1.SolidColorBrush ringBr = new SharpDX.Direct2D1.SolidColorBrush(rt, new SharpDX.Color4(0.12f, 0.56f, 1f, 1f)))
                        rt.DrawEllipse(ellipse, ringBr, 2f);
                }
            }
        }
        #endregion
    }
}
