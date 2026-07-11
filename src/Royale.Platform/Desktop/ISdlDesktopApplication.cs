using SDL;

namespace Royale.Platform.Desktop;

public interface ISdlDesktopApplication
{
    void Initialize(SdlDesktopHost host);
    void ProcessEvent(in SDL_Event sdlEvent);
    void Update(SdlFrameTime time);
    void FixedUpdate(SdlFixedTickTime time);
    void Render(SdlFrameTime time);
}
