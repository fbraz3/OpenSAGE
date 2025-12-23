using OpenSage.Mathematics;
using Veldrid;

namespace OpenSage.Graphics;

public sealed class TextureCopier : DisposableBase
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private readonly GraphicsDevice _graphicsDevice;
    private readonly CommandList _commandList;
    private readonly SpriteBatch _intermediateSpriteBatch;

    private int _logCounter = 0;

    public TextureCopier(IGame game, in OutputDescription outputDescription)
    {
        _graphicsDevice = game.GraphicsDevice;

        _commandList = AddDisposable(_graphicsDevice.ResourceFactory.CreateCommandList());

        _intermediateSpriteBatch = AddDisposable(new SpriteBatch(
            game.GraphicsLoadContext,
            BlendStateDescription.SingleDisabled,
            outputDescription));
    }

    public void Execute(Texture source, Framebuffer destination)
    {
        if (_logCounter++ % 60 == 0)
        {
            Logger.Info($"[TextureCopier] Source: {source.Width}x{source.Height}, Format: {source.Format}");
            Logger.Info($"[TextureCopier] Destination: {destination.Width}x{destination.Height}");
        }

        _commandList.Begin();

        _commandList.PushDebugGroup("Blitting to framebuffer");

        _commandList.SetFramebuffer(destination);

        _intermediateSpriteBatch.Begin(
            _commandList,
            _graphicsDevice.PointSampler,
            new SizeF(source.Width, source.Height),
            ignoreAlpha: true);

        _intermediateSpriteBatch.DrawImage(
            source,
            null,
            new RectangleF(0, 0, (int)source.Width, (int)source.Height),
            ColorRgbaF.White);

        _intermediateSpriteBatch.End();

        _commandList.PopDebugGroup();

        _commandList.End();

        _graphicsDevice.SubmitCommands(_commandList);
    }
}
