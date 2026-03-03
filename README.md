<p align="center">
  <img src="https://avatars.githubusercontent.com/u/209633456?v=4" width="160" alt="RedTail Indicators Logo"/>
</p>

<h1 align="center">RedTail Toolbar & Drawing Tools</h1>

<p align="center">
  <b>A custom chart toolbar and suite of 8 drawing tools for NinjaTrader 8.</b><br>
  One-click access to drawing tools, chart utilities, and features that NinjaTrader doesn't ship out of the box.
</p>

<p align="center">
  <a href="https://buymeacoffee.com/dmwyzlxstj">
    <img src="https://img.shields.io/badge/☕_Buy_Me_a_Coffee-FFDD00?style=flat-square&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me a Coffee"/>
  </a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/3astbeast/RedTail-Toolbar/refs/heads/main/Screenshot%202026-03-03%20182536.pngg" width="800" alt="RedTail Toolbar Screenshot"/>
</p>



---

## Overview

RedTail Toolbar installs a persistent toolbar row at the top of your chart with quick-access buttons for NinjaTrader's drawing tools, plus built-in chart utilities like a lag timer, ATR display, break even button, pan mode, indicator visibility manager, command center, screenshot button, timeframe switcher, and drawing management controls. It also includes 8 custom drawing tools that extend NinjaTrader's default toolset with features commonly found in platforms like TradingView.

---

## Toolbar

The toolbar renders as a dark, compact strip at the top of the chart with color-coded icon buttons organized by category (lines, shapes, fibs, text, custom tools).

### Drawing Tool Buttons

One-click buttons that activate any of NinjaTrader's built-in or custom drawing tools. The toolbar automatically discovers all available drawing tools from NinjaTrader's menu system.

- **Configurable tool selection** — Open the settings panel (⚙) to choose which drawing tools appear in the toolbar via checkboxes. Your selection is saved to a file (`RedTailToolbarSettings.txt`) and persists across sessions.
- **Color-coded icons** — Tools are categorized by type with distinct icon colors: cyan for lines, yellow for shapes, purple for fibs, gray for text, orange for custom tools.

### Drawing Management

- **👁 Hide/Show All Drawings** — Toggle visibility of all drawing objects on the chart with one click
- **🔓 Lock/Unlock All Drawings** — Lock all drawings to prevent accidental moves during live trading
- **🗑 Delete All Drawings** — Remove all drawing objects from the chart (with caution — this is destructive)

### Indicator Visibility Manager

A layers-style button that opens a popup listing all indicators currently on the chart. Toggle each indicator on or off individually without removing it from the chart. Visibility state is persisted to a file (`RedTailIndicatorVisibility.txt`) so your hidden indicators stay hidden across sessions and chart reloads. The manager also toggles the visibility of drawing objects created by each indicator.

### Command Center

A live configuration panel for all RedTail indicators on the chart. Click the Command Center button to open a window with a dropdown selector listing every RedTail indicator currently loaded on the chart. Select one to see all of its configurable properties organized by group — booleans, numbers, colors, enums, and text fields are all rendered with appropriate input controls. Adjust any setting and click Apply to push the changes to the indicator in real-time without removing and re-adding it through NinjaTrader's Indicators dialog. Supports brush/color properties with proper frozen brush handling and serializable color backing.

### Lag Timer

A real-time latency monitor that measures the delay between exchange timestamps and your local clock. Updates continuously during live trading.

- **Green** — Lag below the warning threshold (default: 0.5s)
- **Orange** — Lag between warning and critical thresholds
- **Red** — Lag above the critical threshold (default: 2.0s)
- Displays "NO DATA" if no market data received in 30 seconds, "HIST" during historical playback, "OFF" when disconnected
- Warning and critical thresholds are configurable

### ATR Display

Shows the current ATR value in the toolbar, updated on each bar close. Configurable period (default: 14). Automatically adjusts decimal precision based on the value (4 decimals for small values like forex, 2 for larger values like indices).

### Break Even Button

One-click button to move your stop loss to breakeven on the current instrument.

- Automatically finds your open position and connected account
- Supports an optional tick offset (e.g., BE+2 moves the stop 2 ticks past entry in your favor)
- Only moves the stop toward profit — will not move a stop further from breakeven
- Validates that the position is in enough profit before modifying
- Modifies both StopMarket and StopLimit order types
- Visual feedback: flashes green on success, red on failure
- Label shows "BE" or "BE+N" depending on the offset setting

### Pan Mode

Toggle free chart panning without holding the Ctrl key. When enabled, left-click dragging scrolls the chart instead of drawing. Useful when you want to quickly navigate through history without accidentally placing drawing tools. The button highlights yellow when active. Simulates the Ctrl key via P/Invoke to leverage NinjaTrader's built-in pan behavior.

### Screenshot Button

One-click chart screenshot that captures the full chart window (including DX-rendered content) via screen BitBlt and saves it as a PNG file.

- Files are named automatically: `Instrument_Timeframe_Timestamp.png`
- Configurable save folder (default: My Pictures / RedTail Screenshots)
- Button flashes to confirm the screenshot was captured
- Creates the save folder automatically if it doesn't exist

### Timeframe Switcher

A row of quick-access buttons for switching the chart's timeframe without navigating NinjaTrader's interval menus. The active timeframe is highlighted.

Configure the available timeframes with a comma-separated list in the settings. Plain numbers are interpreted as minutes, and suffixes control the bar type:

| Suffix | Bar Type | Example | Display |
|:---:|---|---|---|
| *(none)* | Minute | `5` | 5m |
| `s` | Second | `30s` | 30s |
| `t` | Tick | `386t` | 386T |
| `r` | Range | `4r` | 4R |
| `rn` | Renko | `4rn` | 4Rn |
| `d` | Daily | `d` | D |
| `w` | Weekly | `w` | W |

Minutes ≥ 60 are automatically displayed as hours (e.g., `60` → `1H`, `240` → `4H`).

**Default list:** `1,3,5,15,60`

**Example with mixed types:** `30s,1,3,5,15,60,240,386t,4r,4rn,d,w`

---

## Toolbar Settings

All toolbar features can be toggled on or off individually:

- **Toolbar Height** — Pixel height of the toolbar row
- **Button Size** — Pixel size for tool buttons
- **Show Lag Timer** / **Lag Warning** / **Lag Critical** — Enable and configure lag thresholds
- **Show ATR** / **ATR Period** — Enable and configure ATR display
- **Show Break Even Button** / **Break Even Offset (ticks)** — Enable and configure the BE button (0 = true breakeven)
- **Show Pan Button** — Enable the pan mode toggle
- **Show Indicator Manager** — Enable the indicator visibility button
- **Show Command Center** — Enable the live indicator configuration panel
- **Show Screenshot Button** / **Screenshot Folder** — Enable screenshots and set the save location
- **Show Timeframe Switcher** / **Timeframe List** — Enable quick timeframe buttons and configure the available intervals

---

## Drawing Tools

The following 8 custom drawing tools are included. Once installed, they appear in NinjaTrader's Drawing Tools menu and can be added to the toolbar like any other tool.

---

### RedTail FRVP Fib

Click two anchor points to define a range, and the tool builds a full Fixed Range Volume Profile with Fibonacci levels, Anchored VWAP, and K-Means cluster detection inside the zone.

**Volume Profile**
- Configurable number of rows, profile width %, and alignment (Left/Right)
- Volume types: Standard, Bullish, Bearish, or Both (polarity coloring)
- Gradient fill with configurable intensity
- Adaptive rendering with Gaussian smoothing and min/max bar pixel height
- Boundary outline with independent color, opacity, and width

**POC & Value Area**
- Point of Control with color, width, style, and opacity
- Value Area with configurable percentage, VA bar color, VA lines, and right extension

**Fibonacci Retracements**
- Up to 10 customizable levels with per-level colors (set to -1 to disable)
- Optional right extension, price labels, and configurable label font size

**Anchored VWAP**
- AVWAP computed from the start anchor of the drawing range
- Optional right extension and label display

**K-Means Cluster Levels**
- Segments volume into 2–10 clusters to find high-volume nodes at different price regions
- Configurable iterations, rows per cluster, line width, style, and opacity
- Up to 10 independently colored cluster levels with optional labels and right extension

---

### RedTail AVWAP

A standalone Anchored VWAP drawing tool. Click on any candle to anchor the VWAP from that point forward — the line extends to the right edge of the chart and updates in real-time as new bars form.

**VWAP Line**
- Configurable color, line width, line style, and opacity
- VWAP source selection: OHLC4 (default), HLC3, HL2, or Close
- Optional label at the end of the line showing "AVWAP" and the current value
- Configurable label font size
- Diamond-shaped anchor marker at the origin point

**Standard Deviation Bands**
- 3 independently configurable standard deviation bands
- Each band has its own: show/hide toggle, multiplier (0.1–10.0), color, opacity, line width, and line style
- Default multipliers: 1.0, 2.0, 3.0
- All bands disabled by default

**Interaction**
- Click to place the anchor, drag to reposition
- The VWAP recalculates automatically when the anchor is moved or new bars arrive
- Renders with SharpDX for performance, only drawing visible segments

---

### RedTail Trend Channel

A professional parallel channel drawing tool with a 3-click placement workflow. Click to set the first anchor of the upper line, click again to complete the upper line, then click a third time to set the channel width — the lower line is drawn parallel to the upper automatically.

**Channel Lines**
- Independent upper and lower line colors, widths, and dash styles
- Configurable line opacity (1–100%)

**Midline**
- Optional dashed midline drawn at the center of the channel
- Independent color, width, and dash style

**Channel Fill**
- Optional fill between the upper and lower lines
- Configurable fill color and opacity

**Extensions**
- Extension modes: None, Right, Left, or Both
- Extensions project the channel lines beyond the anchor points at the same angle
- Configurable extension bar count (0 = infinite, extends to edge of chart)
- Independent extension color, line width, and dash style
- Fill and midline extend into the extension zones as well

**Price Labels**
- Optional price labels at the end of the upper, lower, and midline (when midline is enabled)
- Labels display with a rounded background and auto-flip to the left side when near the chart edge
- Auto-detects decimal precision from the instrument's tick size
- Configurable label background color, text color, and font size

**Interaction**
- After placement, all three anchors are individually draggable via NinjaTrader's native selection handling
- Anchor handles render as white circles with blue rings when selected
- Full selection point coverage along upper, lower, and midline for easy click-to-select

---

### RedTail VP Zone

Draw a selection rectangle on the chart to generate a volume profile within that zone.

- Volume profile histogram with configurable rows, alignment, and volume type (Standard/Bullish/Bearish/Both)
- POC line with configurable color, thickness, and style
- VAH/VAL lines with independent colors, thickness, and style
- Zone fill with configurable color and opacity
- Optional selection rectangle outline
- Range High/Low extension lines with independent colors, thickness, and style
- Useful for quick ad-hoc volume analysis on any price range

---

### RedTail MTF Fib

A multi-timeframe Fibonacci retracement drawing tool. Click two points to set your swing, then tag it with a timeframe label.

- **Timeframe labels:** Daily, Weekly, 4H, 1H, 30m, 15m, 5m, or Custom
- Up to 7 customizable Fib levels with per-level colors (set to -1 to disable)
- Optional price display on labels
- Configurable label font size, line width, line style, and opacity
- Optional right extension
- Anchor line with independent color and width
- Timeframe label makes it easy to distinguish overlapping fibs from different timeframes

---

### RedTail Horizontal Line

An enhanced horizontal line drawing tool with labels and quick price editing.

- **Text label** with configurable position: Left, Right, Center, Above, or Below
- **Show Price** option to display the numerical price next to the label
- **Extend Right / Extend Left** — independently toggle each direction
- **Hover edit button** — when your mouse is near the line, a small edit button appears. Click it to open a popup for precise price adjustment.
- Configurable line color, text color, dash style, width, opacity, and font size

---

### RedTail Rectangle

A feature-rich rectangle drawing tool with mid-line, extension lines, gradient fill, and labels.

**Border**
- Configurable color, width, opacity, and dash style

**Mid Line**
- Optional horizontal line at the midpoint of the rectangle
- Independent color, width, opacity, and dash style
- Can extend left and/or right beyond the rectangle

**Extension Lines**
- Top and bottom border lines can independently extend left and/or right
- Extension lines have their own color, width, dash style, and opacity

**Fill**
- Fill modes: Solid, Gradient, or None
- Gradient mode blends between two configurable colors
- Configurable fill opacity

**Label**
- Optional text label with configurable text, position, and color

---

### RedTail Measure Tool

A comprehensive measurement drawing tool that displays detailed trade and market statistics for any selected range.

**Measurements Displayed:**
- Bars & Time elapsed
- Price change in Points and Ticks
- Dollar Value of the move
- Percentage change
- Velocity (Ticks per Bar)
- Dollars per Minute
- Net P&L after commission
- Volume within the range
- Delta (buy vs. sell volume)
- Average Volume per Bar

**Trade Settings:**
- Configurable number of contracts for P&L calculation
- Configurable round-trip commission per contract

**Visual:**
- Auto-colors green for long (upward) and red for short (downward) measurements
- Configurable fill colors, border, text color, text background, and opacity for each
- Text placement inside or outside the measurement zone
- Optional diagonal line connecting the anchors
- Configurable font size and border width

---

## Installation

1. Download all `.cs` files from this repository (toolbar + drawing tools)
2. Open NinjaTrader 8
3. Go to **Tools → Import → NinjaScript Add-On**
4. Import each file — the toolbar will appear in your **Indicators** list and the drawing tools will appear in the **Drawing Tools** menu
5. Add the toolbar indicator to any chart and configure which tools appear via the ⚙ settings button

---

## Part of the RedTail Indicators Suite

This indicator is part of the [RedTail Indicators](https://github.com/3astbeast/RedTailIndicators) collection — free NinjaTrader 8 tools built for futures traders who demand precision.

---

<p align="center">
  <a href="https://buymeacoffee.com/dmwyzlxstj">
    <img src="https://img.shields.io/badge/☕_Buy_Me_a_Coffee-Support_My_Work-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me a Coffee"/>
  </a>
</p>
