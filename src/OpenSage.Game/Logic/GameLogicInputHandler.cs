using OpenSage.Input;
using Veldrid;

namespace OpenSage.Logic;

/// <summary>
/// Routes input messages to the OrderGeneratorSystem.
/// Handles terrain clicks for unit commands (move, attack, build).
/// </summary>
internal sealed class GameLogicInputHandler : InputMessageHandler
{
    private readonly OrderGeneratorSystem _orderGenerator;
    private KeyModifiers _keyModifiers;

    public override HandlingPriority Priority => HandlingPriority.OrderGeneratorPriority;

    public GameLogicInputHandler(OrderGeneratorSystem orderGenerator)
    {
        _orderGenerator = orderGenerator;
    }

    public override InputMessageResult HandleMessage(InputMessage message, in TimeInterval gameTime)
    {
        switch (message.MessageType)
        {
            case InputMessageType.MouseMove:
                // Update world position based on mouse movement
                _orderGenerator.UpdatePosition(message.Value.MousePosition.ToVector2());
                return InputMessageResult.NotHandled;

            case InputMessageType.MouseLeftButtonDown:
                // Left-click activates current order (move, attack, build, etc.)
                _orderGenerator.TryActivate(_keyModifiers);
                return InputMessageResult.Handled;

            case InputMessageType.MouseRightButtonDown:
                // Right-click during drag
                _orderGenerator.UpdateDrag(message.Value.MousePosition.ToVector2());
                return InputMessageResult.NotHandled;

            case InputMessageType.KeyDown:
                // Track Ctrl key state
                if (message.Value.Key == Key.ControlLeft || message.Value.Key == Key.ControlRight)
                {
                    _keyModifiers |= KeyModifiers.Ctrl;
                    return InputMessageResult.Handled;
                }
                break;

            case InputMessageType.KeyUp:
                // Untrack Ctrl key state
                if (message.Value.Key == Key.ControlLeft || message.Value.Key == Key.ControlRight)
                {
                    _keyModifiers &= ~KeyModifiers.Ctrl;
                    return InputMessageResult.Handled;
                }
                break;
        }

        return InputMessageResult.NotHandled;
    }
}
