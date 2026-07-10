namespace Royale.Client.UI;

public sealed class TelemetryVisibilityController
{
    public bool Visible { get; private set; } = true;

    public void Toggle() => Visible = !Visible;
}
