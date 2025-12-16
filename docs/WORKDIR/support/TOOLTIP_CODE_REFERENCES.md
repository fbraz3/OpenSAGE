# EA Tooltip System - Code Line Reference Guide

## Quick Navigation to Key Tooltip Code

### MOUSE CLASS TOOLTIP MEMBERS

**Header File:** `Code/GameEngine/Include/GameClient/Mouse.h`

| Member | Line | Type | Purpose |
|--------|------|------|---------|
| m_tooltipFontName | ~283 | AsciiString | Font for rendering |
| m_tooltipFontSize | ~284 | Int | Font size in points |
| m_tooltipFontIsBold | ~285 | Bool | Font weight |
| m_tooltipAnimateBackground | ~286 | Bool | Enable animation sweep |
| m_tooltipFillTime | ~287 | Int | Animation duration (ms) |
| m_tooltipDelayTime | 291 | Int | Default delay (50ms) |
| m_tooltipWidth | ~288 | Real | Width % of screen |
| m_tooltipColorText | ~288 | RGBAColorInt | Text color |
| m_tooltipColorHighlight | ~289 | RGBAColorInt | Highlight color |
| m_tooltipColorShadow | ~289 | RGBAColorInt | Shadow color |
| m_tooltipColorBackground | ~290 | RGBAColorInt | Background color |
| m_tooltipColorBorder | ~290 | RGBAColorInt | Border color |
| m_useTooltipAltTextColor | ~292 | Bool | Use house colors for text |
| m_useTooltipAltBackColor | ~293 | Bool | Use house colors for background |
| m_adjustTooltipAltColor | ~294 | Bool | Adjust brightness |
| m_tooltipString | ~330 | UnicodeString | Current tooltip text |
| m_tooltipDisplayString | ~331 | DisplayString* | Rendered tooltip |
| m_displayTooltip | 331 | Bool | Should display this frame? |
| m_isTooltipEmpty | ~333 | Bool | Text is empty |
| m_tooltipDelay | 362 | Int | Window-specific override |
| m_highlightPos | ~362 | Int | Animation progress |
| m_highlightUpdateStart | ~363 | UnsignedInt | Animation start time |
| m_stillTime | ~363 | UnsignedInt | Last movement timestamp |
| m_lastTooltipWidth | ~364 | Real | Cached width for wrapping |

---

### KEY FUNCTIONS IN MOUSE CLASS

**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp`

#### Constructor
```
Mouse::Mouse()  [Line 447-535]
- Initializes all tooltip members
- Sets default values for colors, fonts, delays
- Default font: "Times New Roman", size 12
- Default delay: 50ms
- Default colors: light gray text, yellow highlight, dark background
```

#### Main Delay Logic (In createStreamMessages)
```
Delay checking  [Lines 681-712]
- Compares current time to m_stillTime
- Applies global or window-specific delay
- Sets m_displayTooltip TRUE/FALSE
- Resets animation on show
```

#### Set Tooltip Text
```
setCursorTooltip()  [Lines 826-898]
- Sets m_tooltipString and DisplayString
- Configures word wrapping
- Applies house colors if enabled
- Parameters: text, delay, color, width
```

#### Draw Tooltip
```
drawTooltip()  [Lines 979-1047]
- Main rendering function
- Calculates position with edge wrapping
- Draws background, border, text
- Animates highlight sweep
- Only renders if m_displayTooltip = TRUE
```

#### Reset Delay
```
resetTooltipDelay()  [Lines 970-973]
- Sets m_stillTime = now
- Sets m_displayTooltip = FALSE
- Restarts delay counter
```

#### INI Parse Table
```
TheMouseFieldParseTable  [Lines 93-112]
- Defines all INI configuration keys
- Links to offset in Mouse class
- Used for loading from INI files
```

---

### GAMEWINDOW CALLBACK INTEGRATION

**Header File:** `Code/GameEngine/Include/GameClient/GameWindow.h`

#### Callback Type Definition
```cpp
typedef void (*GameWinTooltipFunc)( GameWindow *, 
                                    WinInstanceData *, 
                                    UnsignedInt );
[Line 87-89]
```

#### GameWindow Methods
```
winSetTooltip()                 [Line 295]
- Sets static tooltip text in WinInstanceData
- Signature: void winSetTooltip( UnicodeString tip )

getTooltipDelay()               [Line 299]
- Returns m_instData.m_tooltipDelay
- Inline method

setTooltipDelay()               [Line 300]
- Sets m_instData.m_tooltipDelay
- Inline method

winSetTooltipFunc()             [Line 349]
- Registers callback function
- Signature: Int winSetTooltipFunc( GameWinTooltipFunc tooltip )

winGetTooltipFunc()             [Line 370]
- Returns callback function pointer
```

---

### GAMEWINDOWMANAGER INTEGRATION

**File:** `Code/GameEngine/Source/GameClient/GUI/GameWindowManager.cpp`

#### Tooltip Window Detection
```
Lines 1015-1115
- Searches window hierarchy by mouse position
- Checks ABOVE, NORMAL, BELOW layers
- Looks for tooltip in window or children
- Records window context in toolTipWindow variable
```

#### Callback Execution
```
Lines 1200-1232
Decision tree:
1. Check if window has callback (line 1211)
   - if (toolTipWindow->m_tooltip) → call callback
2. Otherwise check static text (line 1218)
   - else if (getTooltipTextLength()) → call setCursorTooltip()
3. Otherwise mark for world objects (line 1227)
   - objectTooltip = TRUE
```

---

### CONTROL BAR TOOLTIP EXAMPLE

**File:** `Code/GameEngine/Source/GameClient/GUI/ControlBar/ControlBar.cpp`

#### Callback Definition
```
commandButtonTooltip()  [Lines 129-134]
- Receives: GameWindow*, WinInstanceData*, mouse coords
- Shows build tooltip layout instead of simple text
- TheControlBar->showBuildTooltipLayout(window)
```

#### Callback Registration
```
Lines 1090-1136
- Registers callback for each command button type:
  - m_communicatorButton->winSetTooltipFunc(commandButtonTooltip)
  - Science command buttons
  - Unit upgrade buttons
  - Special power buttons
```

---

### WND FILE PARSING

**File:** `Code/GameEngine/Source/GameClient/GUI/GameWindowManagerScript.cpp`

#### TOOLTIPDELAY Parser
```
parseTooltipDelay()  [Lines 1015-1031]
- Reads TOOLTIPDELAY value from WND file
- Stores in instData->m_tooltipDelay
- Parser signature: Bool parseTooltipDelay(char *token, WinInstanceData *instData, char *type)
```

#### Parser Registration
```
TheGameWindowFieldParseTable  [Line 2278]
{ "TOOLTIPDELAY", parseTooltipDelay }
```

---

### MOUSE INPUT TRACKING

**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp`

#### Movement Detection
```
Line ~700:
if (m_currMouse.deltaPos.x || m_currMouse.deltaPos.y)
    m_stillTime = now;
- Any movement resets the delay counter
- Immediate hide of tooltip
```

#### Draw Call
```
Called from W3DMouse rendering loop
File: Code/GameEngineDevice/Source/W3DDevice/GameClient/W3DMouse.cpp
Line 591: drawTooltip()
- Happens every frame in render pipeline
- Only renders if conditions met
```

---

### WINDOW INSTANCE DATA

**File:** `Code/GameEngine/Include/GameClient/WinInstanceData.h`

#### Tooltip Members
```
m_tooltipDelay          [Line 166]
- Type: Int
- Default: -1 (use global)
- Per-window override

m_tooltipString         [~150-160]
- Type: AsciiString
- From WND file

m_tooltip               [~170]
- Type: DisplayString*
- Rendered tooltip

m_tooltip               [~409 in different version]
- Type: GameWinTooltipFunc
- Callback function pointer
```

---

### ANIMATION FRAME CALCULATION

**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp` lines 1031-1037

```cpp
// Update highlight position each frame
if (m_highlightPos < width + HIGHLIGHT_WIDTH)
{
    UnsignedInt now = timeGetTime();
    m_highlightPos = (width*(now-m_highlightUpdateStart))/m_tooltipFillTime;
}
```

**Formula:** `pos = (width * elapsed_time) / total_fill_time`

Example:
- width = 100px
- fillTime = 50ms
- At 25ms: pos = (100 * 25) / 50 = 50px
- At 50ms: pos = (100 * 50) / 50 = 100px

---

### EDGE WRAPPING LOGIC

**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp` lines 992-1002

```cpp
// Right edge detection
if( xPos + width + 4 > m_maxX )
{
    xPos -= 20 + width;  // Flip left
}

// Bottom edge detection
if( yPos + height + 4 > m_maxY )
{
    yPos -= height;      // Flip up
}
```

**Margins:**
- Standard offset: 20px
- Spill margin: 4px
- Combined flip distance: 20px + tooltip_width

---

### COLOR HOUSE SUPPORT

**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp` lines 860-890

#### Text Color Application
```cpp
if (m_useTooltipAltTextColor)
{
    if (m_adjustTooltipAltColor)
    {
        // Brighten: (color + 1.0) * 255 / 2
        m_tooltipTextColor.red = REAL_TO_INT((color->red + 1.0f) * 255.0f / 2.0f);
    }
    else
    {
        // Direct: color * 255
        m_tooltipTextColor.red = REAL_TO_INT(color->red * 255.0f);
    }
}
```

#### Background Color Application
```cpp
if (m_useTooltipAltBackColor)
{
    if (m_adjustTooltipAltColor)
    {
        // Darken: color * 255 * 0.5
        m_tooltipBackColor.red = REAL_TO_INT(color->red * 255.0f * 0.5f);
    }
    else
    {
        m_tooltipBackColor.red = REAL_TO_INT(color->red * 255.0f);
    }
}
```

---

### DEFAULT COLORS (FROM CONSTRUCTOR)

**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp` lines 495-502

```cpp
setColor(m_tooltipColorText,        220, 220, 220, 255);  // Light gray
setColor(m_tooltipColorHighlight,   255, 255, 0, 255);    // Yellow
setColor(m_tooltipColorShadow,      0, 0, 0, 255);        // Black
setColor(m_tooltipColorBackground,  20, 20, 0, 127);      // Dark yellow, 50% alpha
setColor(m_tooltipColorBorder,      0, 0, 0, 255);        // Black
```

---

### INI CONFIGURATION KEYS

**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp` lines 93-112

| INI Key | Line | Type | Default |
|---------|------|------|---------|
| TooltipFontName | 94 | AsciiString | "Times New Roman" |
| TooltipFontSize | 95 | Int | 12 |
| TooltipFontIsBold | 96 | Bool | FALSE |
| TooltipAnimateBackground | 97 | Bool | TRUE |
| TooltipFillTime | 98 | Int | 50 |
| TooltipDelayTime | 99 | Int | 50 |
| TooltipTextColor | 100 | RGBA | 220,220,220,255 |
| TooltipHighlightColor | 101 | RGBA | 255,255,0,255 |
| TooltipShadowColor | 102 | RGBA | 0,0,0,255 |
| TooltipBackgroundColor | 103 | RGBA | 20,20,0,127 |
| TooltipBorderColor | 104 | RGBA | 0,0,0,255 |
| TooltipWidth | 105 | Real | 15.0f |
| UseTooltipAltTextColor | 106 | Bool | FALSE |
| UseTooltipAltBackColor | 107 | Bool | FALSE |
| AdjustTooltipAltColor | 108 | Bool | FALSE |

---

### SCRIPT FADE SUPPRESSION

**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp` line 980

```cpp
void Mouse::drawTooltip( void )
{
    if (TheScriptEngine->getFade()!=ScriptEngine::FADE_NONE) {
        return;  // Skip rendering when fading
    }
    // ... render logic ...
}
```

---

### CLIPPING REGION RENDERING

**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp` lines 1003-1020

```cpp
// Main text rendering
IRegion2D clipRegion;
clipRegion.lo.x = xPos+2;
clipRegion.lo.y = yPos+1;
clipRegion.hi.x = xPos+2+m_highlightPos;
clipRegion.hi.y = yPos+1+height;
m_tooltipDisplayString->setClipRegion(&clipRegion);
m_tooltipDisplayString->draw(xPos +2, yPos +1, GMC(m_tooltipTextColor), 
                              COLOR(Shadow));

// Highlight rendering
const Int HIGHLIGHT_WIDTH = 15;
clipRegion.lo.x = xPos+2+m_highlightPos-HIGHLIGHT_WIDTH;
clipRegion.lo.y = yPos+1;
clipRegion.hi.x = xPos+2+m_highlightPos;
clipRegion.hi.y = yPos+1+height;
m_tooltipDisplayString->setClipRegion(&clipRegion);
m_tooltipDisplayString->draw(xPos +2, yPos +1, COLOR(Highlight), 
                              COLOR(Shadow));
```

---

## Cross-Reference Table

| Concept | Header | Implementation | Example |
|---------|--------|-----------------|---------|
| Callback Type | GameWindow.h:87 | - | ControlBar.cpp:129 |
| Delay Setting | Mouse.h:291 | Mouse.cpp:681-712 | GameWindowManager.cpp:1222 |
| Text Display | Mouse.h:331 | Mouse.cpp:979-1047 | W3DMouse.cpp:591 |
| Animation | Mouse.h:362-363 | Mouse.cpp:1031-1037 | Mouse.cpp:1006-1020 |
| Edge Wrapping | - | Mouse.cpp:992-1002 | - |
| House Colors | Mouse.h:292-294 | Mouse.cpp:860-890 | - |
| INI Config | Mouse.h:283-294 | Mouse.cpp:93-112 | - |
| WND Parsing | WinInstanceData.h:166 | GameWindowManagerScript.cpp:1015-1031 | - |

