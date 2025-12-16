# EA Generals Tooltip System - Complete Implementation Analysis

## Overview
The EA Generals tooltip system is a sophisticated multi-layered architecture that handles text rendering, delay mechanisms, callback integration, and intelligent positioning. The system combines mouse tracking, window management, and display rendering with configurable visual properties.

---

## 1. TOOLTIP RENDERING & DISPLAY LOGIC

### Primary Rendering Function
**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp:979-1047`

```cpp
void Mouse::drawTooltip( void )
{
    if (TheScriptEngine->getFade()!=ScriptEngine::FADE_NONE) {
        return;  // Skip rendering if screen is fading
    }

    /// @todo: Still need to put in display logic so it puts the tool tips in a visable position on the edge of the screen
    if( m_displayTooltip && TheDisplay && m_tooltipDisplayString && 
        (m_tooltipDisplayString->getTextLength() > 0) && !m_isTooltipEmpty)
    {
        Int width, xPos;
        Int height, yPos;
        m_tooltipDisplayString->getSize(&width,&height);
        xPos = m_currMouse.pos.x + 20;      // 20px offset from cursor
        yPos = m_currMouse.pos.y;           // Vertical alignment at cursor

        // POSITIONING LOGIC - Edge detection to keep tooltip visible
        if( xPos + width + 4 > m_maxX ) // +4 for spill margin
        {
            xPos -= 20 + width;  // Flip to left of cursor
        }
        if( yPos + height + 4 > m_maxY ) // +4 for spill margin
        {
            yPos -= height;      // Flip above cursor
        }

        // ANIMATED BACKGROUND FILL
        Int boxWidth = (m_tooltipAnimateBackground)?(min(width, m_highlightPos)):width;

        // Draw background box
        TheDisplay->drawFillRect(xPos, yPos, boxWidth + 2, height + 2, 
                                 GMC(m_tooltipBackColor));
        
        // Draw border
        TheDisplay->drawOpenRect(xPos, yPos, boxWidth + 2, height + 2, 1.0, 
                                 COLOR(Border));

        // Build clip rect for text rendering
        IRegion2D clipRegion;
        clipRegion.lo.x = xPos+2;
        clipRegion.lo.y = yPos+1;
        clipRegion.hi.x = xPos+2+m_highlightPos;
        clipRegion.hi.y = yPos+1+height;
        
        m_tooltipDisplayString->setClipRegion(&clipRegion);
        m_tooltipDisplayString->draw(xPos +2, yPos +1, GMC(m_tooltipTextColor), 
                                      COLOR(Shadow));

        // HIGHLIGHT SECTION - Animated sweep effect
        const Int HIGHLIGHT_WIDTH = 15;
        clipRegion.lo.x = xPos+2+m_highlightPos-HIGHLIGHT_WIDTH;
        clipRegion.lo.y = yPos+1;
        clipRegion.hi.x = xPos+2+m_highlightPos;
        clipRegion.hi.y = yPos+1+height;
        m_tooltipDisplayString->setClipRegion(&clipRegion);
        m_tooltipDisplayString->draw(xPos +2, yPos +1, COLOR(Highlight), 
                                      COLOR(Shadow));

        // UPDATE ANIMATION - Progressive highlight position
        if (m_highlightPos < width + HIGHLIGHT_WIDTH)
        {
            UnsignedInt now = timeGetTime();
            m_highlightPos = (width*(now-m_highlightUpdateStart))/m_tooltipFillTime;
        }
    }
}
```

### Key Rendering Features:
1. **Fade Skip**: Returns early if screen is fading
2. **Render Conditions**: Multiple checks ensure valid tooltip data
3. **Dynamic Positioning**: Offset 20px right/down from cursor with edge detection
4. **Animated Background**: Optional sweep effect controlled by `m_highlightPos`
5. **Highlight Animation**: 15px wide animated highlight moves across text
6. **Clip Regions**: Precise text clipping for animation effect
7. **Color Rendering**: Separate highlight, text, shadow, and border colors

---

## 2. TOOLTIP DELAY MECHANISM

### Delay Tracking System
**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp:681-712`

```cpp
Int delay = m_tooltipDelayTime;      // Default delay (50ms)
if(m_tooltipDelay >= 0 )
   delay = m_tooltipDelay;            // Override with window-specific delay

if( now - m_stillTime >= delay )
{
    if (!m_displayTooltip)
    {
        m_highlightPos = 0;                    // Reset animation
        m_highlightUpdateStart = timeGetTime(); // Start timer
    }

    // Display tooltip after delay elapsed
    m_displayTooltip = TRUE;
}
else
{
    // Still waiting for delay to pass
    m_displayTooltip = FALSE;
}
```

### Delay Reset on Mouse Movement
```cpp
for (Int i = 0; i < m_eventsThisFrame; ++i)
{
    processMouseEvent(i);
    if (m_currMouse.deltaPos.x || m_currMouse.deltaPos.y)
        m_stillTime = now;  // Reset delay timer on ANY movement
    // ...
}
```

### Reset Function
**Line 970-973:**
```cpp
void Mouse::resetTooltipDelay( void )
{
    m_stillTime = timeGetTime();
    m_displayTooltip = FALSE;
}
```

### Delay Configuration
**File:** `Code/GameEngine/Include/GameClient/Mouse.h:291`
- `Int m_tooltipDelayTime` - Default delay: 50 milliseconds
- `Int m_tooltipDelay` - Per-window override (-1 = use default)

### Two-Tier Delay System:
1. **Global Delay** (`m_tooltipDelayTime`): 50ms by default
2. **Window-Specific Override** (`m_tooltipDelay`): Set via WinInstanceData
3. **Still Time Tracking** (`m_stillTime`): Records when mouse last moved
4. **Comparison**: `now - m_stillTime >= delay` determines if tooltip should show

---

## 3. TOOLTIP CALLBACK/HANDLER SYSTEM

### Callback Type Definition
**File:** `Code/GameEngine/Include/GameClient/GameWindow.h:87-89`

```cpp
typedef void (*GameWinTooltipFunc)( GameWindow *, 
                                    WinInstanceData *, 
                                    UnsignedInt );
```

**Parameters:**
- `GameWindow*` - Window being hovered
- `WinInstanceData*` - Window instance data containing tooltip text
- `UnsignedInt` - Mouse coordinates (packed)

### Callback Integration in GameWindowManager
**File:** `Code/GameEngine/Source/GameClient/GUI/GameWindowManager.cpp:1200-1232`

```cpp
if( tooltipsOn )
{
    if( toolTipWindow )
    {
        // CALLBACK PATH - Custom tooltip handler
        if( toolTipWindow->m_tooltip )
        {
            toolTipWindow->m_tooltip( toolTipWindow, 
                                      &toolTipWindow->m_instData, 
                                      packedMouseCoords );
        }
        // STATIC TEXT PATH - Simple tooltip display
        else if( toolTipWindow->m_instData.getTooltipTextLength() )
        {
            TheMouse->setCursorTooltip( toolTipWindow->m_instData.getTooltipText(), 
                                        toolTipWindow->m_instData.m_tooltipDelay );
        }
    }
    else
    {
        // No window tooltip - Mark for world object tooltips
        objectTooltip = TRUE;
    }
}
```

### Control Bar Example Implementation
**File:** `Code/GameEngine/Source/GameClient/GUI/ControlBar/ControlBar.cpp:129-134`

```cpp
static void commandButtonTooltip(GameWindow *window,
                                WinInstanceData *instData,
                                UnsignedInt mouse)
{
    // Custom tooltip logic: Show build tooltip layout
    TheControlBar->showBuildTooltipLayout(window);
}
```

### Callback Registration
**File:** `Code/GameEngine/Source/GameClient/GUI/ControlBar/ControlBar.cpp:1090-1136`

```cpp
m_communicatorButton->winSetTooltipFunc(commandButtonTooltip);
// ... repeat for command buttons, science purchase buttons, etc.
```

### Function Lexicon Integration
**File:** `Code/GameEngine/Include/Common/FunctionLexicon.h:104`

```cpp
GameWinTooltipFunc gameWinTooltipFunc( NameKeyType key, 
                                       TableIndex = TABLE_GAME_WIN_TOOLTIP );
```

### Callback Decision Tree:
1. Check if window has callback (`window->m_tooltip`)
2. If yes: Call callback with window, instance data, mouse coords
3. If no: Check if static tooltip text exists
4. If yes: Call `setCursorTooltip()` with text and delay
5. If no: Mark for world object tooltips

---

## 4. TOOLTIP POSITIONING LOGIC

### Mouse-Follow Positioning
**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp:990-1000`

```cpp
Int width, xPos;
Int height, yPos;
m_tooltipDisplayString->getSize(&width, &height);

// Default position: 20px right and at cursor height
xPos = m_currMouse.pos.x + 20;
yPos = m_currMouse.pos.y;
```

### Edge Detection & Boundary Handling
```cpp
// Right edge detection - Flip to left if tooltip exceeds screen
if( xPos + width + 4 > m_maxX )  // +4 for spill margin
{
    xPos -= 20 + width;  // Move to left of cursor
}

// Bottom edge detection - Flip to top if tooltip exceeds screen
if( yPos + height + 4 > m_maxY )  // +4 for spill margin
{
    yPos -= height;      // Move above cursor
}
```

### Sticky Positioning (via Delay)
- Once delay elapsed and `m_displayTooltip = TRUE`, position updates every frame
- Position follows mouse until moved away (resets delay timer)
- Tooltip disappears if mouse stays outside current window for full delay period

### Dynamic Width Wrapping
**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp:826-857`

```cpp
void Mouse::setCursorTooltip( UnicodeString tooltip, Int delay, 
                              const RGBColor *color, Real width )
{
    Bool forceRecalc = FALSE;
    if ( !tooltip.isEmpty() && width != m_lastTooltipWidth )
    {
        forceRecalc = TRUE;
        Int widthInPixels = (Int)(TheDisplay->getWidth()*m_tooltipWidth*width);
        
        if (widthInPixels < 10)
            widthInPixels = 120;
        else if (widthInPixels > TheDisplay->getWidth())
            widthInPixels = TheDisplay->getWidth();
        
        m_tooltipDisplayString->setWordWrap( widthInPixels );
        m_lastTooltipWidth = width;
    }
}
```

### Positioning Summary:
1. **Base Position**: +20px right, cursor Y level
2. **Horizontal Wrap**: Flips left if exceeds right edge
3. **Vertical Wrap**: Flips up if exceeds bottom edge
4. **Edge Margin**: 4px spill buffer
5. **Dynamic Width**: Word-wrapping from 10px minimum to screen width maximum
6. **Follow Behavior**: Position updates every frame while delay satisfied

---

## 5. TOOLTIP EVENT SYSTEM (Show/Hide Timing)

### Show Event Trigger
**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp:687-703`

```cpp
if( now - m_stillTime >= delay )
{
    if (!m_displayTooltip)
    {
        // SHOW TRIGGER POINT
        m_highlightPos = 0;
        m_highlightUpdateStart = timeGetTime();  // Start animation timer
    }
    m_displayTooltip = TRUE;
}
else
{
    // HIDE TRIGGER POINT
    m_displayTooltip = FALSE;
}
```

### Hide Events (Trigger Display = FALSE)

#### 1. Mouse Movement Reset
```cpp
if (m_currMouse.deltaPos.x || m_currMouse.deltaPos.y)
    m_stillTime = now;  // Reset delay, tooltip immediately hides
```

#### 2. Window Change Detection
**File:** `Code/GameEngine/Source/GameClient/GUI/GameWindowManager.cpp:1200+`
- GameWindowManager tracks current window under cursor
- On window change, old tooltip cleared
- New window's tooltip delay timer starts fresh

#### 3. Manual Reset
```cpp
void Mouse::resetTooltipDelay( void )
{
    m_stillTime = timeGetTime();
    m_displayTooltip = FALSE;
}
```

Usage: `TheMouse->resetTooltipDelay()` called from InGameUI on certain events

#### 4. Script Fade Suppression
```cpp
if (TheScriptEngine->getFade()!=ScriptEngine::FADE_NONE) {
    return;  // Skip drawing, effectively hides tooltip
}
```

### Animation Timeline

```
Time 0ms: Mouse enters window/stops moving
          m_stillTime = now
          m_displayTooltip = FALSE
          
Time 50ms: Delay threshold reached
          m_displayTooltip = TRUE
          m_highlightPos = 0
          m_highlightUpdateStart = now
          
Time 50ms-100ms: Tooltip renders
                Animation plays: m_highlightPos advances
                Final position: m_highlightPos = width + 15
                
Any movement: m_stillTime = now (resets to Time 0)
```

### Show/Hide Conditions Summary:

**Show (Display = TRUE):**
- Mouse stationary for delay period (default 50ms)
- Window contains valid tooltip (callback or static text)
- Script not fading
- Display available

**Hide (Display = FALSE):**
- Mouse moved (any delta)
- Window changed
- Explicit reset called
- Script fading
- Delay not yet elapsed

---

## 6. TOOLTIP CONFIGURATION & PROPERTIES

### INI Configuration Properties
**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp:93-112`

```cpp
static const FieldParse TheMouseFieldParseTable[] = 
{
    // Font configuration
    { "TooltipFontName",              INI::parseAsciiString,    NULL, offsetof( Mouse, m_tooltipFontName ) },
    { "TooltipFontSize",              INI::parseInt,            NULL, offsetof( Mouse, m_tooltipFontSize ) },
    { "TooltipFontIsBold",            INI::parseBool,           NULL, offsetof( Mouse, m_tooltipFontIsBold ) },
    
    // Animation configuration
    { "TooltipAnimateBackground",     INI::parseBool,           NULL, offsetof( Mouse, m_tooltipAnimateBackground ) },
    { "TooltipFillTime",              INI::parseInt,            NULL, offsetof( Mouse, m_tooltipFillTime ) },
    
    // Timing configuration
    { "TooltipDelayTime",             INI::parseInt,            NULL, offsetof( Mouse, m_tooltipDelayTime ) },
    
    // Color configuration
    { "TooltipTextColor",             INI::parseRGBAColorInt,   NULL, offsetof( Mouse, m_tooltipColorText ) },
    { "TooltipHighlightColor",        INI::parseRGBAColorInt,   NULL, offsetof( Mouse, m_tooltipColorHighlight ) },
    { "TooltipShadowColor",           INI::parseRGBAColorInt,   NULL, offsetof( Mouse, m_tooltipColorShadow ) },
    { "TooltipBackgroundColor",       INI::parseRGBAColorInt,   NULL, offsetof( Mouse, m_tooltipColorBackground ) },
    { "TooltipBorderColor",           INI::parseRGBAColorInt,   NULL, offsetof( Mouse, m_tooltipColorBorder ) },
    
    // Layout configuration
    { "TooltipWidth",                 INI::parsePercentToReal,  NULL, offsetof( Mouse, m_tooltipWidth ) },
    
    // House color support
    { "UseTooltipAltTextColor",       INI::parseBool,           NULL, offsetof( Mouse, m_useTooltipAltTextColor ) },
    { "UseTooltipAltBackColor",       INI::parseBool,           NULL, offsetof( Mouse, m_useTooltipAltBackColor ) },
    { "AdjustTooltipAltColor",        INI::parseBool,           NULL, offsetof( Mouse, m_adjustTooltipAltColor ) },
};
```

### Default Values (Constructor)
**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp:447-535`

```cpp
// Font defaults
m_tooltipFontName = "Times New Roman";
m_tooltipFontSize = 12;
m_tooltipFontIsBold = FALSE;

// Animation defaults
m_tooltipAnimateBackground = TRUE;
m_tooltipFillTime = 50;              // 50ms animation duration
m_tooltipDelayTime = 50;             // 50ms before showing

// Color defaults (RGBA)
m_tooltipColorText = {220, 220, 220, 255};       // Light gray text
m_tooltipColorHighlight = {255, 255, 0, 255};    // Yellow highlight
m_tooltipColorShadow = {0, 0, 0, 255};           // Black shadow
m_tooltipColorBackground = {20, 20, 0, 127};     // Dark yellow background, semi-transparent
m_tooltipColorBorder = {0, 0, 0, 255};           // Black border

// Layout defaults
m_tooltipWidth = 15.0f;              // 15% of screen width
m_lastTooltipWidth = 0.0f;

// House color support
m_useTooltipAltTextColor = FALSE;
m_useTooltipAltBackColor = FALSE;
m_adjustTooltipAltColor = FALSE;

// Animation state
m_highlightPos = 0;
m_highlightUpdateStart = 0;
m_stillTime = 0;
m_displayTooltip = FALSE;
m_isTooltipEmpty = TRUE;
```

### Window-Level Configuration
**File:** `Code/GameEngine/Include/GameClient/WinInstanceData.h:166`

```cpp
Int m_tooltipDelay;  ///< desired delay before showing tooltip
```

**File:** `Code/GameEngine/Include/GameClient/GameWindow.h:299-300`

```cpp
Int  getTooltipDelay() { return m_instData.m_tooltipDelay; }
void setTooltipDelay(Int delay) { m_instData.m_tooltipDelay = delay; }
```

### WND File Parsing
**File:** `Code/GameEngine/Source/GameClient/GUI/GameWindowManagerScript.cpp:1015-1031`

```cpp
// parseTooltipDelay
static Bool parseTooltipDelay( char *token, WinInstanceData *instData,
                               char *type )
{
    if( token == NULL )
        return FALSE;

    char *c = token;
    scanInt( c, instData->m_tooltipDelay );

    return TRUE;
}
```

**Parser registration:**
```cpp
{ "TOOLTIPDELAY", parseTooltipDelay }
```

### House Color Support
**File:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp:860-890`

```cpp
if (color)
{
    if (m_useTooltipAltTextColor)
    {
        if (m_adjustTooltipAltColor)
        {
            // Brighten color for better contrast
            m_tooltipTextColor.red   = REAL_TO_INT((color->red + 1.0f)   * 255.0f / 2.0f);
            m_tooltipTextColor.green = REAL_TO_INT((color->green + 1.0f) * 255.0f / 2.0f);
            m_tooltipTextColor.blue  = REAL_TO_INT((color->blue + 1.0f)  * 255.0f / 2.0f);
        }
        else
        {
            // Use color directly
            m_tooltipTextColor.red   = REAL_TO_INT(color->red   * 255.0f);
            m_tooltipTextColor.green = REAL_TO_INT(color->green * 255.0f);
            m_tooltipTextColor.blue  = REAL_TO_INT(color->blue  * 255.0f);
        }
        m_tooltipTextColor.alpha = m_tooltipColorText.alpha;
    }
    
    if (m_useTooltipAltBackColor)
    {
        if (m_adjustTooltipAltColor)
        {
            // Darken color for contrast
            m_tooltipBackColor.red   = REAL_TO_INT(color->red   * 255.0f * 0.5f);
            m_tooltipBackColor.green = REAL_TO_INT(color->green * 255.0f * 0.5f);
            m_tooltipBackColor.blue  = REAL_TO_INT(color->blue  * 255.0f * 0.5f);
        }
        else
        {
            m_tooltipBackColor.red   = REAL_TO_INT(color->red   * 255.0f);
            m_tooltipBackColor.green = REAL_TO_INT(color->green * 255.0f);
            m_tooltipBackColor.blue  = REAL_TO_INT(color->blue  * 255.0f);
        }
        m_tooltipBackColor.alpha = m_tooltipColorBackground.alpha;
    }
}
else
{
    m_tooltipTextColor = m_tooltipColorText;
    m_tooltipBackColor = m_tooltipColorBackground;
}
```

---

## 7. COMPLETE TOOLTIP LIFECYCLE

### Phase 1: Initialization
1. Mouse object created with default tooltip properties
2. DisplayString allocated for tooltip rendering
3. All colors, fonts, delays configured via INI
4. Window manager reads WND files with TOOLTIPDELAY entries

### Phase 2: Idle (No Tooltip)
1. Mouse cursor moves or stays in non-tooltipable area
2. `m_displayTooltip = FALSE`
3. `m_stillTime` updated when movement detected
4. No rendering occurs

### Phase 3: Hover Start
1. Mouse enters tooltip-enabled window
2. GameWindowManager detects window under cursor
3. Checks for callback or static tooltip text
4. If callback: calls tooltip function
5. If static: calls `Mouse::setCursorTooltip(text, delay)`
6. `m_stillTime` recorded (start of delay)

### Phase 4: Delay Period
1. Time elapsed: `now - m_stillTime`
2. Compare against: `delay = m_tooltipDelay` (if >= 0) OR `m_tooltipDelayTime` (default 50ms)
3. While `elapsed < delay`: `m_displayTooltip = FALSE`, no rendering
4. If mouse moves: `m_stillTime` reset, delay counter resets

### Phase 5: Display
1. When `elapsed >= delay`:
   - `m_displayTooltip = TRUE`
   - `m_highlightPos = 0` (reset animation)
   - `m_highlightUpdateStart = now` (start animation timer)

### Phase 6: Animation & Rendering
1. Every frame during display:
   - Calculate position (20px right, cursor Y, with edge wrapping)
   - Draw background box with optional animation
   - Draw text with clip regions
   - Update highlight position: `m_highlightPos = (width*(now-m_highlightUpdateStart))/m_tooltipFillTime`
   - Animation completes when `m_highlightPos >= width + 15`

### Phase 7: Hide Events
1. **Mouse Movement**: Any delta resets `m_stillTime`, triggers Phase 4
2. **Window Change**: GameWindowManager switches tooltip window context
3. **Manual Reset**: `TheMouse->resetTooltipDelay()` forces Phase 4
4. **Script Fade**: Rendering skipped, effectively hides (Phase 2)

### Phase 8: Termination
1. Mouse leaves tooltip area
2. New window doesn't have tooltip
3. `m_displayTooltip = FALSE`
4. `m_tooltipDisplayString` cleared (if set)
5. Return to Phase 2 (Idle)

---

## 8. KEY DATA STRUCTURES

### Mouse Class Tooltip Members
**File:** `Code/GameEngine/Include/GameClient/Mouse.h`

```cpp
// Configuration (INI-loaded)
AsciiString m_tooltipFontName;          ///< Tooltip font name
Int m_tooltipFontSize;                  ///< Tooltip font size
Bool m_tooltipFontIsBold;               ///< Tooltip font bold
Bool m_tooltipAnimateBackground;        ///< Enable animation
Int m_tooltipFillTime;                  ///< Animation duration (ms)
Int m_tooltipDelayTime;                 ///< Default delay (ms)
Real m_tooltipWidth;                    ///< Tooltip width % of screen
Bool m_useTooltipAltTextColor;          ///< Use house colors for text
Bool m_useTooltipAltBackColor;          ///< Use house colors for background
Bool m_adjustTooltipAltColor;           ///< Adjust brightness for contrast

// Colors
RGBAColorInt m_tooltipColorText;
RGBAColorInt m_tooltipColorHighlight;
RGBAColorInt m_tooltipColorShadow;
RGBAColorInt m_tooltipColorBackground;
RGBAColorInt m_tooltipColorBorder;
RGBAColorInt m_tooltipTextColor;        ///< Runtime color (can be modified by setCursorTooltip)
RGBAColorInt m_tooltipBackColor;        ///< Runtime color

// State
UnicodeString m_tooltipString;          ///< Current tooltip text
DisplayString *m_tooltipDisplayString;  ///< Rendered tooltip
Bool m_displayTooltip;                  ///< Should render this frame?
Bool m_isTooltipEmpty;                  ///< Text is empty
Int m_tooltipDelay;                     ///< Window-specific delay override (-1 = use default)
Int m_highlightPos;                     ///< Animation sweep position
UnsignedInt m_highlightUpdateStart;     ///< Animation start time
UnsignedInt m_stillTime;                ///< Last movement timestamp
Real m_lastTooltipWidth;                ///< Cached width for word wrap
```

### WinInstanceData Tooltip Members
**File:** `Code/GameEngine/Include/GameClient/WinInstanceData.h`

```cpp
Int m_tooltipDelay;                     ///< desired delay before showing tooltip (-1 = default)
AsciiString m_tooltipString;            ///< tooltip Label from window file
DisplayString *m_tooltip;               ///< tooltip for display
GameWinTooltipFunc m_tooltip;           ///< callback for tooltip execution (different m_tooltip)
```

### GameWindow Tooltip Methods
**File:** `Code/GameEngine/Include/GameClient/GameWindow.h`

```cpp
void winSetTooltip( UnicodeString tip );              ///< set tooltip text
Int  getTooltipDelay() { return m_instData.m_tooltipDelay; }
void setTooltipDelay(Int delay) { m_instData.m_tooltipDelay = delay; }
Int winSetTooltipFunc( GameWinTooltipFunc tooltip );  ///< set callback
GameWinTooltipFunc winGetTooltipFunc( void );         ///< get callback
```

---

## 9. INTEGRATION POINTS

### Mouse Rendering Loop
**File:** `Code/GameEngineDevice/Source/W3DDevice/GameClient/W3DMouse.cpp:580-592`

```cpp
// draw cursor text
if (!isThread)
    drawCursorText();

// draw tooltip text
if (m_visible && !isThread)
    drawTooltip();  ← Tooltip rendered every frame during display
```

### Window Manager Update Loop
**File:** `Code/GameEngine/Source/GameClient/GUI/GameWindowManager.cpp:1015-1232`

- Detects window under mouse cursor
- Searches for tooltip in window hierarchy
- Calls tooltip callback or sets static text via `setCursorTooltip()`
- Handles tooltip detection with window layer system (ABOVE, NORMAL, BELOW)

### Control Bar Integration
**File:** `Code/GameEngine/Source/GameClient/GUI/ControlBar/ControlBar.cpp:1090+`

- Registers `commandButtonTooltip` callback for build command buttons
- Shows/hides tooltip layout (`ControlBarPopupDescription.wnd`)
- Handles dynamic tooltip content (build times, costs, etc.)

### INI Configuration
- **Mouse section**: Global tooltip properties
- **Window definitions**: Individual TOOLTIPDELAY overrides
- **GUIEdit**: Tooltip editor for .wnd file creation

---

## 10. IMPLEMENTATION NOTES FOR OpenSAGE

### Key Takeaways for Re-implementation:

1. **Two-tier Delay System**: 
   - Global default (50ms) + per-window override
   - Reset on ANY mouse movement (delta)

2. **Callback Architecture**:
   - Supports custom callbacks for dynamic tooltips
   - Falls back to static text if no callback
   - Callbacks receive window, instance data, and mouse coords

3. **Animated Rendering**:
   - Highlight sweep effect with 15px width
   - Animation fill time configurable (default 50ms)
   - Animation uses clipping regions for clean rendering

4. **Positioning**:
   - 20px right, cursor Y level
   - Edge wrapping on right/bottom screen boundaries
   - 4px spill margin
   - Dynamic word wrapping (10px min to screen max)

5. **State Management**:
   - `m_displayTooltip` controls rendering
   - `m_stillTime` tracks delay timer
   - Mouse movement resets delay immediately
   - Window change resets delay context

6. **Color Support**:
   - Optional house color substitution
   - Configurable brightness adjustment
   - Separate colors for text, highlight, shadow, background, border

7. **Conditions for Display**:
   - Valid DisplayString and text
   - Delay threshold met
   - Not fading
   - Display system available
   - Not empty flag set

---

## File Reference Map

| Purpose | File Path | Key Lines |
|---------|-----------|-----------|
| Mouse Tooltip Core | `Code/GameEngine/Source/GameClient/Input/Mouse.cpp` | 447-535 (init), 681-712 (delay), 826-898 (set), 970-1047 (draw) |
| Mouse Header | `Code/GameEngine/Include/GameClient/Mouse.h` | 264-291 (members), 276 (draw decl) |
| Window Callback | `Code/GameEngine/Include/GameClient/GameWindow.h` | 87-89 (typedef), 295-300 (methods), 349 (callback set), 406 (callback member) |
| Window Instance Data | `Code/GameEngine/Include/GameClient/WinInstanceData.h` | 166 (delay member) |
| GameWindowManager Integration | `Code/GameEngine/Source/GameClient/GUI/GameWindowManager.cpp` | 1015-1115 (detection), 1200-1232 (callback/static) |
| ControlBar Example | `Code/GameEngine/Source/GameClient/GUI/ControlBar/ControlBar.cpp` | 129-134 (callback), 1090-1136 (registration) |
| INI Parsing | `Code/GameEngine/Source/GameClient/Input/Mouse.cpp` | 93-112 (field parse table) |
| WND Parsing | `Code/GameEngine/Source/GameClient/GUI/GameWindowManagerScript.cpp` | 1015-1031 (tooltipdelay parser), 2278 (parser table) |
| W3D Rendering | `Code/GameEngineDevice/Source/W3DDevice/GameClient/W3DMouse.cpp` | 591 (draw call) |

