using OpenSage.Input;
using OpenSage.Mathematics;

namespace OpenSage.Logic;

/// <summary>
/// Routes input messages to the SelectionSystem.
/// Handles drag-to-select, single-click selection, and right-click panning.
/// </summary>
internal sealed class SelectionInputHandler : InputMessageHandler
{
    private readonly SelectionSystem _selectionSystem;
    private bool _isLeftMouseDown;
    private bool _isRightMouseDown;
    private Point2D _lastMousePosition;

    public override HandlingPriority Priority => HandlingPriority.BoxSelectionPriority;

    public SelectionInputHandler(SelectionSystem selectionSystem)
    {
        _selectionSystem = selectionSystem;
    }

    public override InputMessageResult HandleMessage(InputMessage message, in TimeInterval gameTime)
    {
        switch (message.MessageType)
        {
            case InputMessageType.MouseMove:
                _lastMousePosition = message.Value.MousePosition;
                
                // Update hover information
                _selectionSystem.OnHoverSelection(message.Value.MousePosition);
                
                // If left mouse is down, update drag selection
                if (_isLeftMouseDown && _selectionSystem.Selecting)
                {
                    _selectionSystem.OnDragSelection(message.Value.MousePosition);
                    return InputMessageResult.Handled;
                }
                
                // If right mouse is down, update pan
                if (_isRightMouseDown && _selectionSystem.Panning)
                {
                    // Panning is visual only, not consuming input
                }
                
                return InputMessageResult.NotHandled;

            case InputMessageType.MouseLeftButtonDown:
                _isLeftMouseDown = true;
                _lastMousePosition = message.Value.MousePosition;
                
                // Start selection drag
                _selectionSystem.OnStartDragSelection(message.Value.MousePosition);
                return InputMessageResult.Handled;

            case InputMessageType.MouseLeftButtonUp:
                _isLeftMouseDown = false;
                
                // End selection drag
                if (_selectionSystem.Selecting)
                {
                    _selectionSystem.OnEndDragSelection();
                    return InputMessageResult.Handled;
                }
                
                return InputMessageResult.NotHandled;

            case InputMessageType.MouseRightButtonDown:
                _isRightMouseDown = true;
                _lastMousePosition = message.Value.MousePosition;
                
                // Start right-click drag (pan/deselect)
                _selectionSystem.OnStartRightClickDrag(message.Value.MousePosition);
                return InputMessageResult.Handled;

            case InputMessageType.MouseRightButtonUp:
                _isRightMouseDown = false;
                
                // End right-click drag
                if (_selectionSystem.Panning)
                {
                    _selectionSystem.OnEndRightClickDrag(_lastMousePosition);
                    return InputMessageResult.Handled;
                }
                
                return InputMessageResult.NotHandled;
        }

        return InputMessageResult.NotHandled;
    }
}
