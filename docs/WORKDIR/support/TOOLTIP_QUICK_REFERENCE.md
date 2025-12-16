# EA Tooltip System - Quick Reference Guide

## System Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    MOUSE CURSOR TRACKING                     │
│  ├─ Position: m_currMouse.pos.x/y                           │
│  ├─ Stillness: m_stillTime (delta movement)                 │
│  └─ Movement: m_deltaPos triggers delay reset               │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│           GAMEWINDOWMANAGER TOOLTIP DETECTION               │
│  ├─ Finds window under cursor (ABOVE/NORMAL/BELOW layers)  │
│  ├─ Searches hierarchy for tooltip content                 │
│  ├─ Calls tooltip callback OR static text setter           │
│  └─ Records window context                                  │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│                  DELAY MECHANISM                             │
│  (now - m_stillTime) >= delay_time ?                         │
│  ├─ delay_time = m_tooltipDelay (if >= 0)                  │
│  │                OR m_tooltipDelayTime (default 50ms)      │
│  ├─ On movement: m_stillTime = now (reset)                 │
│  └─ Once met: m_displayTooltip = TRUE                       │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│                 ANIMATION & RENDERING                        │
│  if (m_displayTooltip && text && !empty && display_ok)      │
│  ├─ Position: (cursor+20, cursor_y) with edge wrapping     │
│  ├─ Background: animated fill OR solid                      │
│  ├─ Animation: highlight sweep (0 to width+15px)           │
│  ├─ Duration: m_tooltipFillTime (default 50ms)             │
│  └─ Colors: text, highlight, shadow, background, border    │
└─────────────────────────────────────────────────────────────┘
```

## 1. Delay System Flow

```
IDLE STATE: m_displayTooltip = FALSE
   │
   ├─ Mouse enters tooltip window
   │  m_stillTime = now
   │
   └─ Waiting: (now - m_stillTime) < delay
      │
      └─ Any movement: m_stillTime = now (RESTART)
         
SHOW STATE: m_displayTooltip = TRUE
   │
   ├─ Animation runs for m_tooltipFillTime
   │  m_highlightPos advances from 0 to width+15
   │
   └─ Movement resets to IDLE
      m_stillTime = now, delay counter restarts
```

## 2. Window-Level Tooltip Configuration

### Three Ways to Set Tooltips:

#### A) Static Text (WND File)
```
WINDOW MyButton BUTTON "MyButton"
{
    TOOLTIP "This is a tooltip"
    TOOLTIPDELAY 100  // Override default 50ms
}
```

#### B) Static Text (Code)
```cpp
GameWindow *button = ...;
button->winSetTooltip(L"My tooltip text");
button->setTooltipDelay(100);  // Optional override
```

#### C) Dynamic Callback
```cpp
// Define callback
void myTooltipFunc(GameWindow *win, WinInstanceData *instData, UnsignedInt mouse)
{
    // Generate dynamic tooltip content
    UnicodeString text = buildDynamicText(...);
    TheMouse->setCursorTooltip(text, -1);  // -1 = use default delay
}

// Register callback
button->winSetTooltipFunc(myTooltipFunc);
```

## 3. Tooltip Rendering Coordinates

```
Screen Layout:

TOP (0)
│
├─────────────────────────────────────
│ Tooltip (wrapped to fit)
│ ╔═══════════════════════════════════╗
│ ║ Text content word-wrapped here    ║  ← 20px right of cursor
│ ║ Optional animation highlight      ║
│ ╚═══════════════════════════════════╝
│   ↑
│   └─ Cursor position
│
│ If right edge exceeded:
│ flips to LEFT of cursor (xPos -= 20 + width)
│
│ If bottom exceeded:
│ flips ABOVE cursor (yPos -= height)

BOTTOM (m_maxY)
```

## 4. Color System

### Default Colors:
```cpp
m_tooltipColorText = {220, 220, 220, 255}       // Light gray
m_tooltipColorHighlight = {255, 255, 0, 255}    // Yellow
m_tooltipColorShadow = {0, 0, 0, 255}           // Black
m_tooltipColorBackground = {20, 20, 0, 127}     // Dark yellow, 50% alpha
m_tooltipColorBorder = {0, 0, 0, 255}           // Black
```

### House Color Support (Optional):
```cpp
// Enable in INI:
UseTooltipAltTextColor = true
UseTooltipAltBackColor = true
AdjustTooltipAltColor = true  // Brighten text, darken background

// Then call:
setCursorTooltip(text, delay, &houseColor, 1.0f);
```

## 5. INI Configuration Keys

```ini
[Mouse]
TooltipFontName = Times New Roman
TooltipFontSize = 12
TooltipFontIsBold = false

TooltipDelayTime = 50              // ms before showing
TooltipFillTime = 50               // animation duration (ms)
TooltipWidth = 15                  // % of screen width
TooltipAnimateBackground = true    // highlight sweep effect

TooltipTextColor = 255 255 255 255           // RGBA
TooltipHighlightColor = 255 255 0 255        // Yellow
TooltipShadowColor = 0 0 0 255                // Black
TooltipBackgroundColor = 20 20 0 127         // Semi-transparent
TooltipBorderColor = 0 0 0 255

UseTooltipAltTextColor = false
UseTooltipAltBackColor = false
AdjustTooltipAltColor = false
```

## 6. State Transitions

```
State Diagram:

    [IDLE]
      │
      │ Mouse enters tooltip window
      │ (no movement for delay_time)
      ▼
    [DELAY_ACTIVE]
      │
      ├─ Movement detected ──────┐
      │                          │
      │                          ▼
      │ Delay elapsed ──────→ [DISPLAYING]
      │                          │
      │                          ├─ Animation runs
      │                          │  (m_highlightPos 0→width+15)
      │                          │
      │                          └─ Movement ─────┐
      │                                            │
      └────────────────────────────────────────────┘
```

## 7. Animation Timing

```
Animation Timeline:

Time 0ms: m_displayTooltip = TRUE
         m_highlightPos = 0
         m_highlightUpdateStart = now

         Every frame:
         m_highlightPos = (width * (now - start)) / fillTime

Time 50ms (fillTime): 
         m_highlightPos >= width + 15
         Animation complete, stops updating

During animation:
  Frame 0:   ▓░░░░░░░░░░░░░░░░░░░ (0%)
  Frame 1:   ▓▓░░░░░░░░░░░░░░░░░░ (5%)
  Frame 2:   ▓▓▓░░░░░░░░░░░░░░░░░ (10%)
  ...
  Frame 50:  ░░░░░░░░░░░░░░░░░░░▓ (100%+15px)
```

## 8. Key Methods Reference

### Mouse Class
```cpp
// Setup
void setCursorTooltip(UnicodeString text, Int delay = -1, 
                      const RGBColor *color = NULL, Real width = 1.0f);

// Rendering
void drawTooltip(void);
void drawCursorText(void);

// State control
void resetTooltipDelay(void);

// Accessors
Int getCursorTooltipDelay(void);
void setCursorTooltipDelay(Int delay);
```

### GameWindow Class
```cpp
// Set static text
void winSetTooltip(UnicodeString tip);

// Set callback
Int winSetTooltipFunc(GameWinTooltipFunc tooltip);
GameWinTooltipFunc winGetTooltipFunc(void);

// Delay override
Int getTooltipDelay(void);
void setTooltipDelay(Int delay);
```

### Callback Signature
```cpp
typedef void (*GameWinTooltipFunc)(GameWindow *, 
                                   WinInstanceData *, 
                                   UnsignedInt mouse);
```

## 9. Complete Example: Control Bar Tooltip

```cpp
// 1. Define callback (generate dynamic tooltip)
static void commandButtonTooltip(GameWindow *window,
                                WinInstanceData *instData,
                                UnsignedInt mouse)
{
    // Custom behavior: show popup layout instead of simple tooltip
    TheControlBar->showBuildTooltipLayout(window);
}

// 2. Register for buttons
void ControlBar::initCommandButtons()
{
    for (int i = 0; i < NUM_BUTTONS; i++)
    {
        m_commandButtons[i]->winSetTooltipFunc(commandButtonTooltip);
        m_commandButtons[i]->setTooltipDelay(200);  // 200ms override
    }
}

// 3. Callback Flow:
//    - User hovers over button
//    - GameWindowManager detects tooltip
//    - Calls commandButtonTooltip callback
//    - Callback shows ControlBarPopupDescription layout
//    - Layout updates every frame (ControlBarPopupDescriptionUpdateFunc)
//    - Disappears when mouse moves/leaves button
```

## 10. Testing Checklist

- [ ] Tooltip appears after correct delay
- [ ] Tooltip disappears on mouse movement
- [ ] Tooltip follows mouse cursor
- [ ] Tooltip wraps at right screen edge
- [ ] Tooltip wraps at bottom screen edge
- [ ] Animation highlight sweeps left-to-right
- [ ] Colors render correctly
- [ ] House colors apply (if enabled)
- [ ] Window-specific delays override global
- [ ] Callback functions fire at correct time
- [ ] Multiple windows show correct tooltips
- [ ] Window layer system (ABOVE/NORMAL/BELOW) respected
- [ ] Fade suppresses tooltip rendering
- [ ] Text is properly formatted and readable

---

## Quick Copy-Paste Snippets

### Basic Tooltip Setup (Code)
```cpp
window->winSetTooltip(L"Tooltip text here");
window->setTooltipDelay(100);  // 100ms delay
```

### Callback Setup
```cpp
window->winSetTooltipFunc(myCallbackFunction);
```

### Reset Tooltip Delay
```cpp
TheMouse->resetTooltipDelay();
```

### Dynamic Color Tooltip
```cpp
RGBColor houseColor = {255, 0, 0};  // Red
TheMouse->setCursorTooltip(L"Text", 50, &houseColor, 1.0f);
```

### WND File Entry
```
WINDOW MyButton BUTTON "MyButton"
{
    TOOLTIP "Hover text"
    TOOLTIPDELAY 150
}
```

