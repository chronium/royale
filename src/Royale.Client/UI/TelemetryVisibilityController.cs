namespace Royale.Client.UI;

public sealed class TelemetryVisibilityController
{
    public TelemetryVisibilityController(bool visible = true) => Visible = visible;

    public bool Visible { get; private set; }

    public void Toggle() => Visible = !Visible;
}
