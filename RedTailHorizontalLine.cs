#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
#endregion

namespace NinjaTrader.NinjaScript.DrawingTools
{
    public class RedTailHorizontalLine : DrawingTool
    {
        #region Variables
        private bool                    isMoving;
        private ChartAnchor[]           anchorCollection;
        private ChartControl            cachedChartControl;
        private ChartScale              cachedChartScale;
        private ChartPanel              cachedChartPanel;

        // Hover edit button tracking
        private System.Windows.Point    lastMouseScreenPoint;
        private bool                    isMouseNearLine;
        private bool                    isMouseOverEditBtn;
        private SharpDX.RectangleF      editBtnRect;
        private const float             EditBtnSize     = 22f;
        private const float             EditBtnMargin   = 6f;
        private const float             HoverThreshold  = 12f;

        // Event subscription tracking
        private bool                    eventsSubscribed;
        private bool                    popupOpen;
        #endregion

        #region Anchors
        [Browsable(false)]
        public ChartAnchor Anchor { get; set; }

        public override IEnumerable<ChartAnchor> Anchors
        {
            get
            {
                if (anchorCollection == null || anchorCollection[0] != Anchor)
                    anchorCollection = new[] { Anchor };
                return anchorCollection;
            }
        }
        #endregion

        #region Properties

        [Display(Name = "Price Level", Description = "Price level for the horizontal line", Order = 0, GroupName = "Line Settings")]
        public double Price
        {
            get { return Anchor != null ? Anchor.Price : 0; }
            set
            {
                if (Anchor != null && value > 0)
                {
                    Anchor.Price = value;
                    ForceRefresh();
                }
            }
        }

        [Display(Name = "Label Text", Description = "Text label for the line", Order = 1, GroupName = "Line Settings")]
        public string LabelText { get; set; }

        [Display(Name = "Label Position", Description = "Where to display the label", Order = 2, GroupName = "Line Settings")]
        public LabelPositionType LabelPosition { get; set; }

        [Range(6, 48)]
        [Display(Name = "Font Size", Description = "Font size for the label", Order = 3, GroupName = "Line Settings")]
        public int FontSize { get; set; }

        [Display(Name = "Show Price", Description = "Show price value next to label", Order = 4, GroupName = "Line Settings")]
        public bool ShowPrice { get; set; }

        [XmlIgnore]
        [Display(Name = "Line Color", Description = "Color of the line", Order = 5, GroupName = "Line Settings")]
        public System.Windows.Media.Brush LineBrush { get; set; }

        [Browsable(false)]
        public string LineBrushSerialize
        {
            get { return Serialize.BrushToString(LineBrush); }
            set { LineBrush = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Text Color", Description = "Color of the label text", Order = 6, GroupName = "Line Settings")]
        public System.Windows.Media.Brush TextBrush { get; set; }

        [Browsable(false)]
        public string TextBrushSerialize
        {
            get { return Serialize.BrushToString(TextBrush); }
            set { TextBrush = Serialize.StringToBrush(value); }
        }

        [Range(1, 10)]
        [Display(Name = "Line Width", Description = "Width of the line", Order = 7, GroupName = "Line Settings")]
        public int LineWidth { get; set; }

        [Display(Name = "Line Dash Style", Description = "Dash style of the line", Order = 8, GroupName = "Line Settings")]
        public DashStyleHelper LineDashStyle { get; set; }

        [Range(0, 100)]
        [Display(Name = "Line Opacity %", Description = "Opacity of the line (0-100)", Order = 9, GroupName = "Line Settings")]
        public int LineOpacity { get; set; }

        [Display(Name = "Extend Right", Description = "Extend line to the right edge of chart", Order = 10, GroupName = "Line Settings")]
        public bool ExtendRight { get; set; }

        [Display(Name = "Extend Left", Description = "Extend line to the left edge of chart", Order = 11, GroupName = "Line Settings")]
        public bool ExtendLeft { get; set; }
        #endregion

        public enum LabelPositionType
        {
            Left,
            Center,
            Right
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name                = "RedTail HLine";
                Description         = "Simple named horizontal line drawing tool";

                LabelText           = string.Empty;
                LabelPosition       = LabelPositionType.Right;
                FontSize            = 12;
                ShowPrice           = true;
                LineBrush           = System.Windows.Media.Brushes.DodgerBlue;
                TextBrush           = System.Windows.Media.Brushes.White;
                LineWidth           = 2;
                LineDashStyle       = DashStyleHelper.Solid;
                LineOpacity         = 100;
                ExtendRight         = true;
                ExtendLeft          = true;

                Anchor              = new ChartAnchor
                {
                    IsEditing       = true,
                    DrawingTool     = this,
                    DisplayName     = "Price",
                };

                editBtnRect         = new SharpDX.RectangleF(0, 0, 0, 0);
            }
            else if (State == State.Terminated)
            {
                UnsubscribeEvents();
            }
        }

        private void ForceRefresh()
        {
            if (cachedChartControl != null)
                cachedChartControl.InvalidateVisual();
        }

        #region Chart Panel Event Subscriptions
        private void SubscribeEvents()
        {
            if (eventsSubscribed || cachedChartPanel == null)
                return;

            cachedChartPanel.MouseMove                  += OnChartPanelMouseMove;
            cachedChartPanel.PreviewMouseLeftButtonDown  += OnChartPanelMouseDown;
            eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!eventsSubscribed || cachedChartPanel == null)
                return;

            cachedChartPanel.MouseMove                  -= OnChartPanelMouseMove;
            cachedChartPanel.PreviewMouseLeftButtonDown  -= OnChartPanelMouseDown;
            eventsSubscribed = false;
        }

        private void OnChartPanelMouseMove(object sender, MouseEventArgs e)
        {
            if (cachedChartControl == null || cachedChartScale == null || cachedChartPanel == null || Anchor == null)
                return;

            if (DrawingState == DrawingState.Building)
                return;

            System.Windows.Point mousePoint = e.GetPosition(cachedChartPanel);
            lastMouseScreenPoint = mousePoint;

            System.Windows.Point anchorPoint = Anchor.GetPoint(cachedChartControl, cachedChartPanel, cachedChartScale);
            bool nearLine = Math.Abs(mousePoint.Y - anchorPoint.Y) <= HoverThreshold;

            bool overBtn = editBtnRect.Width > 0
                && mousePoint.X >= editBtnRect.Left   && mousePoint.X <= editBtnRect.Right
                && mousePoint.Y >= editBtnRect.Top    && mousePoint.Y <= editBtnRect.Bottom;

            bool needsRedraw = (nearLine != isMouseNearLine) || (overBtn != isMouseOverEditBtn);
            isMouseNearLine     = nearLine;
            isMouseOverEditBtn  = overBtn;

            // Update cursor based on hover state
            if (overBtn)
                cachedChartPanel.Cursor = Cursors.Hand;

            if (needsRedraw)
                ForceRefresh();
        }

        private void OnChartPanelMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (popupOpen)
                return;

            if (isMouseOverEditBtn)
            {
                e.Handled = true;    // Prevent NT from starting a drag or selection
                ShowPriceEditPopup();
            }
        }
        #endregion

        #region Price Edit Popup
        private void ShowPriceEditPopup()
        {
            ChartControl cc = cachedChartControl;
            if (cc == null || Anchor == null || popupOpen)
                return;

            popupOpen = true;

            cc.Dispatcher.InvokeAsync(() =>
            {
                string tickFormat = "F2";
                if (AttachedTo != null && AttachedTo.Instrument != null)
                    tickFormat = Core.Globals.GetTickFormatString(AttachedTo.Instrument.MasterInstrument.TickSize);

                Window popupWindow = new Window
                {
                    Title                   = "Edit Price Level",
                    Width                   = 280,
                    Height                  = 140,
                    WindowStartupLocation   = WindowStartupLocation.CenterScreen,
                    ResizeMode              = ResizeMode.NoResize,
                    WindowStyle             = WindowStyle.ToolWindow,
                    Topmost                 = true,
                    Background              = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)),
                };

                Grid grid       = new Grid();
                grid.Margin     = new Thickness(12);
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                StackPanel inputPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                TextBlock label = new TextBlock
                {
                    Text                = "Price:  ",
                    Foreground          = System.Windows.Media.Brushes.White,
                    FontSize            = 14,
                    VerticalAlignment   = VerticalAlignment.Center,
                };

                TextBox priceBox = new TextBox
                {
                    Text                = Anchor.Price.ToString(tickFormat),
                    Width               = 160,
                    FontSize            = 14,
                    Padding             = new Thickness(4, 2, 4, 2),
                    Background          = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 50)),
                    Foreground          = System.Windows.Media.Brushes.White,
                    BorderBrush         = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                    CaretBrush          = System.Windows.Media.Brushes.White,
                    SelectionBrush      = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)),
                };
                priceBox.SelectAll();

                inputPanel.Children.Add(label);
                inputPanel.Children.Add(priceBox);
                Grid.SetRow(inputPanel, 0);
                grid.Children.Add(inputPanel);

                StackPanel buttonPanel = new StackPanel
                {
                    Orientation         = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };

                Button okBtn = new Button
                {
                    Content     = "OK",
                    Width       = 70,
                    Height      = 28,
                    Margin      = new Thickness(0, 0, 8, 0),
                    Background  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 100, 200)),
                    Foreground  = System.Windows.Media.Brushes.White,
                    FontSize    = 12,
                    IsDefault   = true,
                };

                Button cancelBtn = new Button
                {
                    Content     = "Cancel",
                    Width       = 70,
                    Height      = 28,
                    Background  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(55, 55, 55)),
                    Foreground  = System.Windows.Media.Brushes.White,
                    FontSize    = 12,
                    IsCancel    = true,
                };

                buttonPanel.Children.Add(okBtn);
                buttonPanel.Children.Add(cancelBtn);
                Grid.SetRow(buttonPanel, 2);
                grid.Children.Add(buttonPanel);

                popupWindow.Content = grid;

                okBtn.Click += (s, ev) =>
                {
                    double newPrice;
                    if (double.TryParse(priceBox.Text, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out newPrice) && newPrice > 0)
                    {
                        Anchor.Price = newPrice;
                        ForceRefresh();
                        popupWindow.DialogResult = true;
                        popupWindow.Close();
                    }
                    else
                    {
                        priceBox.BorderBrush = System.Windows.Media.Brushes.Red;
                        priceBox.Focus();
                    }
                };

                cancelBtn.Click += (s, ev) => popupWindow.Close();

                popupWindow.Closed += (s, ev) => { popupOpen = false; };

                popupWindow.Loaded += (s, ev) =>
                {
                    priceBox.Focus();
                    priceBox.SelectAll();
                };

                popupWindow.ShowDialog();
            });
        }
        #endregion

        #region Mouse / Cursor Handling (NT8 Drawing Tool Overrides)
        public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, System.Windows.Point point)
        {
            cachedChartControl  = chartControl;
            cachedChartScale    = chartScale;

            if (DrawingState == DrawingState.Building)
                return Cursors.Pen;

            if (DrawingState == DrawingState.Moving)
                return Cursors.SizeAll;

            // If hovering the edit button, show hand cursor and block NT from treating as line interaction
            if (isMouseOverEditBtn)
                return Cursors.Hand;

            System.Windows.Point anchorPoint = Anchor.GetPoint(chartControl, chartPanel, chartScale);

            if (Math.Abs(point.Y - anchorPoint.Y) <= 8)
            {
                IsSelected = true;
                return Cursors.SizeAll;
            }

            IsSelected = false;
            return null;
        }

        public override System.Windows.Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
        {
            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
            System.Windows.Point anchorPoint = Anchor.GetPoint(chartControl, chartPanel, chartScale);

            float chartWidth = (float)chartPanel.W;
            float yPixel = (float)anchorPoint.Y;

            return new[]
            {
                new System.Windows.Point(chartWidth * 0.25, yPixel),
                new System.Windows.Point(chartWidth * 0.5, yPixel),
                new System.Windows.Point(chartWidth * 0.75, yPixel),
            };
        }

        public override void OnCalculateMinMax()
        {
            MinValue = Anchor.Price;
            MaxValue = Anchor.Price;
        }

        public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Building)
            {
                dataPoint.CopyDataValues(Anchor);
                Anchor.IsEditing = false;
                DrawingState = DrawingState.Normal;
                IsSelected = false;
                return;
            }

            // If the edit button is hovered, don't start a drag
            if (isMouseOverEditBtn)
                return;

            if (DrawingState == DrawingState.Editing)
            {
                System.Windows.Point cursorPoint = dataPoint.GetPoint(chartControl, chartPanel, chartScale);
                System.Windows.Point anchorPoint = Anchor.GetPoint(chartControl, chartPanel, chartScale);

                if (Math.Abs(cursorPoint.Y - anchorPoint.Y) <= HoverThreshold)
                {
                    DrawingState = DrawingState.Moving;
                    isMoving = true;
                }
            }
        }

        public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Building)
            {
                dataPoint.CopyDataValues(Anchor);
            }
            else if (isMoving)
            {
                Anchor.Price = dataPoint.Price;
            }
        }

        public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (isMoving)
            {
                isMoving = false;
                DrawingState = DrawingState.Normal;
            }
        }
        #endregion

        #region Rendering
        public override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (Anchor == null || chartControl == null || chartScale == null)
                return;

            cachedChartControl  = chartControl;
            cachedChartScale    = chartScale;

            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];

            // Cache and subscribe to chart panel events on first render
            if (cachedChartPanel != chartPanel)
            {
                UnsubscribeEvents();
                cachedChartPanel = chartPanel;
                SubscribeEvents();
            }

            SharpDX.Direct2D1.RenderTarget renderTarget = RenderTarget;

            if (renderTarget == null)
                return;

            System.Windows.Point anchorPoint = Anchor.GetPoint(chartControl, chartPanel, chartScale);
            float yPixel    = (float)anchorPoint.Y;
            float xAnchor   = (float)anchorPoint.X;
            float chartLeft = (float)chartPanel.X;
            float chartRight = (float)(chartPanel.X + chartPanel.W);

            // Determine line start/end
            float lineStart = ExtendLeft ? chartLeft : xAnchor;
            float lineEnd   = ExtendRight ? chartRight : xAnchor;

            if (!ExtendLeft && !ExtendRight)
            {
                lineStart = chartLeft;
                lineEnd = chartRight;
            }

            // Build DX resources
            float opacity = LineOpacity / 100f;
            System.Windows.Media.Color lineMediaColor = ((System.Windows.Media.SolidColorBrush)LineBrush).Color;
            SharpDX.Color4 lineColor4 = new SharpDX.Color4(
                lineMediaColor.R / 255f,
                lineMediaColor.G / 255f,
                lineMediaColor.B / 255f,
                opacity);

            using (var lineBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget, lineColor4))
            {
                // Convert dash style
                SharpDX.Direct2D1.StrokeStyle strokeStyle = null;
                if (LineDashStyle != DashStyleHelper.Solid)
                {
                    SharpDX.Direct2D1.StrokeStyleProperties strokeProps = new SharpDX.Direct2D1.StrokeStyleProperties
                    {
                        DashCap    = SharpDX.Direct2D1.CapStyle.Round,
                        StartCap   = SharpDX.Direct2D1.CapStyle.Round,
                        EndCap     = SharpDX.Direct2D1.CapStyle.Round,
                        DashStyle  = SharpDX.Direct2D1.DashStyle.Custom,
                        DashOffset = 0f,
                    };

                    float[] dashes;
                    switch (LineDashStyle)
                    {
                        case DashStyleHelper.Dot:        dashes = new float[] { 0.1f, 2f }; break;
                        case DashStyleHelper.Dash:       dashes = new float[] { 4f, 3f }; break;
                        case DashStyleHelper.DashDot:    dashes = new float[] { 4f, 2f, 0.1f, 2f }; break;
                        case DashStyleHelper.DashDotDot: dashes = new float[] { 4f, 2f, 0.1f, 2f, 0.1f, 2f }; break;
                        default:                         dashes = new float[] { 1f }; break;
                    }

                    strokeStyle = new SharpDX.Direct2D1.StrokeStyle(
                        NinjaTrader.Core.Globals.D2DFactory,
                        strokeProps,
                        dashes);
                }

                // Draw the line
                renderTarget.DrawLine(
                    new SharpDX.Vector2(lineStart, yPixel),
                    new SharpDX.Vector2(lineEnd, yPixel),
                    lineBrush,
                    LineWidth,
                    strokeStyle);

                if (strokeStyle != null)
                    strokeStyle.Dispose();

                // Build label text
                string labelPart = !string.IsNullOrEmpty(LabelText) ? LabelText : string.Empty;
                string pricePart = string.Empty;

                if (ShowPrice && AttachedTo != null && AttachedTo.Instrument != null)
                    pricePart = Anchor.Price.ToString(Core.Globals.GetTickFormatString(AttachedTo.Instrument.MasterInstrument.TickSize));

                string displayText = string.Empty;
                if (!string.IsNullOrEmpty(labelPart) && !string.IsNullOrEmpty(pricePart))
                    displayText = labelPart + "  " + pricePart;
                else if (!string.IsNullOrEmpty(labelPart))
                    displayText = labelPart;
                else if (!string.IsNullOrEmpty(pricePart))
                    displayText = pricePart;

                // Only render label if there's something to show
                if (!string.IsNullOrEmpty(displayText))
                {
                    using (var textFormat = new SharpDX.DirectWrite.TextFormat(
                        NinjaTrader.Core.Globals.DirectWriteFactory,
                        "Arial",
                        SharpDX.DirectWrite.FontWeight.Bold,
                        SharpDX.DirectWrite.FontStyle.Normal,
                        FontSize))
                    using (var textLayout = new SharpDX.DirectWrite.TextLayout(
                        NinjaTrader.Core.Globals.DirectWriteFactory,
                        displayText,
                        textFormat,
                        chartRight - chartLeft,
                        FontSize + 4))
                    {
                        float textWidth  = textLayout.Metrics.Width;
                        float textHeight = textLayout.Metrics.Height;

                        // Position label
                        float textX;
                        float padding = 8;
                        switch (LabelPosition)
                        {
                            case LabelPositionType.Left:
                                textX = chartLeft + padding;
                                break;
                            case LabelPositionType.Center:
                                textX = (chartLeft + chartRight) / 2f - textWidth / 2f;
                                break;
                            case LabelPositionType.Right:
                            default:
                                textX = chartRight - textWidth - padding - 40;
                                break;
                        }

                        float textY = yPixel - textHeight - 4;

                        // Draw background pill behind text
                        SharpDX.RectangleF bgRect = new SharpDX.RectangleF(
                            textX - 4, textY - 2,
                            textWidth + 8, textHeight + 4);

                        using (var bgBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                            new SharpDX.Color4(lineMediaColor.R / 255f, lineMediaColor.G / 255f, lineMediaColor.B / 255f, opacity * 0.85f)))
                        {
                            renderTarget.FillRoundedRectangle(
                                new RoundedRectangle { Rect = bgRect, RadiusX = 3, RadiusY = 3 },
                                bgBrush);
                        }

                        // Draw label text
                        System.Windows.Media.Color textMediaColor = ((System.Windows.Media.SolidColorBrush)TextBrush).Color;
                        using (var textBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                            new SharpDX.Color4(textMediaColor.R / 255f, textMediaColor.G / 255f, textMediaColor.B / 255f, 1f)))
                        {
                            renderTarget.DrawTextLayout(
                                new SharpDX.Vector2(textX, textY),
                                textLayout,
                                textBrush);
                        }
                    }
                }

                // ── Draw Edit Button when mouse is near the line ──
                if (isMouseNearLine && DrawingState != DrawingState.Building)
                {
                    RenderEditButton(renderTarget, lineMediaColor, opacity, yPixel, chartRight);
                }
                else
                {
                    editBtnRect = new SharpDX.RectangleF(0, 0, 0, 0);
                }
            }
        }

        private void RenderEditButton(SharpDX.Direct2D1.RenderTarget renderTarget,
            System.Windows.Media.Color lineMediaColor, float opacity, float yPixel, float chartRight)
        {
            float btnX = chartRight - EditBtnSize - EditBtnMargin - 8;
            float btnY = yPixel - EditBtnSize / 2f;

            editBtnRect = new SharpDX.RectangleF(btnX, btnY, EditBtnSize, EditBtnSize);

            // Button background — brighter when hovered
            float bgAlpha = isMouseOverEditBtn ? 0.95f : 0.80f;
            SharpDX.Color4 btnBgColor;

            if (isMouseOverEditBtn)
            {
                btnBgColor = new SharpDX.Color4(
                    Math.Min(1f, lineMediaColor.R / 255f * 0.8f + 0.2f),
                    Math.Min(1f, lineMediaColor.G / 255f * 0.8f + 0.2f),
                    Math.Min(1f, lineMediaColor.B / 255f * 0.8f + 0.2f),
                    bgAlpha);
            }
            else
            {
                btnBgColor = new SharpDX.Color4(
                    lineMediaColor.R / 255f * 0.6f,
                    lineMediaColor.G / 255f * 0.6f,
                    lineMediaColor.B / 255f * 0.6f,
                    bgAlpha);
            }

            using (var btnBgBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget, btnBgColor))
            {
                renderTarget.FillRoundedRectangle(
                    new RoundedRectangle { Rect = editBtnRect, RadiusX = 4, RadiusY = 4 },
                    btnBgBrush);
            }

            // Border
            using (var borderBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                new SharpDX.Color4(1f, 1f, 1f, isMouseOverEditBtn ? 0.5f : 0.25f)))
            {
                renderTarget.DrawRoundedRectangle(
                    new RoundedRectangle { Rect = editBtnRect, RadiusX = 4, RadiusY = 4 },
                    borderBrush, 1f);
            }

            // Draw pencil icon
            using (var iconBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                new SharpDX.Color4(1f, 1f, 1f, isMouseOverEditBtn ? 1f : 0.85f)))
            {
                float cx = btnX + EditBtnSize / 2f;
                float cy = btnY + EditBtnSize / 2f;
                float s  = EditBtnSize * 0.28f;

                // Pencil body
                renderTarget.DrawLine(
                    new SharpDX.Vector2(cx - s, cy + s),
                    new SharpDX.Vector2(cx + s, cy - s),
                    iconBrush, 2f);

                // Pencil tip
                renderTarget.DrawLine(
                    new SharpDX.Vector2(cx - s * 1.15f, cy + s * 1.15f),
                    new SharpDX.Vector2(cx - s * 0.5f, cy + s * 0.5f),
                    iconBrush, 1.5f);

                // Pencil eraser end
                renderTarget.DrawLine(
                    new SharpDX.Vector2(cx + s * 0.5f, cy - s * 0.5f),
                    new SharpDX.Vector2(cx + s * 1.1f, cy - s * 1.1f),
                    iconBrush, 3f);

                // Small baseline
                renderTarget.DrawLine(
                    new SharpDX.Vector2(cx - s * 1.2f, cy + s * 1.3f),
                    new SharpDX.Vector2(cx - s * 0.3f, cy + s * 1.3f),
                    iconBrush, 1.5f);
            }
        }
        #endregion
    }
}
