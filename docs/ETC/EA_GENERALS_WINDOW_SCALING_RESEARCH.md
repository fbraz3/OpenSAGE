# EA Generals Window Scaling & Responsive Layout System

## Executive Summary

EA Generals implements a **responsive window scaling system** that automatically adapts UI layouts to different screen resolutions. The system uses a **design resolution (creation resolution)** specified in `.wnd` files and scales all window positions and sizes proportionally based on the runtime display resolution.

---

## 1. Core Architecture

### 1.1 Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `WindowLayout` | `GameEngine/Include/GameClient/WindowLayout.h` | Container for groups of windows (screens) |
| `WindowLayoutInfo` | `GameEngine/Include/GameClient/GameWindowManager.h` | Metadata about loaded layouts including callbacks |
| `GameWindow` | `GameEngine/Include/GameClient/GameWindow.h` | Base class for all windows and GUI controls |
| `GameWindowManager` | `GameEngine/Include/GameClient/GameWindowManager.h` | Manages window creation, destruction, and event routing |
| `.wnd` Files | `Window/` directory | Script files defining window layouts |

### 1.2 Resolution Scaling Pipeline

```text
.wnd File (Design Resolution)
    ↓
Parse CREATIONRESOLUTION
    ↓
Calculate Scale Factors:
    xScale = CurrentDisplayWidth / DesignResolutionWidth
    yScale = CurrentDisplayHeight / DesignResolutionHeight
    ↓
Apply to All Window Coordinates:
    scaledX = designX * xScale
    scaledY = designY * yScale
    scaledWidth = designWidth * xScale
    scaledHeight = designHeight * yScale
    ↓
Position Windows on Screen
```

---

## 2. Resolution Scaling Implementation

### 2.1 Scaling Logic in `parseScreenRect()`

**File:** [references/generals_code/Generals/Code/GameEngine/Source/GameClient/GUI/GameWindowManagerScript.cpp](references/generals_code/Generals/Code/GameEngine/Source/GameClient/GUI/GameWindowManagerScript.cpp#L515-L545)

```cpp
// parseScreenRect - lines 515-545
static Bool parseScreenRect( char *token, char *buffer,
                             Int *x, Int *y, Int *width, Int *height )
{
    GameWindow *parent = peekWindow();
    IRegion2D screenRegion;
    ICoord2D createRes;  // creation resolution
    char *seps = " ,:=\n\r\t";
    char *c;

    // Parse UPPERLEFT coordinates
    c = strtok( NULL, seps );  // UPPERLEFT token
    c = strtok( NULL, seps );  // x position
    scanInt( c, screenRegion.lo.x );
    c = strtok( NULL, seps );  // y position
    scanInt( c, screenRegion.lo.y );

    // Parse BOTTOMRIGHT coordinates
    c = strtok( NULL, seps );  // BOTTOMRIGHT token
    c = strtok( NULL, seps );  // x position
    scanInt( c, screenRegion.hi.x );
    c = strtok( NULL, seps );  // y position
    scanInt( c, screenRegion.hi.y );

    // Parse CREATIONRESOLUTION (design resolution)
    c = strtok( NULL, seps );  // CREATIONRESOLUTION token
    c = strtok( NULL, seps );  // x creation resolution
    scanInt( c, createRes.x );
    c = strtok( NULL, seps );  // y creation resolution
    scanInt( c, createRes.y );

    // *** CRITICAL: Calculate scale factors ***
    Real xScale = (Real)TheDisplay->getWidth() / (Real)createRes.x;
    Real yScale = (Real)TheDisplay->getHeight() / (Real)createRes.y;
    
    // Apply scaling to all coordinates
    screenRegion.lo.x = (Int)((Real)screenRegion.lo.x * xScale);
    screenRegion.lo.y = (Int)((Real)screenRegion.lo.y * yScale);
    screenRegion.hi.x = (Int)((Real)screenRegion.hi.x * xScale);
    screenRegion.hi.y = (Int)((Real)screenRegion.hi.y * yScale);

    // Adjust coordinates relative to parent if window is a child
    if( parent )
    {
        ICoord2D parentScreenPos;
        parent->winGetScreenPosition( &parentScreenPos.x, &parentScreenPos.y );
        
        // Make coordinates relative to parent's top-left
        *x = screenRegion.lo.x - parentScreenPos.x;
        *y = screenRegion.lo.y - parentScreenPos.y;
    }
    else
    {
        // Root window - use absolute screen coordinates
        *x = screenRegion.lo.x;
        *y = screenRegion.lo.y;
    }

    // Calculate final size
    *width = screenRegion.hi.x - screenRegion.lo.x;
    *height = screenRegion.hi.y - screenRegion.lo.y;

    return TRUE;
}
```

### 2.2 .wnd File Format

Windows are defined in script files with the `CREATIONRESOLUTION` specified at the layout level:

```ini
FILE_VERSION = 2

// Layout-level metadata (version 2+)
WINDOW_LAYOUT:
    CREATION: ScreenRect     UPPERLEFT: 0 0   BOTTOMRIGHT: 1024 768   CREATIONRESOLUTION: 1024 768;
    INIT: [None];
    UPDATE: [None];
    SHUTDOWN: [None];
END

// Example window definition
WINDOW
    NAME: "MainMenu"
    STATUS: ENABLED
    STYLE: USER
    SCREENRECT: UPPERLEFT: 100 100   BOTTOMRIGHT: 400 300   CREATIONRESOLUTION: 1024 768;
END
```

### 2.3 Scale Factor Examples

| Current Resolution | Design Resolution | X Scale | Y Scale | Notes |
|-------------------|-------------------|---------|---------|-------|
| 800×600 | 1024×768 | 0.78125 | 0.78125 | Lower resolution - UI shrinks |
| 1024×768 | 1024×768 | 1.0 | 1.0 | Exact match - no scaling |
| 1280×960 | 1024×768 | 1.25 | 1.25 | Higher resolution - UI enlarges |
| 1920×1440 | 1024×768 | 1.875 | 1.875 | Much higher - UI significantly enlarged |
| 1920×1080 | 1024×768 | 1.875 | 1.40625 | Wide aspect ratio - independent scaling |

**Key insight:** X and Y scale factors are **independent**, allowing the system to handle arbitrary aspect ratios without letterboxing.

---

## 3. Window Layout System (`WindowLayout` class)

### 3.1 Class Definition

**File:** [references/generals_code/Generals/Code/GameEngine/Include/GameClient/WindowLayout.h](references/generals_code/Generals/Code/GameEngine/Include/GameClient/WindowLayout.h)

```cpp
class WindowLayout : public MemoryPoolObject
{
    MEMORY_POOL_GLUE_WITH_USERLOOKUP_CREATE( WindowLayout, "WindowLayoutPool" );

public:
    WindowLayout( void );

    // Screen management
    AsciiString getFilename( void );                ///< return source window filename
    Bool load( AsciiString filename );              ///< create windows and load from .wnd file
    void hide( Bool hide );                         ///< hide/unhide all windows on this screen
    Bool isHidden( void );                          ///< return visible state of screen
    void bringForward( void );                      ///< bring all windows in this screen forward

    // Window list management
    void addWindow( GameWindow *window );           ///< add window to screen
    void removeWindow( GameWindow *window );        ///< remove window from screen
    void destroyWindows( void );                    ///< destroy all windows in this screen
    GameWindow *getFirstWindow( void );             ///< get first window in list for screen

    // Lifecycle callbacks (custom per-layout)
    void runInit( void *userData = NULL );          ///< run the init method if available
    void runUpdate( void *userData = NULL );        ///< run the update method if available
    void runShutdown( void *userData = NULL );      ///< run the shutdown method if available
    
    void setInit( WindowLayoutInitFunc init );
    void setUpdate( WindowLayoutUpdateFunc update );
    void setShutdown( WindowLayoutShutdownFunc shutdown );

protected:
    AsciiString m_filenameString;                   ///< layout filename
    GameWindow *m_windowList;                       ///< head of window list
    GameWindow *m_windowTail;                       ///< end of window list
    Int m_windowCount;                              ///< total windows in layout
    Bool m_hidden;                                  ///< visibility state

    // Callbacks for custom layout behavior
    WindowLayoutInitFunc m_init;
    WindowLayoutUpdateFunc m_update;
    WindowLayoutShutdownFunc m_shutdown;
};
```

### 3.2 WindowLayoutInfo Structure

**File:** [references/generals_code/Generals/Code/GameEngine/Include/GameClient/GameWindowManager.h](references/generals_code/Generals/Code/GameEngine/Include/GameClient/GameWindowManager.h#L56-L68)

```cpp
class WindowLayoutInfo
{
public:
    WindowLayoutInfo();

    UnsignedInt version;                            ///< file version that was loaded
    WindowLayoutInitFunc init;                      ///< init callback (if specified)
    WindowLayoutUpdateFunc update;                  ///< update callback (if specified)
    WindowLayoutShutdownFunc shutdown;              ///< shutdown callback (if specified)
    AsciiString initNameString;                     ///< init method name as string
    AsciiString updateNameString;                   ///< update method name as string
    AsciiString shutdownNameString;                 ///< shutdown method name as string
    std::list<GameWindow *> windows;                ///< list of top-level windows in the layout
};
```

---

## 4. Window Class Integration

### 4.1 GameWindow Layout Fields

**File:** [references/generals_code/Generals/Code/GameEngine/Include/GameClient/GameWindow.h](references/generals_code/Generals/Code/GameEngine/Include/GameClient/GameWindow.h#L350-L360)

```cpp
class GameWindow : public MemoryPoolObject
{
    // ... other members ...

    // Layout-specific fields (only used for root windows in a layout)
    GameWindow *m_nextLayout;       ///< next in layout linked list
    GameWindow *m_prevLayout;       ///< prev in layout linked list
    WindowLayout *m_layout;         ///< layout this window is part of

    // Related methods
    void winSetNextInLayout( GameWindow *next );
    void winSetPrevInLayout( GameWindow *prev );
    void winSetLayout( WindowLayout *layout );
    WindowLayout *winGetLayout( void );
    GameWindow *winGetNextInLayout( void );
    GameWindow *winGetPrevInLayout( void );
};
```

### 4.2 Position & Size Methods

```cpp
Int winSetSize( Int width, Int height );           // set window size
Int winGetSize( Int *width, Int *height );         // get size
Int winSetPosition( Int x, Int y );                // set position (relative to parent)
Int winGetPosition( Int *x, Int *y );              // get position
Int winGetScreenPosition( Int *x, Int *y );        // get absolute screen position
Int winGetRegion( IRegion2D *region );              // get full region
```

---

## 5. Anchoring & Edge-Pinning (Limited in Core GUI)

### 5.1 Anchoring in WorldBuilder Tools

While the core game GUI system does **not** have built-in anchoring/pinning logic, the **WorldBuilder tool** demonstrates anchor-based resizing:

**File:** [references/generals_code/Generals/Code/Tools/WorldBuilder/src/WHeightMapEdit.cpp](references/generals_code/Generals/Code/Tools/WorldBuilder/src/WHeightMapEdit.cpp#L1790-L1828)

```cpp
Bool WorldHeightMapEdit::resize(Int newXSize, Int newYSize, Int newHeight, Int newBorder,
                                Bool anchorTop, Bool anchorBottom,
                                Bool anchorLeft, Bool anchorRight, Coord3D *pObjOffset)
{
    // When resizing terrain, maintain anchored edges
    
    if (anchorLeft)
    {
        // Keep left edge fixed, expand to the right
        offsetX = 0;
    }
    else if (anchorRight)
    {
        // Keep right edge fixed, shift left
        offsetX = oldWidth - newWidth;
    }
    // else: center the offset

    if (anchorBottom)
    {
        // Keep bottom edge fixed
        offsetY = 0;
    }
    else if (anchorTop)
    {
        // Keep top edge fixed, expand downward
        offsetY = oldHeight - newHeight;
    }
    // else: center the offset

    // Apply offsets to maintain pinned edges
    if (pObjOffset)
    {
        pObjOffset->x = offsetX;
        pObjOffset->y = offsetY;
    }

    return TRUE;
}
```

**Key limitation:** This anchoring is **not integrated into the core GUI system** and exists only in the tool. The game GUI relies purely on **uniform scaling**.

---

## 6. Display Resolution Management

### 6.1 Display Interface

**File:** [references/generals_code/Generals/Code/GameEngine/Include/GameClient/Display.h](references/generals_code/Generals/Code/GameEngine/Include/GameClient/Display.h)

```cpp
class Display
{
    // ... many methods ...
    
    virtual Int getWidth( void ) = 0;               // Get current display width
    virtual Int getHeight( void ) = 0;              // Get current display height
    virtual void setResolution( Int w, Int h ) = 0; // Change resolution
    
    // ... other rendering methods ...
};
```

### 6.2 Resolution Queries

Throughout the codebase, `TheDisplay->getWidth()` and `TheDisplay->getHeight()` are called to get runtime resolution for scaling calculations:

```cpp
// From W3DDisplay.cpp
if (resolutions[res].BitDepth >= 24 && resolutions[res].Width >= 800 && 
    (fabs((Real)resolutions[res].Width/(Real)resolutions[res].Height - 1.3333f)) < 0.01f)
{
    // Only accept 4:3 aspect ratio modes
}
```

---

## 7. Complete Window Creation Flow

### 7.1 From Script to Screen

**File:** [references/generals_code/Generals/Code/GameEngine/Source/GameClient/GUI/GameWindowManagerScript.cpp](references/generals_code/Generals/Code/GameEngine/Source/GameClient/GUI/GameWindowManagerScript.cpp#L2690-L2800)

```text
1. GameWindowManager::winCreateFromScript(filename, &info)
   ├─ Open .wnd file from Window/ directory
   ├─ Read FILE_VERSION
   │
   ├─ If version >= 2:
   │  └─ parseLayoutBlock()
   │     ├─ Parse CREATION rectangle with CREATIONRESOLUTION
   │     └─ Parse INIT/UPDATE/SHUTDOWN callbacks
   │
   ├─ While not EOF:
   │  ├─ Parse WINDOW sections
   │  ├─ For each WINDOW:
   │  │  ├─ parseWindow()
   │  │  ├─ parseScreenRect()
   │  │  │  ├─ Read design-time coordinates
   │  │  │  ├─ Read CREATIONRESOLUTION
   │  │  │  ├─ Calculate xScale = display.width / create.x
   │  │  │  ├─ Calculate yScale = display.height / create.y
   │  │  │  ├─ Apply scale to all coordinates
   │  │  │  └─ Return scaled position & size
   │  │  └─ Create GameWindow with scaled params
   │  │
   │  └─ Add to scriptInfo.windows
   │
   ├─ Copy scriptInfo to *info if provided
   └─ Return first window created

2. WindowLayout::load(filename, &info)
   ├─ Call winCreateFromScript()
   ├─ For each window in info.windows:
   │  └─ addWindow(window)
   │
   └─ Set layout callbacks from info

3. Windows are now ready to render at scaled coordinates
```

### 7.2 Example: Loading "MainMenu.wnd"

Suppose a file with design resolution 1024×768 is loaded on a 1920×1080 display:

```text
xScale = 1920 / 1024 = 1.875
yScale = 1080 / 768 ≈ 1.40625

Design Coordinates:     Scaled Coordinates:
X: 100                  X: 100 × 1.875 = 187.5 → 187
Y: 100                  Y: 100 × 1.40625 ≈ 140

Width: 300              Width: 300 × 1.875 = 562.5 → 562
Height: 200             Height: 200 × 1.40625 ≈ 281
```

---

## 8. Key Findings

### ✅ What EA Generals Implements

| Feature | Implementation | Location |
|---------|----------------|----------|
| **Design Resolution** | Stored per-window in .wnd scripts as `CREATIONRESOLUTION` | GameWindowManagerScript.cpp |
| **Uniform Scaling** | Linear scale factors (xScale, yScale) applied to all coordinates | parseScreenRect() |
| **Independent X/Y Scaling** | Different scale factors per axis (handles non-square aspect ratios) | scale calculations |
| **Hierarchical Positioning** | Parent-relative positioning after scaling | parseScreenRect() |
| **Layout Callbacks** | Init/Update/Shutdown callbacks per layout | WindowLayout, WindowLayoutInfo |
| **Screen Management** | WindowLayout groups windows for collective visibility/z-order | WindowLayout class |
| **Aspect Ratio Awareness** | Display validates 4:3 aspect ratio (historical) | W3DDisplay.cpp |

### ❌ What EA Generals Does NOT Implement

| Feature | Status | Reason |
|---------|--------|--------|
| **UI Anchoring** | Not in core GUI | Would require per-window anchor metadata (found only in WorldBuilder) |
| **Responsive Breakpoints** | Not implemented | No resolution-based layout switching |
| **Edge-Pinning** | Not in core GUI | All windows scale uniformly from their origin |
| **Font Scaling** | Limited | Fonts are fixed-size; only positions/sizes scale |
| **Letterboxing** | Not used | Independent X/Y scaling handles any aspect ratio |
| **Dynamic Layout Reflow** | Not implemented | Layout is static; only coordinates scale |

---

## 9. Configuration Examples

### 9.1 Common Design Resolutions

```ini
// Typical 4:3 aspect ratio designs
CREATIONRESOLUTION: 800 600      // 1:1 scale on 800×600
CREATIONRESOLUTION: 1024 768     // Common design target
CREATIONRESOLUTION: 1280 960     // High-res variant

// These scale to any runtime resolution
```

### 9.2 Multi-Layout Configuration

```cpp
WindowLayout mainMenuLayout;
mainMenuLayout.load("MainMenu.wnd");      // Design res: 1024×768
mainMenuLayout.setInit(MainMenu_Init);
mainMenuLayout.setUpdate(MainMenu_Update);

WindowLayout gameUILayout;
gameUILayout.load("GameUI.wnd");          // Design res: 1024×768
gameUILayout.setInit(GameUI_Init);
```

---

## 10. Migration Notes for OpenSAGE

### How to Adapt This System

1. **Preserve CREATIONRESOLUTION metadata** in `.wnd` file parser

2. **Calculate scale factors at load time**:

   ```csharp
   float xScale = currentWidth / designResolutionWidth;
   float yScale = currentHeight / designResolutionHeight;
   ```

3. **Apply scaling to all window coordinates**:

   ```csharp
   scaledX = (int)(designX * xScale);
   scaledY = (int)(designY * yScale);
   scaledWidth = (int)(designWidth * xScale);
   scaledHeight = (int)(designHeight * yScale);
   ```

4. **Support WindowLayout containers** for screen management

5. **Implement layout lifecycle callbacks** (Init/Update/Shutdown)

6. **No need for** per-window anchoring (unless adding as new feature)

---

## 11. File References Summary

| File | Lines | Purpose |
|------|-------|---------|
| GameWindowManagerScript.cpp | 515–545 | Scale calculation (`parseScreenRect`) |
| GameWindowManagerScript.cpp | 2690–2800 | Script loading (`winCreateFromScript`) |
| GameWindowManager.h | 56–68 | WindowLayoutInfo structure |
| WindowLayout.h | Full file | WindowLayout class definition |
| WindowLayout.cpp | Full file | WindowLayout implementation |
| GameWindow.h | 350–360 | Layout fields in GameWindow |
| W3DDisplay.cpp | 445, 463 | Resolution validation & aspect ratio |

---

## 12. Research Conclusion

**EA Generals uses a simple but effective two-step responsive system:**

1. **At design time:** UI layouts are created at a known resolution (e.g., 1024×768)
2. **At runtime:** All coordinates are scaled linearly based on `currentResolution / designResolution`

This approach is:

- ✅ **Simple** to implement and understand
- ✅ **Scalable** to any resolution (no letterboxing)
- ✅ **Aspect-ratio aware** (independent X/Y scaling)
- ⚠️ **Limited** (no per-element anchoring or responsive breakpoints)
- ⚠️ **Static** (no dynamic reflow or element repositioning)

For modern implementations, this could be enhanced with optional anchoring, breakpoints, and responsive layout modes, but the core algorithm is sound and reusable.
