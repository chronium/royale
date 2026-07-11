namespace Royale.Server.Networking;

public readonly record struct BotInputDelayDiagnostics(
    int SampledHumanCount,
    double AverageOneWayLatencyMilliseconds,
    int EffectiveDelayTicks);
