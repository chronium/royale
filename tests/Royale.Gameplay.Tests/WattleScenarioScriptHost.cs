using WattleScript.Interpreter;

namespace Royale.Gameplay.Tests;

internal sealed class WattleScenarioScriptHost
{
    public DynValue Execute(string source)
    {
        Script script = CreateScript();
        return script.DoString(source);
    }

    internal static Script CreateScript()
    {
        var script = new Script(CoreModules.Preset_HardSandboxWattle);
        script.Options.Syntax = ScriptSyntax.Wattle;

        return script;
    }
}
