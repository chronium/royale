using WattleScript.Interpreter;

namespace Royale.Gameplay.Tests;

public sealed class WattleScenarioScriptHostTests
{
    [Fact]
    public void ExecuteRunsWattleSyntax()
    {
        const string source = """
            var ticks = 1;
            ticks++;
            if ticks == 2 {
                return true;
            }
            return false;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ExecuteReturnsScriptValue()
    {
        DynValue result = new WattleScenarioScriptHost().Execute("return 40 + 2;");

        Assert.Equal(42.0, result.Number);
    }

    [Fact]
    public void WattleSyntaxIsNotRunningAsDefaultLuaSyntax()
    {
        const string source = """
            var ticks = 1;
            ticks++;
            return ticks;
            """;
        var luaScript = new Script(CoreModules.Preset_HardSandbox);

        Assert.Throws<SyntaxErrorException>(() => luaScript.DoString(source));
    }

    [Fact]
    public void HardSandboxDoesNotExposeIoOrUnsafeOsExecute()
    {
        const string source = "return io == nil and (os == nil or os.execute == nil);";

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }
}
