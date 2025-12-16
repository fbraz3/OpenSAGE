using OpenSage.Diagnostics;
using OpenSage.Graphics.Rendering;
using Veldrid;

namespace OpenSage.Graphics;

public sealed class GraphicsSystem : GameSystem
{
    private readonly RenderContext _renderContext;

    internal RenderPipeline RenderPipeline { get; private set; }

    public Texture ShadowMap => RenderPipeline.ShadowMap;
    public Texture ReflectionMap => RenderPipeline.ReflectionMap;
    public Texture RefractionMap => RenderPipeline.RefractionMap;

    public GraphicsSystem(IGame game)
        : base(game)
    {
        _renderContext = new RenderContext();
    }

    public override void Initialize()
    {
        RenderPipeline = AddDisposable(new RenderPipeline(Game));
    }

    internal void Draw(in TimeInterval gameTime)
    {
        // PLAN-015: Profile rendering pipeline execution
        // Note: gameTime is 'in' parameter, must be copied for use in lambdas
        var time = gameTime;

        PerfGather.Profile("RenderPipeline.Setup", () => {
            _renderContext.ContentManager = Game.ContentManager;
            _renderContext.GraphicsDevice = Game.GraphicsDevice;
            _renderContext.Scene3D = Game.Scene3D;
            _renderContext.Scene2D = Game.Scene2D;
            _renderContext.RenderTarget = Game.Panel.Framebuffer;
            _renderContext.GameTime = time;
        });

        PerfGather.Profile("RenderPipeline.Execute", () =>
            RenderPipeline.Execute(_renderContext));
    }
}
