# EA Generals Tooltip System - Search Results Summary

## Documents Created

Two comprehensive documentation files have been created:

1. **EA_TOOLTIP_SYSTEM_ANALYSIS.md** - Complete technical deep-dive
2. **TOOLTIP_QUICK_REFERENCE.md** - Quick reference and examples

---

## Search Results: All Tooltip Patterns Found

### 1. TOOLTIP RENDERING/DISPLAY LOGIC ✓

**Location:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp` lines 979-1047
**Function:** `Mouse::drawTooltip()`

**Key Findings:**
- Tooltip rendering happens every frame during display phase
- Coordinates: 20px right of cursor, cursor Y-level
- Edge wrapping: Flips left if exceeds right edge, flips up if exceeds bottom
- Animated background sweep effect (15px highlight)
- Clipping regions for precise animation
- Returns early if script is fading

**Code Snippet:**
```cpp
void Mouse::drawTooltip( void )
{
    if (TheScriptEngine->getFade()!=ScriptEngine::FADE_NONE) {
        return;  // Skip if fading
    }
    
    if( m_displayTooltip && TheDisplay && m_tooltipDisplayString && 
        (m_tooltipDisplayString->getTextLength() > 0) && !m_isTooltipEmpty)
    {
        // Position calculation
        xPos = m_currMouse.pos.x + 20;
        yPos = m_currMouse.pos.y;
        
        // Edge detection & wrapping
        if( xPos + width + 4 > m_maxX )
            xPos -= 20 + width;  // Flip left
        if( yPos + height + 4 > m_maxY )
            yPos -= height;      // Flip up
        
        // Render with animation
        // ... background, text, highlight ...
    }
}
```

---

### 2. TOOLTIP DELAY MECHANISM ✓

**Location:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp` lines 681-712
**Tracking Method:** Millisecond timer comparison

**Key Findings:**
- Two-tier delay system: global + per-window override
- Delay measured by: `(now - m_stillTime) >= delay_ms`
- `m_stillTime` records last movement timestamp
- ANY mouse movement resets delay timer
- Default delay: 50 milliseconds
- Per-window override possible via WinInstanceData::m_tooltipDelay

**Code Snippet:**
```cpp
Int delay = m_tooltipDelayTime;      // Default: 50ms
if(m_tooltipDelay >= 0 )
   delay = m_tooltipDelay;            // Window override

if( now - m_stillTime >= delay )
{
    if (!m_displayTooltip)
    {
        m_highlightPos = 0;
        m_highlightUpdateStart = timeGetTime();
    }
    m_displayTooltip = TRUE;  // SHOW TRIGGER
}
else
{
    m_displayTooltip = FALSE;  // HIDE TRIGGER
}

// Movement reset
if (m_currMouse.deltaPos.x || m_currMouse.deltaPos.y)
    m_stillTime = now;  // Reset delay counter
```

**Delay Reset Function:**
```cpp
void Mouse::resetTooltipDelay( void )
{
    m_stillTime = timeGetTime();
    m_displayTooltip = FALSE;
}
```

---

### 3. TOOLTIP CALLBACK/HANDLER SYSTEM ✓

**Location:** 
- Definition: `Code/GameEngine/Include/GameClient/GameWindow.h` lines 87-89
- Integration: `Code/GameEngine/Source/GameClient/GUI/GameWindowManager.cpp` lines 1200-1232

**Callback Type:**
```cpp
typedef void (*GameWinTooltipFunc)( GameWindow *, 
                                    WinInstanceData *, 
                                    UnsignedInt );
```

**Integration Logic:**
```cpp
// GameWindowManager tooltip detection
if( tooltipsOn )
{
    if( toolTipWindow )
    {
        // CALLBACK PATH
        if( toolTipWindow->m_tooltip )
        {
            toolTipWindow->m_tooltip( toolTipWindow, 
                                      &toolTipWindow->m_instData, 
                                      packedMouseCoords );
        }
        // STATIC TEXT PATH - Fallback
        else if( toolTipWindow->m_instData.getTooltipTextLength() )
        {
            TheMouse->setCursorTooltip( 
                toolTipWindow->m_instData.getTooltipText(), 
                toolTipWindow->m_instData.m_tooltipDelay );
        }
    }
}
```

**Example Callback Implementation (Control Bar):**
```cpp
static void commandButtonTooltip(GameWindow *window,
                                WinInstanceData *instData,
                                UnsignedInt mouse)
{
    // Custom tooltip: Show build tooltip layout
    TheControlBar->showBuildTooltipLayout(window);
}

// Registration
m_communicatorButton->winSetTooltipFunc(commandButtonTooltip);
```

---

### 4. TOOLTIP POSITIONING LOGIC ✓

**Location:** `Code/GameEngine/Source/GameClient/Input/Mouse.cpp` lines 990-1000

**Positioning Calculation:**
```cpp
Int width, xPos;
Int height, yPos;
m_tooltipDisplayString->getSize(&width, &height);

// Default: 20px right, cursor Y-level
xPos = m_currMouse.pos.x + 20;
yPos = m_currMouse.pos.y;

// Edge Detection & Wrapping
if( xPos + width + 4 > m_maxX )      // Right edge exceeded?
{
    xPos -= 20 + width;              // Flip to left of cursor
}
if( yPos + height + 4 > m_maxY )     // Bottom edge exceeded?
{
    yPos -= height;                  // Flip above cursor
}
```

**Dynamic Width Wrapping:**
```cpp
void Mouse::setCursorTooltip( UnicodeString tooltip, Int delay, 
                              const RGBColor *color, Real width )
{
    // Word wrap configuration
    Int widthInPixels = (Int)(TheDisplay->getWidth()*m_tooltipWidth*width);
    
    if (widthInPixels < 10)
        widthInPixels = 120;
    else if (widthInPixels > TheDisplay->getWidth())
        widthInPixels = TheDisplay->getWidth();
    
    m_tooltipDisplayString->setWordWrap( widthInPixels );
}
```

**Positioning Features:**
- Base position: 20px right, cursor Y-level
- Right boundary: Flips to left when text would exceed screen
- Bottom boundary: Flips up when text would exceed screen
- Margin: 4px spill buffer
- Width handling: 10px minimum, screen width maximum
- Dynamic word wrapping based on configured width percentage

---

### 5. TOOLTIP EVENT SYSTEM (Show/Hide Timing) ✓

**Show Trigger:**
```cpp
// Delay threshold met
if( now - m_stillTime >= delay )
{
    if (!m_displayTooltip)  // First time showing?
    {
        m_highlightPos = 0;              // Reset animation
        m_highlightUpdateStart = now;    // Start animation timer
    }
    m_displayTooltip = TRUE;  // SHOW EVENT
}
```

**Hide Events:**

1. **Mouse Movement:**
```cpp
if (m_currMouse.deltaPos.x || m_currMouse.deltaPos.y)
    m_stillTime = now;  // Reset delay, triggers hide
```

2. **Manual Reset:**
```cpp
TheMouse->resetTooltipDelay();  // Sets m_displayTooltip = FALSE
```

3. **Script Fade:**
```cpp
if (TheScriptEngine->getFade()!=ScriptEngine::FADE_NONE) {
    return;  // Skip rendering (hide)
}
```

4. **Window Change:**
- GameWindowManager detects new window
- Resets tooltip context
- Delay timer restarts

**Animation Timeline:**
```
T=0ms: Delay met
       m_displayTooltip = TRUE
       m_highlightPos = 0
       Start animation
       
T=0-50ms: Animation plays
          m_highlightPos = (width * elapsed) / fillTime
          Highlight sweep left to right
          
T=50ms: Animation complete
        Tooltip stable display
        
T=any: Mouse movement detected
       m_stillTime = now
       m_displayTooltip = FALSE (hide)
```

---

## Files Examined

### Primary Source Files (Generals Version)

1. **Mouse.h** - `Code/GameEngine/Include/GameClient/Mouse.h`
   - Line 264-265: Delay accessors
   - Line 267: setCursorTooltip signature
   - Line 276: drawTooltip declaration
   - Line 279: resetTooltipDelay declaration
   - Line 291: m_tooltipDelayTime
   - Line 331-332: m_displayTooltip flag
   - Line 362: m_tooltipDelay override

2. **Mouse.cpp** - `Code/GameEngine/Source/GameClient/Input/Mouse.cpp`
   - Line 85-112: INI configuration table
   - Line 447-535: Constructor with defaults
   - Line 681-712: Delay checking logic
   - Line 826-898: setCursorTooltip implementation
   - Line 970-973: resetTooltipDelay implementation
   - Line 979-1047: drawTooltip implementation

3. **GameWindow.h** - `Code/GameEngine/Include/GameClient/GameWindow.h`
   - Line 87-89: GameWinTooltipFunc typedef
   - Line 295: winSetTooltip method
   - Line 299-300: Delay getter/setter
   - Line 349: winSetTooltipFunc method
   - Line 370: winGetTooltipFunc method
   - Line 406: m_tooltip callback member

4. **WinInstanceData.h** - `Code/GameEngine/Include/GameClient/WinInstanceData.h`
   - Line 166: m_tooltipDelay member

5. **GameWindowManager.cpp** - `Code/GameEngine/Source/GameClient/GUI/GameWindowManager.cpp`
   - Line 1015-1115: Window tooltip detection
   - Line 1221-1222: Tooltip window context
   - Line 1200-1232: Callback vs static text decision

6. **ControlBar.cpp** - `Code/GameEngine/Source/GameClient/GUI/ControlBar/ControlBar.cpp`
   - Line 129-134: commandButtonTooltip callback example
   - Line 1090-1136: Callback registration

7. **GameWindowManagerScript.cpp**
   - Line 1015-1031: parseTooltipDelay function
   - Line 2278: Parser table registration

8. **W3DMouse.cpp** - `Code/GameEngineDevice/Source/W3DDevice/GameClient/W3DMouse.cpp`
   - Line 591: drawTooltip call in render loop

---

## Configuration Reference

### INI Configuration Keys (Mouse Section)

```
TooltipFontName = Times New Roman       # Font name
TooltipFontSize = 12                    # Font size in points
TooltipFontIsBold = false               # Font weight

TooltipAnimateBackground = true         # Enable animation
TooltipFillTime = 50                    # Animation duration (ms)
TooltipDelayTime = 50                   # Before-show delay (ms)

TooltipTextColor = 220 220 220 255      # RGBA
TooltipHighlightColor = 255 255 0 255   # Animation highlight
TooltipShadowColor = 0 0 0 255          # Drop shadow
TooltipBackgroundColor = 20 20 0 127    # Background (semi-transparent)
TooltipBorderColor = 0 0 0 255          # Border

TooltipWidth = 15                       # % of screen width
UseTooltipAltTextColor = false          # House color text
UseTooltipAltBackColor = false          # House color background
AdjustTooltipAltColor = false           # Adjust brightness
```

### WND File Configuration

```
WINDOW MyButton BUTTON "MyButton"
{
    TOOLTIP "Tooltip text here"
    TOOLTIPDELAY 100  # Override global delay (ms)
}
```

---

## Complete Lifecycle

```
1. INITIALIZATION
   - Mouse singleton created
   - DisplayString allocated for tooltips
   - Default colors/fonts configured
   - INI values loaded

2. IDLE STATE
   - m_displayTooltip = FALSE
   - No rendering
   - m_stillTime updated on movement

3. HOVER START
   - Mouse enters window with tooltip
   - GameWindowManager detects
   - Callback registered? Call it
   - Otherwise: setCursorTooltip(text, delay)
   - m_stillTime = now (start delay)

4. DELAY PERIOD
   - Waiting: (now - m_stillTime) < delay
   - Mouse moves? m_stillTime = now (restart)
   - Animation: hidden

5. DISPLAY
   - Delay elapsed: m_displayTooltip = TRUE
   - m_highlightPos = 0 (animation start)
   - m_highlightUpdateStart = now

6. RENDERING
   - Every frame during display:
     - Calculate position (20px right, cursor Y)
     - Edge wrapping (left/up)
     - Draw background
     - Draw text with clipping
     - Update highlight: m_highlightPos += delta
     - Colors applied (normal or house colors)

7. HIDE EVENTS
   - Mouse movement: m_stillTime = now (restart delay)
   - Window change: reset context
   - Manual reset: TheMouse->resetTooltipDelay()
   - Fade: drawTooltip() returns early

8. TERMINATION
   - m_displayTooltip = FALSE
   - Return to idle
```

---

## Key Implementation Patterns

### Pattern 1: Two-Tier Delay
- Global default stored in Mouse singleton
- Per-window override in WinInstanceData
- Override only active if >= 0

### Pattern 2: Callback Chain
- Check for callback function pointer
- Call if exists (for custom tooltips)
- Fall back to static text if no callback

### Pattern 3: Edge Wrapping
- Right edge: flip 20px + width pixels left
- Bottom edge: flip height pixels up
- 4px margin for spill

### Pattern 4: Animation Timing
- Highlight position: (width * elapsed) / fillTime
- Maximum: width + 15px
- Linear interpolation

### Pattern 5: State Management
- Single boolean flag controls display
- Still time tracks delay threshold
- DisplayTooltip blocks rendering when FALSE

---

## OpenSAGE Implementation Notes

Key concepts to port:

1. **Delay System**: Track movement with timestamp, reset on any delta
2. **Callback Architecture**: Support both static text and callback functions
3. **Animation**: Linear progression of highlight position based on time
4. **Positioning**: Offset cursor, edge detection, dynamic wrapping
5. **Colors**: Separate channels for text/highlight/shadow/background/border
6. **Configuration**: INI-based global defaults + per-window overrides
7. **Rendering**: Use clipping regions for animation effect
8. **Integration**: Hook into window manager's hover detection

