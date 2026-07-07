using WattleScript.Interpreter;

namespace Royale.Gameplay.Tests;

[Collection(Box3DNativeTestCollection.Name)]
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

    [Fact]
    public void ExecuteExposesSingleScenarioGlobalWithGroupedApis()
    {
        const string source = """
            return scenario != nil
                and scenario.server != nil
                and scenario.players != nil
                and scenario.observe != nil
                and scenario.assert != nil
                and scenario.clock != nil
                and scenario.artifacts != nil;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioServerStartsStopsAndReportsTick()
    {
        const string source = """
            scenario.server.start("graybox");
            var started = scenario.server.isRunning;
            var tick = scenario.server.tick;
            scenario.server.stop();
            return started and tick == 0 and !scenario.server.isRunning and scenario.clock.tick == 0;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioPlayersConnectAndObserveDistinctLocalPlayerIds()
    {
        const string source = """
            scenario.server.start("graybox");
            var first = scenario.players.connect();
            var second = scenario.players.connect();
            scenario.server.step(1);
            var firstSnapshot = scenario.observe.latest(first);
            var secondSnapshot = scenario.observe.latest(second);

            scenario.assert.equal(2, scenario.players.count);
            scenario.assert.equal(first.playerId, firstSnapshot.localPlayerId);
            scenario.assert.equal(second.playerId, secondSnapshot.localPlayerId);
            scenario.assert.isTrue(first.playerId != second.playerId);
            scenario.assert.equal(2, firstSnapshot.connectedPlayerCount);
            scenario.assert.equal(2, firstSnapshot.livingPlayerCount);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioServerStepAdvancesLatestSnapshotAndClockTick()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            scenario.observe.latest(player);

            scenario.server.step(3);
            var snapshot = scenario.observe.latest(player);

            scenario.assert.equal(3, snapshot.serverTick);
            scenario.assert.equal(3, scenario.server.tick);
            scenario.assert.equal(3, scenario.clock.tick);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioAssertHelpersThrowScriptRuntimeFailures()
    {
        Assert.Throws<ScriptRuntimeException>(
            () => new WattleScenarioScriptHost().Execute("scenario.assert.equal(1, 2);"));
        Assert.Throws<ScriptRuntimeException>(
            () => new WattleScenarioScriptHost().Execute("scenario.assert.isTrue(false);"));
    }

    [Fact]
    public void ScenarioAssertTrueHelperIsAvailableThroughReservedNameIndex()
    {
        DynValue result = new WattleScenarioScriptHost().Execute(
            "scenario.assert[\"true\"](true); return true;");

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioArtifactsRecordInMemoryMetadata()
    {
        const string source = """
            scenario.artifacts.record("snapshot", "tick=0");
            scenario.artifacts.record("summary", "ok");
            var names = scenario.artifacts.names;

            scenario.assert.equal(2, scenario.artifacts.count);
            scenario.assert.equal("snapshot", names[1]);
            scenario.assert.equal("summary", names[2]);

            return true;
            """;

        using var scenario = new ScenarioApi();

        DynValue result = new WattleScenarioScriptHost().Execute(source, scenario);

        Assert.True(result.Boolean);
        Assert.Equal("tick=0", scenario.artifacts.Records["snapshot"]);
        Assert.Equal("ok", scenario.artifacts.Records["summary"]);
    }

    [Fact]
    public void ScenarioApiDoesNotExposeDirectAuthoritativePlayerMutation()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            player.playerId = 99;
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
    }

    [Fact]
    public void ScenarioSnapshotDoesNotExposeDirectAuthoritativeStateMutation()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            var snapshot = scenario.observe.latest(player);
            snapshot.localPlayerId = 99;
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
    }

    [Fact]
    public void ScenarioApisFailExplicitlyAfterServerStop()
    {
        const string source = """
            scenario.server.start("graybox");
            scenario.players.connect();
            scenario.server.stop();
            scenario.server.step(1);
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
    }

    [Fact]
    public void ScenarioApisFailExplicitlyAfterPlayerDisconnect()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            scenario.players.disconnect(player);
            scenario.observe.latest(player);
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
    }
}
