using System.Reflection;
using System.Runtime.ExceptionServices;
using WattleScript.Interpreter;

namespace Royale.Gameplay.Tests;

internal sealed class WattleScenarioScriptHost
{
    public DynValue Execute(string source, ScenarioApi? scenario = null)
    {
        bool ownsScenario = scenario is null;
        scenario ??= new ScenarioApi();

        try
        {
            Script script = CreateScript(scenario);
            try
            {
                return script.DoString(source);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is ScriptRuntimeException inner)
            {
                ExceptionDispatchInfo.Capture(inner).Throw();
                throw;
            }
        }
        finally
        {
            if (ownsScenario)
                scenario.Dispose();
        }
    }

    internal static Script CreateScript(ScenarioApi? scenario = null)
    {
        RegisterScenarioTypes();

        var script = new Script(CoreModules.Preset_HardSandboxWattle);
        script.Options.Syntax = ScriptSyntax.Wattle;
        script.Globals.Set("scenario", UserData.Create(scenario ?? new ScenarioApi()));

        return script;
    }

    private static void RegisterScenarioTypes()
    {
        Register<ScenarioApi>();
        Register<ScenarioServerApi>();
        Register<ScenarioPlayersApi>();
        Register<ScenarioObservationsApi>();
        Register<ScenarioAssertApi>();
        Register<ScenarioClockApi>();
        Register<ScenarioArtifactsApi>();
        Register<ScenarioEventsApi>();
        Register<ScenarioEventApi>();
        Register<ScenarioPlayerApi>();
        Register<ScenarioSnapshotApi>();
        Register<ScenarioPlayerSnapshotApi>();
        Register<ScenarioPlayerDebugStateApi>();
        Register<ScenarioPlayerDebugWeaponApi>();
        Register<ScenarioVector3Api>();
        Register<ScenarioLookSnapshotApi>();
        Register<ScenarioHealthSnapshotApi>();
        Register<ScenarioWeaponSnapshotApi>();
        Register<ScenarioMatchSnapshotApi>();
        Register<ScenarioSafeZoneSnapshotApi>();
    }

    private static void Register<T>()
    {
        if (!UserData.IsTypeRegistered<T>())
            UserData.RegisterType<T>(InteropAccessMode.Reflection);
    }
}
