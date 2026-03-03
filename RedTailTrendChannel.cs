#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Input;
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
#endregion

using WpfPoint			= System.Windows.Point;
using WpfBrush			= System.Windows.Media.Brush;
using WpfBrushes		= System.Windows.Media.Brushes;
using WpfColor			= System.Windows.Media.Color;
using WpfColors			= System.Windows.Media.Colors;
using WpfSolidBrush		= System.Windows.Media.SolidColorBrush;
using DxBrush			= SharpDX.Direct2D1.Brush;
using DxSolidBrush		= SharpDX.Direct2D1.SolidColorBrush;
using DxPathGeometry	= SharpDX.Direct2D1.PathGeometry;
using DxStrokeStyle		= SharpDX.Direct2D1.StrokeStyle;

namespace NinjaTrader.NinjaScript.DrawingTools
{
	[CategoryOrder("Channel Lines", 1)]
	[CategoryOrder("Midline", 2)]
	[CategoryOrder("Channel Fill", 3)]
	[CategoryOrder("Extensions", 4)]
	[CategoryOrder("Price Labels", 5)]
	public class RedTailTrendChannel : DrawingTool
	{
		#region Enums

		public enum ChannelExtensionMode
		{
			None,
			Right,
			Left,
			Both
		}

		#endregion

		#region Private Fields

		private const int		cursorSensitivity	= 15;
		private int				drawStep;

		// Track previous render target to detect when we need new DX resources
		private RenderTarget	lastRenderTarget;

		private DxBrush			upperLineBrushDx;
		private DxBrush			lowerLineBrushDx;
		private DxBrush			midLineBrushDx;
		private DxBrush			fillBrushDx;
		private DxBrush			extensionBrushDx;
		private DxBrush			labelBgBrushDx;
		private DxBrush			labelTextBrushDx;
		private DxStrokeStyle	upperStrokeStyleDx;
		private DxStrokeStyle	lowerStrokeStyleDx;
		private DxStrokeStyle	midStrokeStyleDx;
		private DxStrokeStyle	extStrokeStyleDx;
		private SharpDX.DirectWrite.TextFormat labelTextFormatDx;

		#endregion

		#region Chart Anchors

		[Display(Order = 1)]
		public ChartAnchor Anchor1 { get; set; }

		[Display(Order = 2)]
		public ChartAnchor Anchor2 { get; set; }

		[Display(Order = 3)]
		public ChartAnchor Anchor3 { get; set; }

		public override IEnumerable<ChartAnchor> Anchors
		{
			get { return new[] { Anchor1, Anchor2, Anchor3 }; }
		}

		#endregion

		#region Properties - Channel Lines

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Upper Line Color", GroupName = "Channel Lines", Order = 1)]
		public WpfBrush UpperLineBrush { get; set; }

		[Browsable(false)]
		public string UpperLineBrushSerialize
		{
			get { return Serialize.BrushToString(UpperLineBrush); }
			set { UpperLineBrush = Serialize.StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Lower Line Color", GroupName = "Channel Lines", Order = 2)]
		public WpfBrush LowerLineBrush { get; set; }

		[Browsable(false)]
		public string LowerLineBrushSerialize
		{
			get { return Serialize.BrushToString(LowerLineBrush); }
			set { LowerLineBrush = Serialize.StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name = "Upper Line Width", GroupName = "Channel Lines", Order = 3)]
		public int UpperLineWidth { get; set; }

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name = "Lower Line Width", GroupName = "Channel Lines", Order = 4)]
		public int LowerLineWidth { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Upper Line Dash Style", GroupName = "Channel Lines", Order = 5)]
		public DashStyleHelper UpperLineDashStyle { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Lower Line Dash Style", GroupName = "Channel Lines", Order = 6)]
		public DashStyleHelper LowerLineDashStyle { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Line Opacity %", GroupName = "Channel Lines", Order = 7)]
		public int LineOpacity { get; set; }

		#endregion

		#region Properties - Midline

		[NinjaScriptProperty]
		[Display(Name = "Show Midline", GroupName = "Midline", Order = 1)]
		public bool ShowMidline { get; set; }

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Midline Color", GroupName = "Midline", Order = 2)]
		public WpfBrush MidLineBrush { get; set; }

		[Browsable(false)]
		public string MidLineBrushSerialize
		{
			get { return Serialize.BrushToString(MidLineBrush); }
			set { MidLineBrush = Serialize.StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name = "Midline Width", GroupName = "Midline", Order = 3)]
		public int MidLineWidth { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Midline Dash Style", GroupName = "Midline", Order = 4)]
		public DashStyleHelper MidLineDashStyle { get; set; }

		#endregion

		#region Properties - Channel Fill

		[NinjaScriptProperty]
		[Display(Name = "Show Fill", GroupName = "Channel Fill", Order = 1)]
		public bool ShowFill { get; set; }

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Fill Color", GroupName = "Channel Fill", Order = 2)]
		public WpfBrush FillBrush { get; set; }

		[Browsable(false)]
		public string FillBrushSerialize
		{
			get { return Serialize.BrushToString(FillBrush); }
			set { FillBrush = Serialize.StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Fill Opacity %", GroupName = "Channel Fill", Order = 3)]
		public int FillOpacity { get; set; }

		#endregion

		#region Properties - Extensions

		[NinjaScriptProperty]
		[Display(Name = "Extension Mode", GroupName = "Extensions", Order = 1)]
		public ChannelExtensionMode ExtensionMode { get; set; }

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Extension Color", GroupName = "Extensions", Order = 2)]
		public WpfBrush ExtensionBrush { get; set; }

		[Browsable(false)]
		public string ExtensionBrushSerialize
		{
			get { return Serialize.BrushToString(ExtensionBrush); }
			set { ExtensionBrush = Serialize.StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name = "Extension Line Width", GroupName = "Extensions", Order = 3)]
		public int ExtensionLineWidth { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Extension Dash Style", GroupName = "Extensions", Order = 4)]
		public DashStyleHelper ExtensionDashStyle { get; set; }

		[NinjaScriptProperty]
		[Range(0, 500)]
		[Display(Name = "Extension Bars (0=infinite)", GroupName = "Extensions", Order = 5)]
		public int ExtensionBars { get; set; }

		#endregion

		#region Properties - Price Labels

		[NinjaScriptProperty]
		[Display(Name = "Show Price Labels", GroupName = "Price Labels", Order = 1)]
		public bool ShowPriceLabels { get; set; }

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Label Background", GroupName = "Price Labels", Order = 2)]
		public WpfBrush LabelBackgroundBrush { get; set; }

		[Browsable(false)]
		public string LabelBackgroundBrushSerialize
		{
			get { return Serialize.BrushToString(LabelBackgroundBrush); }
			set { LabelBackgroundBrush = Serialize.StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Label Text Color", GroupName = "Price Labels", Order = 3)]
		public WpfBrush LabelTextBrush { get; set; }

		[Browsable(false)]
		public string LabelTextBrushSerialize
		{
			get { return Serialize.BrushToString(LabelTextBrush); }
			set { LabelTextBrush = Serialize.StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[Range(8, 24)]
		[Display(Name = "Label Font Size", GroupName = "Price Labels", Order = 4)]
		public int LabelFontSize { get; set; }

		#endregion

		#region State Management

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Name					= "RedTail Trend Channel";
				Description				= @"Professional trend channel with midline, fill, extensions, and price labels.";
				DrawingState			= DrawingState.Building;
				IsAutoScale				= false;
				drawStep				= 0;

				Anchor1 = new ChartAnchor
				{
					IsEditing	= true,
					DrawingTool	= this
				};
				Anchor2 = new ChartAnchor
				{
					IsEditing	= true,
					DrawingTool	= this
				};
				Anchor3 = new ChartAnchor
				{
					IsEditing	= true,
					DrawingTool	= this
				};

				UpperLineBrush		= WpfBrushes.DodgerBlue;
				LowerLineBrush		= WpfBrushes.DodgerBlue;
				UpperLineWidth		= 2;
				LowerLineWidth		= 2;
				UpperLineDashStyle	= DashStyleHelper.Solid;
				LowerLineDashStyle	= DashStyleHelper.Solid;
				LineOpacity			= 100;

				ShowMidline			= true;
				MidLineBrush		= WpfBrushes.Gray;
				MidLineWidth		= 1;
				MidLineDashStyle	= DashStyleHelper.Dash;

				ShowFill			= true;
				FillBrush			= WpfBrushes.DodgerBlue;
				FillOpacity			= 12;

				ExtensionMode		= ChannelExtensionMode.None;
				ExtensionBrush		= WpfBrushes.DodgerBlue;
				ExtensionLineWidth	= 1;
				ExtensionDashStyle	= DashStyleHelper.Dot;
				ExtensionBars		= 0;

				ShowPriceLabels			= true;
				LabelBackgroundBrush	= WpfBrushes.Black;
				LabelTextBrush			= WpfBrushes.White;
				LabelFontSize			= 11;
			}
			else if (State == State.Terminated)
			{
				DisposeDeviceResources();
			}
		}

		#endregion

		#region Mouse Events - Building + Editing + Moving

		private ChartAnchor	editingAnchor;
		private ChartAnchor	lastMoveDataPoint;

		private ChartAnchor GetClosestAnchor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, WpfPoint point, int sensitivity)
		{
			ChartAnchor closest		= null;
			double		closestDist	= double.MaxValue;

			foreach (ChartAnchor anchor in Anchors)
			{
				WpfPoint ap = anchor.GetPoint(chartControl, chartPanel, chartScale);
				double dx = ap.X - point.X;
				double dy = ap.Y - point.Y;
				double dist = Math.Sqrt(dx * dx + dy * dy);

				if (dist <= sensitivity && dist < closestDist)
				{
					closestDist	= dist;
					closest		= anchor;
				}
			}

			return closest;
		}

		private bool IsPointNearLine(WpfPoint point, Vector2 lineStart, Vector2 lineEnd, int sensitivity)
		{
			Vector2 p	= new Vector2((float)point.X, (float)point.Y);
			Vector2 ab	= lineEnd - lineStart;
			float	len	= ab.Length();
			if (len < 0.001f) return false;

			float t = Vector2.Dot(p - lineStart, ab) / (len * len);
			t = Math.Max(0f, Math.Min(1f, t));
			Vector2 closest	= lineStart + ab * t;
			float	dist	= (p - closest).Length();
			return dist <= sensitivity;
		}

		private bool IsPointNearChannel(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, WpfPoint point)
		{
			if (drawStep < 2) return false;

			GetChannelScreenPoints(chartControl, chartPanel, chartScale, out Vector2 u1, out Vector2 u2, out Vector2 l1, out Vector2 l2);

			if (IsPointNearLine(point, u1, u2, cursorSensitivity))
				return true;
			if (IsPointNearLine(point, l1, l2, cursorSensitivity))
				return true;
			if (ShowMidline && IsPointNearLine(point, Mid(u1, l1), Mid(u2, l2), cursorSensitivity))
				return true;

			return false;
		}

		public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, WpfPoint point)
		{
			switch (DrawingState)
			{
				case DrawingState.Building:
					return Cursors.Pen;
				case DrawingState.Editing:
					return Cursors.SizeAll;
				case DrawingState.Moving:
					return Cursors.SizeAll;
			}

			// When selected, provide visual feedback on hover
			if (IsSelected)
			{
				if (GetClosestAnchor(chartControl, chartPanel, chartScale, point, cursorSensitivity) != null)
					return Cursors.SizeAll;
				if (IsPointNearChannel(chartControl, chartPanel, chartScale, point))
					return Cursors.SizeAll;
			}

			return null;
		}

		public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
		{
			// === BUILDING: 3-click placement ===
			if (DrawingState == DrawingState.Building)
			{
				switch (drawStep)
				{
					case 0:
						dataPoint.CopyDataValues(Anchor1);
						dataPoint.CopyDataValues(Anchor2);
						dataPoint.CopyDataValues(Anchor3);
						Anchor1.IsEditing = false;
						drawStep = 1;
						break;
					case 1:
						dataPoint.CopyDataValues(Anchor2);
						dataPoint.CopyDataValues(Anchor3);
						Anchor2.IsEditing = false;
						drawStep = 2;
						break;
					case 2:
						dataPoint.CopyDataValues(Anchor3);
						Anchor3.IsEditing = false;
						drawStep = 3;
						DrawingState = DrawingState.Normal;
						IsSelected = false;
						break;
				}
				return;
			}

			// === NORMAL: user clicked us while selected -> figure out what to do ===
			if (DrawingState == DrawingState.Normal && IsSelected)
			{
				WpfPoint wp = dataPoint.GetPoint(chartControl, chartPanel, chartScale);

				// Check if clicking near an anchor -> enter Editing for that anchor
				ChartAnchor closest = GetClosestAnchor(chartControl, chartPanel, chartScale, wp, cursorSensitivity);
				if (closest != null)
				{
					editingAnchor	= closest;
					editingAnchor.IsEditing = true;
					DrawingState	= DrawingState.Editing;
					return;
				}

				// Check if clicking near the channel body -> enter Moving
				if (IsPointNearChannel(chartControl, chartPanel, chartScale, wp))
				{
					editingAnchor	= null;
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
			if (DrawingState == DrawingState.Building)
			{
				if (drawStep == 1)
				{
					dataPoint.CopyDataValues(Anchor2);
					dataPoint.CopyDataValues(Anchor3);
				}
				else if (drawStep == 2)
				{
					dataPoint.CopyDataValues(Anchor3);
				}
				return;
			}

			// === EDITING: drag the single anchor that's being edited ===
			if (DrawingState == DrawingState.Editing && editingAnchor != null)
			{
				dataPoint.CopyDataValues(editingAnchor);
				return;
			}

			// === MOVING: translate all anchors by delta ===
			if (DrawingState == DrawingState.Moving && lastMoveDataPoint != null)
			{
				ChartPanel panel = chartControl.ChartPanels[chartScale.PanelIndex];

				foreach (ChartAnchor anchor in Anchors)
					anchor.MoveAnchor(lastMoveDataPoint, dataPoint, chartControl, panel, chartScale, this);

				dataPoint.CopyDataValues(lastMoveDataPoint);
				return;
			}
		}

		public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
		{
			// === BUILDING: nothing to do ===
			if (DrawingState == DrawingState.Building)
				return;

			// === End editing ===
			if (DrawingState == DrawingState.Editing)
			{
				if (editingAnchor != null)
				{
					editingAnchor.IsEditing = false;
					editingAnchor = null;
				}
				DrawingState = DrawingState.Normal;
				IsSelected = true;
				return;
			}

			// === End moving ===
			if (DrawingState == DrawingState.Moving)
			{
				lastMoveDataPoint = null;
				DrawingState = DrawingState.Normal;
				IsSelected = true;
				return;
			}
		}

		#endregion

		#region Selection Points - Critical for NT8 Selection

		public override WpfPoint[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
		{
			ChartPanel panel = chartControl.ChartPanels[chartScale.PanelIndex];
			if (panel == null)
				return new WpfPoint[0];

			// Only the 3 real draggable anchors
			WpfPoint a1 = Anchor1.GetPoint(chartControl, panel, chartScale);
			WpfPoint a2 = Anchor2.GetPoint(chartControl, panel, chartScale);
			WpfPoint a3 = Anchor3.GetPoint(chartControl, panel, chartScale);

			return new WpfPoint[] { a1, a2, a3 };
		}

		#endregion

		#region Alert / Visibility

		public override bool IsAlertConditionTrue(AlertConditionItem conditionItem, Condition condition, ChartAlertValue[] values, ChartControl chartControl, ChartScale chartScale)
		{
			return false;
		}

		public override bool IsVisibleOnChart(ChartControl chartControl, ChartScale chartScale, DateTime firstTimeOnChart, DateTime lastTimeOnChart)
		{
			if (DrawingState == DrawingState.Building)
				return true;

			DateTime minTime = new DateTime(Math.Min(Anchor1.Time.Ticks, Math.Min(Anchor2.Time.Ticks, Anchor3.Time.Ticks)));
			DateTime maxTime = new DateTime(Math.Max(Anchor1.Time.Ticks, Math.Max(Anchor2.Time.Ticks, Anchor3.Time.Ticks)));

			if (ExtensionMode == ChannelExtensionMode.Right || ExtensionMode == ChannelExtensionMode.Both)
				return minTime <= lastTimeOnChart;
			if (ExtensionMode == ChannelExtensionMode.Left || ExtensionMode == ChannelExtensionMode.Both)
				return maxTime >= firstTimeOnChart;

			return maxTime >= firstTimeOnChart && minTime <= lastTimeOnChart;
		}

		#endregion

		#region Channel Geometry

		private void GetChannelScreenPoints(ChartControl cc, ChartPanel cp, ChartScale cs,
			out Vector2 upper1, out Vector2 upper2, out Vector2 lower1, out Vector2 lower2)
		{
			WpfPoint p1 = Anchor1.GetPoint(cc, cp, cs);
			WpfPoint p2 = Anchor2.GetPoint(cc, cp, cs);
			WpfPoint p3 = Anchor3.GetPoint(cc, cp, cs);

			Vector2 v1 = new Vector2((float)p1.X, (float)p1.Y);
			Vector2 v2 = new Vector2((float)p2.X, (float)p2.Y);
			Vector2 v3 = new Vector2((float)p3.X, (float)p3.Y);

			Vector2 dir = v2 - v1;
			float len = dir.Length();
			if (len < 0.001f)
			{
				upper1 = upper2 = lower1 = lower2 = v1;
				return;
			}

			Vector2 unitDir = dir / len;
			Vector2 perp = new Vector2(-unitDir.Y, unitDir.X);
			float signedDist = Vector2.Dot(v3 - v1, perp);
			Vector2 offset = perp * signedDist;

			upper1 = v1;
			upper2 = v2;
			lower1 = v1 + offset;
			lower2 = v2 + offset;
		}

		private void GetExtendedPoints(ChartControl cc, ChartPanel cp, ChartScale cs,
			out Vector2 u1E, out Vector2 u2E, out Vector2 l1E, out Vector2 l2E)
		{
			GetChannelScreenPoints(cc, cp, cs, out Vector2 u1, out Vector2 u2, out Vector2 l1, out Vector2 l2);
			u1E = u1; u2E = u2; l1E = l1; l2E = l2;

			if (ExtensionMode == ChannelExtensionMode.None)
				return;

			Vector2 dir = u2 - u1;
			float len = dir.Length();
			if (len < 0.001f) return;

			Vector2 unitDir = dir / len;
			float extDist = (ExtensionBars > 0)
				? ExtensionBars * Math.Max(cc.Properties.BarDistance, 5f)
				: cp.W + cp.H;

			if (ExtensionMode == ChannelExtensionMode.Right || ExtensionMode == ChannelExtensionMode.Both)
			{
				u2E = u2 + unitDir * extDist;
				l2E = l2 + unitDir * extDist;
			}
			if (ExtensionMode == ChannelExtensionMode.Left || ExtensionMode == ChannelExtensionMode.Both)
			{
				u1E = u1 - unitDir * extDist;
				l1E = l1 - unitDir * extDist;
			}
		}

		#endregion

		#region Rendering

		public override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			// After building completes or after deserialization, always treat as complete
			// The anchors having valid data is the real indicator that the channel is fully placed
			if (DrawingState != DrawingState.Building && drawStep < 3)
				drawStep = 3;

			if (drawStep < 1) return;

			RenderTarget rt = RenderTarget;
			ChartPanel panel = chartControl.ChartPanels[chartScale.PanelIndex];
			if (rt == null || panel == null) return;

			// Rebuild DX resources when render target changes (happens on property edits, resize, etc.)
			if (rt != lastRenderTarget)
			{
				RebuildDeviceResources(rt);
				lastRenderTarget = rt;
			}

			GetChannelScreenPoints(chartControl, panel, chartScale, out Vector2 u1, out Vector2 u2, out Vector2 l1, out Vector2 l2);

			// ---- Channel body fill (Anchor1 to Anchor2 ONLY) ----
			if (ShowFill && fillBrushDx != null && drawStep >= 2)
				DrawQuadFill(rt, u1, u2, l2, l1);

			// ---- Extensions (lines + fill) only when enabled ----
			if (ExtensionMode != ChannelExtensionMode.None && drawStep >= 2)
			{
				GetExtendedPoints(chartControl, panel, chartScale, out Vector2 u1E, out Vector2 u2E, out Vector2 l1E, out Vector2 l2E);

				if (ShowFill && fillBrushDx != null)
				{
					if (ExtensionMode == ChannelExtensionMode.Right || ExtensionMode == ChannelExtensionMode.Both)
						DrawQuadFill(rt, u2, u2E, l2E, l2);
					if (ExtensionMode == ChannelExtensionMode.Left || ExtensionMode == ChannelExtensionMode.Both)
						DrawQuadFill(rt, u1E, u1, l1, l1E);
				}

				if (extensionBrushDx != null)
				{
					if (ExtensionMode == ChannelExtensionMode.Right || ExtensionMode == ChannelExtensionMode.Both)
					{
						rt.DrawLine(u2, u2E, extensionBrushDx, ExtensionLineWidth, extStrokeStyleDx);
						rt.DrawLine(l2, l2E, extensionBrushDx, ExtensionLineWidth, extStrokeStyleDx);
						if (ShowMidline)
							rt.DrawLine(Mid(u2, l2), Mid(u2E, l2E), extensionBrushDx, 1, extStrokeStyleDx);
					}
					if (ExtensionMode == ChannelExtensionMode.Left || ExtensionMode == ChannelExtensionMode.Both)
					{
						rt.DrawLine(u1E, u1, extensionBrushDx, ExtensionLineWidth, extStrokeStyleDx);
						rt.DrawLine(l1E, l1, extensionBrushDx, ExtensionLineWidth, extStrokeStyleDx);
						if (ShowMidline)
							rt.DrawLine(Mid(u1E, l1E), Mid(u1, l1), extensionBrushDx, 1, extStrokeStyleDx);
					}
				}
			}

			// ---- Upper line ----
			if (upperLineBrushDx != null && drawStep >= 1)
				rt.DrawLine(u1, u2, upperLineBrushDx, UpperLineWidth, upperStrokeStyleDx);

			// ---- Lower line ----
			if (lowerLineBrushDx != null && drawStep >= 2)
				rt.DrawLine(l1, l2, lowerLineBrushDx, LowerLineWidth, lowerStrokeStyleDx);

			// ---- Midline ----
			if (ShowMidline && midLineBrushDx != null && drawStep >= 2)
				rt.DrawLine(Mid(u1, l1), Mid(u2, l2), midLineBrushDx, MidLineWidth, midStrokeStyleDx);

			// ---- Anchor handles when selected ----
			if (IsSelected || DrawingState == DrawingState.Building || DrawingState == DrawingState.Editing)
			{
				// Main draggable anchors - large, prominent
				foreach (ChartAnchor anchor in Anchors)
				{
					WpfPoint ap = anchor.GetPoint(chartControl, panel, chartScale);
					Vector2 av = new Vector2((float)ap.X, (float)ap.Y);
					SharpDX.Direct2D1.Ellipse ellipse = new SharpDX.Direct2D1.Ellipse(av, 6f, 6f);
					using (DxSolidBrush fillBr = new DxSolidBrush(rt, new Color4(1f, 1f, 1f, 1f)))
						rt.FillEllipse(ellipse, fillBr);
					using (DxSolidBrush ringBr = new DxSolidBrush(rt, new Color4(0.12f, 0.56f, 1f, 1f)))
						rt.DrawEllipse(ellipse, ringBr, 2f);
				}
			}

			// ---- Price labels ----
			if (ShowPriceLabels && drawStep >= 2 && labelTextFormatDx != null)
			{
				double upperPrice	= Anchor2.Price;
				double priceDelta	= Anchor3.Price - ProjectedPrice(Anchor3.Time, Anchor1, Anchor2);
				double lowerPrice	= upperPrice + priceDelta;
				double midPrice		= (upperPrice + lowerPrice) * 0.5;

				int decimals = 2;
				try
				{
					if (chartControl.Instrument != null && chartControl.Instrument.MasterInstrument != null)
						decimals = (int)Math.Max(0, Math.Ceiling(-Math.Log10(chartControl.Instrument.MasterInstrument.TickSize)));
				}
				catch { }

				string fmt = "F" + decimals;

				Vector2 labelUpper = u2;
				Vector2 labelLower = l2;
				if (ExtensionMode == ChannelExtensionMode.Right || ExtensionMode == ChannelExtensionMode.Both)
				{
					GetExtendedPoints(chartControl, panel, chartScale, out Vector2 eu1, out Vector2 eu2, out Vector2 el1, out Vector2 el2);
					labelUpper = eu2;
					labelLower = el2;
				}

				RenderPriceLabel(rt, labelUpper, upperPrice.ToString(fmt), panel);
				RenderPriceLabel(rt, labelLower, lowerPrice.ToString(fmt), panel);
				if (ShowMidline)
					RenderPriceLabel(rt, Mid(labelUpper, labelLower), midPrice.ToString(fmt), panel);
			}
		}

		private void DrawQuadFill(RenderTarget rt, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
		{
			if (fillBrushDx == null) return;
			using (DxPathGeometry geo = new DxPathGeometry(rt.Factory))
			{
				using (GeometrySink sink = geo.Open())
				{
					sink.BeginFigure(a, FigureBegin.Filled);
					sink.AddLine(b);
					sink.AddLine(c);
					sink.AddLine(d);
					sink.EndFigure(FigureEnd.Closed);
					sink.Close();
				}
				rt.FillGeometry(geo, fillBrushDx);
			}
		}

		private void RenderPriceLabel(RenderTarget rt, Vector2 pos, string text, ChartPanel panel)
		{
			if (labelBgBrushDx == null || labelTextBrushDx == null || labelTextFormatDx == null) return;

			float pad = 4f;
			float xOff = 8f;

			using (SharpDX.DirectWrite.TextLayout layout = new SharpDX.DirectWrite.TextLayout(
				Core.Globals.DirectWriteFactory, text, labelTextFormatDx, 200f, 30f))
			{
				SharpDX.DirectWrite.TextMetrics m = layout.Metrics;
				float w	= m.Width + pad * 2;
				float h	= m.Height + pad * 2;
				float x	= pos.X + xOff;
				float y	= pos.Y - h * 0.5f;

				if (x + w > panel.X + panel.W)
					x = pos.X - xOff - w;

				SharpDX.RectangleF rect = new SharpDX.RectangleF(x, y, w, h);
				rt.FillRoundedRectangle(new RoundedRectangle { Rect = rect, RadiusX = 3f, RadiusY = 3f }, labelBgBrushDx);
				rt.DrawText(text, labelTextFormatDx, new SharpDX.RectangleF(x + pad, y + pad, m.Width, m.Height), labelTextBrushDx);
			}
		}

		private static Vector2 Mid(Vector2 a, Vector2 b)
		{
			return (a + b) * 0.5f;
		}

		private static double ProjectedPrice(DateTime time, ChartAnchor a1, ChartAnchor a2)
		{
			if (a1.Time == a2.Time) return a1.Price;
			double frac = (time - a1.Time).TotalSeconds / (a2.Time - a1.Time).TotalSeconds;
			return a1.Price + frac * (a2.Price - a1.Price);
		}

		#endregion

		#region Device Resource Management

		private void RebuildDeviceResources(RenderTarget rt)
		{
			DisposeDeviceResources();

			float lineAlpha = LineOpacity / 100f;
			float fillAlpha = FillOpacity / 100f;

			upperLineBrushDx	= WpfToDxBrush(rt, UpperLineBrush, lineAlpha);
			lowerLineBrushDx	= WpfToDxBrush(rt, LowerLineBrush, lineAlpha);
			midLineBrushDx		= WpfToDxBrush(rt, MidLineBrush, 1f);
			fillBrushDx			= WpfToDxBrush(rt, FillBrush, fillAlpha);
			extensionBrushDx	= WpfToDxBrush(rt, ExtensionBrush, lineAlpha * 0.6f);
			labelBgBrushDx		= WpfToDxBrush(rt, LabelBackgroundBrush, 0.85f);
			labelTextBrushDx	= WpfToDxBrush(rt, LabelTextBrush, 1f);

			upperStrokeStyleDx	= BuildStrokeStyle(UpperLineDashStyle);
			lowerStrokeStyleDx	= BuildStrokeStyle(LowerLineDashStyle);
			midStrokeStyleDx	= BuildStrokeStyle(MidLineDashStyle);
			extStrokeStyleDx	= BuildStrokeStyle(ExtensionDashStyle);

			labelTextFormatDx = new SharpDX.DirectWrite.TextFormat(
				Core.Globals.DirectWriteFactory, "Consolas",
				SharpDX.DirectWrite.FontWeight.Normal,
				SharpDX.DirectWrite.FontStyle.Normal,
				LabelFontSize);
		}

		private static DxBrush WpfToDxBrush(RenderTarget rt, WpfBrush wpf, float alpha)
		{
			if (wpf == null) return null;
			WpfColor c = (wpf is WpfSolidBrush scb) ? scb.Color : WpfColors.White;
			return new DxSolidBrush(rt, new Color4(c.R / 255f, c.G / 255f, c.B / 255f, alpha));
		}

		private static DxStrokeStyle BuildStrokeStyle(DashStyleHelper dash)
		{
			SharpDX.Direct2D1.DashStyle dx;
			switch (dash)
			{
				case DashStyleHelper.Dash:			dx = SharpDX.Direct2D1.DashStyle.Dash; break;
				case DashStyleHelper.DashDot:		dx = SharpDX.Direct2D1.DashStyle.DashDot; break;
				case DashStyleHelper.DashDotDot:	dx = SharpDX.Direct2D1.DashStyle.DashDotDot; break;
				case DashStyleHelper.Dot:			dx = SharpDX.Direct2D1.DashStyle.Dot; break;
				default:							dx = SharpDX.Direct2D1.DashStyle.Solid; break;
			}
			return new DxStrokeStyle(Core.Globals.D2DFactory, new StrokeStyleProperties
			{
				DashStyle	= dx,
				StartCap	= CapStyle.Round,
				EndCap		= CapStyle.Round
			});
		}

		private void DisposeDeviceResources()
		{
			if (upperLineBrushDx != null)	{ upperLineBrushDx.Dispose();	upperLineBrushDx = null; }
			if (lowerLineBrushDx != null)	{ lowerLineBrushDx.Dispose();	lowerLineBrushDx = null; }
			if (midLineBrushDx != null)		{ midLineBrushDx.Dispose();		midLineBrushDx = null; }
			if (fillBrushDx != null)		{ fillBrushDx.Dispose();		fillBrushDx = null; }
			if (extensionBrushDx != null)	{ extensionBrushDx.Dispose();	extensionBrushDx = null; }
			if (labelBgBrushDx != null)		{ labelBgBrushDx.Dispose();		labelBgBrushDx = null; }
			if (labelTextBrushDx != null)	{ labelTextBrushDx.Dispose();	labelTextBrushDx = null; }
			if (upperStrokeStyleDx != null)	{ upperStrokeStyleDx.Dispose();	upperStrokeStyleDx = null; }
			if (lowerStrokeStyleDx != null)	{ lowerStrokeStyleDx.Dispose();	lowerStrokeStyleDx = null; }
			if (midStrokeStyleDx != null)	{ midStrokeStyleDx.Dispose();	midStrokeStyleDx = null; }
			if (extStrokeStyleDx != null)	{ extStrokeStyleDx.Dispose();	extStrokeStyleDx = null; }
			if (labelTextFormatDx != null)	{ labelTextFormatDx.Dispose();	labelTextFormatDx = null; }

			lastRenderTarget = null;
		}

		#endregion
	}
}