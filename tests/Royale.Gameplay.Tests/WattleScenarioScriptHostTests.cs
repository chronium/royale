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
    public void ScenarioClockWaitTicksAdvancesServerClockAndSnapshots()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            scenario.observe.latest(player);

            var tick = scenario.clock.waitTicks(3);
            var snapshot = scenario.observe.latest(player);

            scenario.assert.equal(3, tick);
            scenario.assert.equal(3, snapshot.serverTick);
            scenario.assert.equal(3, scenario.server.tick);
            scenario.assert.equal(3, scenario.clock.tick);
            scenario.assert.equal(10000, scenario.clock.maxWaitTicks);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioClockWaitTicksProcessesQueuedPlayerInput()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();

            scenario.assert.isTrue(scenario.players.input(player, {
                sequence = 107,
                clientTick = 1070,
                moveX = 0.0,
                moveY = 1.0,
                yawRadians = 0.0,
                pitchRadians = 0.0
            }));

            scenario.clock.waitTicks(1);
            var snapshot = scenario.observe.latest(player);

            scenario.assert.equal(107, snapshot.acknowledgedInputSequence);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioClockWaitUntilReturnsImmediatelyWhenPredicateIsAlreadyTrue()
    {
        const string source = """
            scenario.server.start("graybox");

            var completed = scenario.clock.waitUntil(5, function() {
                return scenario.clock.tick == 0;
            });

            scenario.assert.isTrue(completed);
            scenario.assert.equal(0, scenario.clock.tick);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioClockWaitUntilStepsUntilPredicateBecomesTrue()
    {
        const string source = """
            scenario.server.start("graybox");

            var completed = scenario.clock.waitUntil(5, function() {
                return scenario.clock.tick == 3;
            });

            scenario.assert.isTrue(completed);
            scenario.assert.equal(3, scenario.clock.tick);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioClockWaitUntilReturnsFalseAfterExactlyMaxTicks()
    {
        const string source = """
            scenario.server.start("graybox");

            var completed = scenario.clock.waitUntil(4, function() {
                return false;
            });

            scenario.assert.equal(false, completed);
            scenario.assert.equal(4, scenario.clock.tick);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Theory]
    [InlineData("scenario.clock.waitTicks(0);")]
    [InlineData("scenario.clock.waitTicks(10001);")]
    [InlineData("scenario.clock.waitUntil(-1, function() { return true; });")]
    [InlineData("scenario.clock.waitUntil(10001, function() { return true; });")]
    [InlineData("scenario.clock.waitUntil(1, nil);")]
    [InlineData("scenario.clock.waitUntil(1, 42);")]
    [InlineData("scenario.clock.waitUntil(1, function() { return 42; });")]
    [InlineData("scenario.clock.waitUntil(1, function() { error(\"predicate failure\"); });")]
    public void ScenarioClockWaitHelpersRejectInvalidArgumentsOrPredicateFailures(string statement)
    {
        string source = $$"""
            scenario.server.start("graybox");
            {{statement}}
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
    }

    [Theory]
    [InlineData("scenario.clock.waitTicks(1);")]
    [InlineData("scenario.clock.waitUntil(0, function() { return true; });")]
    public void ScenarioClockWaitHelpersRequireRunningServer(string statement)
    {
        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(statement));
    }

    [Fact]
    public void ScenarioPlayersInputSubmitsValidCommandAndAcknowledgesSequence()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();

            var accepted = scenario.players.input(player, {
                sequence = 7,
                clientTick = 120,
                moveX = 0.0,
                moveY = 1.0,
                yawRadians = 0.25,
                pitchRadians = 0.0
            });

            scenario.server.step(1);
            var snapshot = scenario.observe.latest(player);

            scenario.assert.isTrue(accepted);
            scenario.assert.equal(7, snapshot.acknowledgedInputSequence);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioPlayersInputTracksSeparatePlayerAcknowledgements()
    {
        const string source = """
            scenario.server.start("graybox");
            var first = scenario.players.connect();
            var second = scenario.players.connect();

            scenario.assert.isTrue(scenario.players.input(first, {
                sequence = 11,
                clientTick = 201,
                moveX = 0.0,
                moveY = 1.0,
                yawRadians = 0.0,
                pitchRadians = 0.0
            }));
            scenario.assert.isTrue(scenario.players.input(second, {
                sequence = 22,
                clientTick = 202,
                moveX = 1.0,
                moveY = 0.0,
                yawRadians = 1.0,
                pitchRadians = 0.0
            }));

            scenario.server.step(1);
            var firstSnapshot = scenario.observe.latest(first);
            var secondSnapshot = scenario.observe.latest(second);

            scenario.assert.equal(11, firstSnapshot.acknowledgedInputSequence);
            scenario.assert.equal(22, secondSnapshot.acknowledgedInputSequence);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioPlayersInputCombinesButtonBooleans()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();

            scenario.assert.isTrue(scenario.players.input(player, {
                sequence = 31,
                clientTick = 301,
                moveX = 0.0,
                moveY = 0.0,
                yawRadians = 0.0,
                pitchRadians = 0.0,
                fire = true,
                reload = true,
                crouch = true
            }));

            scenario.server.step(1);
            var snapshot = scenario.observe.latest(player);

            scenario.assert.equal(31, snapshot.acknowledgedInputSequence);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioPlayersInputReturnsFalseForProtocolInvalidCommand()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();

            var accepted = scenario.players.input(player, {
                sequence = 40,
                clientTick = 400,
                moveX = 2.0,
                moveY = 0.0,
                yawRadians = 0.0,
                pitchRadians = 0.0
            });

            scenario.server.step(1);
            var snapshot = scenario.observe.latest(player);

            scenario.assert.equal(false, accepted);
            scenario.assert.equal(nil, snapshot.acknowledgedInputSequence);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Theory]
    [InlineData("sequence")]
    [InlineData("clientTick")]
    [InlineData("moveX")]
    [InlineData("moveY")]
    [InlineData("yawRadians")]
    [InlineData("pitchRadians")]
    public void ScenarioPlayersInputRequiresNumericFields(string removedField)
    {
        string source = $$"""
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            var command = {
                sequence = 50,
                clientTick = 500,
                moveX = 0.0,
                moveY = 0.0,
                yawRadians = 0.0,
                pitchRadians = 0.0
            };
            command.{{removedField}} = nil;
            scenario.players.input(player, command);
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
    }

    [Theory]
    [InlineData("sequence")]
    [InlineData("clientTick")]
    [InlineData("moveX")]
    [InlineData("moveY")]
    [InlineData("yawRadians")]
    [InlineData("pitchRadians")]
    public void ScenarioPlayersInputRejectsNonNumberFields(string field)
    {
        string source = $$"""
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            var command = {
                sequence = 60,
                clientTick = 600,
                moveX = 0.0,
                moveY = 0.0,
                yawRadians = 0.0,
                pitchRadians = 0.0
            };
            command.{{field}} = "invalid";
            scenario.players.input(player, command);
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
    }

    [Fact]
    public void ScenarioPlayersInputRejectsNonBooleanButtonFields()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            scenario.players.input(player, {
                sequence = 70,
                clientTick = 700,
                moveX = 0.0,
                moveY = 0.0,
                yawRadians = 0.0,
                pitchRadians = 0.0,
                fire = 1
            });
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
    }

    [Fact]
    public void ScenarioPlayersInputRequiresConnectedPlayer()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            scenario.players.disconnect(player);
            scenario.players.input(player, {
                sequence = 80,
                clientTick = 800,
                moveX = 0.0,
                moveY = 0.0,
                yawRadians = 0.0,
                pitchRadians = 0.0
            });
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
    }

    [Fact]
    public void ScenarioPlayersInputRequiresRunningServer()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            scenario.server.stop();
            scenario.players.input(player, {
                sequence = 90,
                clientTick = 900,
                moveX = 0.0,
                moveY = 0.0,
                yawRadians = 0.0,
                pitchRadians = 0.0
            });
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
    }

    [Fact]
    public void ScenarioPlayersInputRequiresPlayerHandle()
    {
        const string source = """
            scenario.server.start("graybox");
            scenario.players.input(nil, {
                sequence = 100,
                clientTick = 1000,
                moveX = 0.0,
                moveY = 0.0,
                yawRadians = 0.0,
                pitchRadians = 0.0
            });
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
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
