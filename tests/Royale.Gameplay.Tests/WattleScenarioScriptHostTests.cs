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
                and scenario.artifacts != nil
                and scenario.events != nil
                and scenario.network != nil;
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
    public void ScenarioServerStartUdpRejectsInvalidMapId()
    {
        const string source = """
            scenario.server.startUdp("missing-map");
            """;

        ScriptRuntimeException ex = Assert.Throws<ScriptRuntimeException>(
            () => new WattleScenarioScriptHost().Execute(source));

        Assert.Contains("scenario.server.startUdp failed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScenarioServerStartUdpRejectsDuplicateStarts()
    {
        const string source = """
            scenario.server.startUdp("graybox");
            scenario.server.startUdp("graybox");
            """;

        ScriptRuntimeException ex = Assert.Throws<ScriptRuntimeException>(
            () => new WattleScenarioScriptHost().Execute(source));

        Assert.Contains("already running", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScenarioPlayersConnectOverUdpWithNonzeroIds()
    {
        const string source = """
            scenario.server.startUdp("graybox");
            var player = scenario.players.connect();
            var snapshot = scenario.observe.latest(player);

            scenario.assert.isTrue(player.connectionId != 0);
            scenario.assert.isTrue(player.playerId != 0);
            scenario.assert.equal(player.playerId, snapshot.localPlayerId);
            scenario.assert.equal(1, scenario.players.count);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioUdpDisconnectAndStopDisposeRuntime()
    {
        const string source = """
            scenario.server.startUdp("graybox");
            var player = scenario.players.connect();

            scenario.players.disconnect(player);
            scenario.assert.equal(false, player.isConnected);
            scenario.assert.equal(0, scenario.players.count);

            scenario.server.stop();
            scenario.assert.equal(false, scenario.server.isRunning);

            scenario.server.startUdp("graybox");
            var second = scenario.players.connect();
            scenario.assert.isTrue(second.playerId != 0);
            scenario.server.stop();

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioNetworkConditionsSupportReadbackClearEventsAndPerPlayerIsolation()
    {
        const string source = """
            scenario.server.startUdp("graybox");
            var first = scenario.players.connect();
            var second = scenario.players.connect();

            scenario.network.set(first, {
                latencyMs = 50,
                jitterMs = 10,
                lossChance = 0.1,
                duplicateChance = 0.05,
                reorderChance = 0.2,
                randomSeed = 123
            });

            var firstConditions = scenario.network.current(first);
            var secondConditions = scenario.network.current(second);
            scenario.assert.equal(50, firstConditions.latencyMs);
            scenario.assert.equal(10, firstConditions.jitterMs);
            scenario.assert.equal(0.1, firstConditions.lossChance);
            scenario.assert.equal(0.05, firstConditions.duplicateChance);
            scenario.assert.equal(0.2, firstConditions.reorderChance);
            scenario.assert.equal(123, firstConditions.randomSeed);
            scenario.assert.equal(0, secondConditions.latencyMs);
            scenario.assert.equal(0, secondConditions.lossChance);
            scenario.assert.equal(nil, secondConditions.randomSeed);

            scenario.assert.equal("network.conditions.changed", scenario.events.latest.type);
            scenario.assert.equal(first.playerId, scenario.events.latest.playerId);
            scenario.assert.equal(
                "latencyMs=50;jitterMs=10;lossChance=0.1;duplicateChance=0.05;reorderChance=0.2;randomSeed=123",
                scenario.events.latest.detail);

            scenario.network.set(first, { latencyMs = 25 });
            var replaced = scenario.network.current(first);
            scenario.assert.equal(25, replaced.latencyMs);
            scenario.assert.equal(0, replaced.jitterMs);
            scenario.assert.equal(0, replaced.lossChance);
            scenario.assert.equal(0, replaced.duplicateChance);
            scenario.assert.equal(0, replaced.reorderChance);
            scenario.assert.equal(nil, replaced.randomSeed);

            scenario.network.clear(first);
            var cleared = scenario.network.current(first);
            scenario.assert.equal(0, cleared.latencyMs);
            scenario.assert.equal(0, cleared.jitterMs);
            scenario.assert.equal(0, cleared.lossChance);
            scenario.assert.equal(0, cleared.duplicateChance);
            scenario.assert.equal(0, cleared.reorderChance);
            scenario.assert.equal(nil, cleared.randomSeed);
            scenario.assert.equal(
                "latencyMs=0;jitterMs=0;lossChance=0;duplicateChance=0;reorderChance=0;randomSeed=nil",
                scenario.events.latest.detail);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioNetworkConditionsRejectInProcessScenarios()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            scenario.network.set(player, { latencyMs = 10 });
            """;

        ScriptRuntimeException ex = Assert.Throws<ScriptRuntimeException>(
            () => new WattleScenarioScriptHost().Execute(source));

        Assert.Contains("scenario.server.startUdp", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScenarioNetworkConditionsRejectDisconnectedAndStoppedPlayers()
    {
        const string disconnectedSource = """
            scenario.server.startUdp("graybox");
            var player = scenario.players.connect();
            scenario.players.disconnect(player);
            scenario.network.current(player);
            """;
        const string stoppedSource = """
            scenario.server.startUdp("graybox");
            var player = scenario.players.connect();
            scenario.server.stop();
            scenario.network.clear(player);
            """;

        Assert.Throws<ScriptRuntimeException>(
            () => new WattleScenarioScriptHost().Execute(disconnectedSource));
        Assert.Throws<ScriptRuntimeException>(
            () => new WattleScenarioScriptHost().Execute(stoppedSource));
    }

    [Fact]
    public void ScenarioNetworkConditionsRejectForeignPlayerHandles()
    {
        using var firstScenario = new ScenarioApi();
        using var secondScenario = new ScenarioApi();
        firstScenario.StartUdpServer("graybox");
        secondScenario.StartUdpServer("graybox");
        ScenarioPlayerApi firstPlayer = firstScenario.ConnectPlayer();
        secondScenario.ConnectPlayer();

        ScriptRuntimeException ex = Assert.Throws<ScriptRuntimeException>(
            () => secondScenario.network.current(firstPlayer));

        Assert.Contains("different scenario runtime", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("{ unknown = 1 }")]
    [InlineData("{ latencyMs = -1 }")]
    [InlineData("{ latencyMs = \"fast\" }")]
    [InlineData("{ latencyMs = 0.0 / 0.0 }")]
    [InlineData("{ jitterMs = -0.1 }")]
    [InlineData("{ jitterMs = 1.0 / 0.0 }")]
    [InlineData("{ lossChance = 1.1 }")]
    [InlineData("{ duplicateChance = \"often\" }")]
    [InlineData("{ reorderChance = -0.1 }")]
    [InlineData("{ randomSeed = 1.5 }")]
    [InlineData("{ randomSeed = 2147483648 }")]
    public void ScenarioNetworkConditionsRejectMalformedTables(string conditions)
    {
        string source = $$"""
            scenario.server.startUdp("graybox");
            var player = scenario.players.connect();
            scenario.network.set(player, {{conditions}});
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
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
    public void ScenarioAssertNearPassesWithinTolerance()
    {
        DynValue result = new WattleScenarioScriptHost().Execute(
            "scenario.assert.near(1.0, 1.05, 0.1); return true;");

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioAssertNearFailsOutsideToleranceWithDiagnosticValues()
    {
        ScriptRuntimeException ex = Assert.Throws<ScriptRuntimeException>(
            () => new WattleScenarioScriptHost().Execute("scenario.assert.near(1.0, 1.2, 0.1);"));

        Assert.Contains("scenario.assert.near failed", ex.Message, StringComparison.Ordinal);
        Assert.Contains("expected 1", ex.Message, StringComparison.Ordinal);
        Assert.Contains("got 1.2", ex.Message, StringComparison.Ordinal);
        Assert.Contains("tolerance 0.1", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("scenario.assert.near(\"1\", 1, 0.1);")]
    [InlineData("scenario.assert.near(1, \"1\", 0.1);")]
    [InlineData("scenario.assert.near(1, 1, \"0.1\");")]
    [InlineData("scenario.assert.near(1, 1, -0.1);")]
    public void ScenarioAssertNearRejectsInvalidArguments(string statement)
    {
        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(statement));
    }

    [Fact]
    public void ScenarioAssertStatePassesForTruePredicate()
    {
        const string source = """
            var ready = true;
            scenario.assert.state("ready flag", function() {
                return ready;
            });
            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioAssertStateFailsWithSuppliedStateName()
    {
        const string source = """
            scenario.assert.state("player is alive", function() {
                return false;
            });
            """;

        ScriptRuntimeException ex = Assert.Throws<ScriptRuntimeException>(
            () => new WattleScenarioScriptHost().Execute(source));

        Assert.Contains("player is alive", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("scenario.assert.state(\"bad\", nil);")]
    [InlineData("scenario.assert.state(\"bad\", 42);")]
    [InlineData("scenario.assert.state(\"bad\", function() { return 42; });")]
    [InlineData("scenario.assert.state(\"bad\", function() { error(\"predicate failure\"); });")]
    public void ScenarioAssertStateRejectsInvalidPredicates(string statement)
    {
        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(statement));
    }

    [Fact]
    public void ScenarioAssertEventPassesForRecordedHostEvent()
    {
        const string source = """
            scenario.server.start("graybox");
            scenario.assert.event("server.started");
            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioAssertEventFailsForMissingHostEvent()
    {
        ScriptRuntimeException ex = Assert.Throws<ScriptRuntimeException>(
            () => new WattleScenarioScriptHost().Execute("scenario.assert.event(\"player.connected\");"));

        Assert.Contains("player.connected", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScenarioAssertEventuallySucceedsImmediatelyWithoutAdvancing()
    {
        const string source = """
            scenario.server.start("graybox");

            scenario.assert.eventually(5, function() {
                return scenario.clock.tick == 0;
            }, "already true");

            scenario.assert.equal(0, scenario.clock.tick);
            scenario.assert.event("clock.wait.satisfied");
            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioAssertEventuallySucceedsAfterAdvancingTicks()
    {
        const string source = """
            scenario.server.start("graybox");

            scenario.assert.eventually(5, function() {
                return scenario.clock.tick == 3;
            }, "tick reached");

            scenario.assert.equal(3, scenario.clock.tick);
            scenario.assert.event("server.stepped");
            scenario.assert.event("clock.wait.satisfied");
            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioAssertEventuallyThrowsAfterTimeoutAndAdvancesExactlyMaxTicks()
    {
        const string source = """
            scenario.server.start("graybox");
            scenario.assert.eventually(4, function() {
                return false;
            }, "never true");
            """;

        using var scenario = new ScenarioApi();

        ScriptRuntimeException ex = Assert.Throws<ScriptRuntimeException>(
            () => new WattleScenarioScriptHost().Execute(source, scenario));

        Assert.Contains("never true", ex.Message, StringComparison.Ordinal);
        Assert.Equal(4UL, scenario.clock.tick);
        Assert.Equal(4, scenario.events.types.Count(t => t == "server.stepped"));
        Assert.Equal("clock.wait.timeout", scenario.events.latest?.type);
    }

    [Theory]
    [InlineData("scenario.assert.eventually(-1, function() { return true; }, \"bad\");")]
    [InlineData("scenario.assert.eventually(10001, function() { return true; }, \"bad\");")]
    [InlineData("scenario.assert.eventually(1, nil, \"bad\");")]
    [InlineData("scenario.assert.eventually(1, 42, \"bad\");")]
    [InlineData("scenario.assert.eventually(1, function() { return 42; }, \"bad\");")]
    [InlineData("scenario.assert.eventually(1, function() { error(\"predicate failure\"); }, \"bad\");")]
    public void ScenarioAssertEventuallyRejectsInvalidArgumentsOrPredicateFailures(string statement)
    {
        string source = $$"""
            scenario.server.start("graybox");
            {{statement}}
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
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
    public void ScenarioSnapshotExposesReadOnlyPlayerMatchAndSafeZoneState()
    {
        const string source = """
            scenario.server.start("graybox");
            var first = scenario.players.connect();
            var second = scenario.players.connect();
            scenario.clock.waitTicks(1);

            var snapshot = scenario.observe.latest(first);
            var players = snapshot.players;
            var firstPlayer = snapshot.player(first.playerId);
            var secondPlayer = snapshot.player(second.playerId);

            scenario.assert.equal(first.playerId, players[1].playerId);
            scenario.assert.equal(second.playerId, players[2].playerId);
            scenario.assert.equal(first.playerId, firstPlayer.playerId);
            scenario.assert.equal(second.playerId, secondPlayer.playerId);
            scenario.assert.equal(nil, snapshot.player(9999));

            scenario.assert.near(firstPlayer.position.x, players[1].position.x, 0.0001);
            scenario.assert.near(firstPlayer.position.y, players[1].position.y, 0.0001);
            scenario.assert.near(firstPlayer.position.z, players[1].position.z, 0.0001);
            scenario.assert.near(firstPlayer.velocity.x, players[1].velocity.x, 0.0001);
            scenario.assert.near(firstPlayer.velocity.y, players[1].velocity.y, 0.0001);
            scenario.assert.near(firstPlayer.velocity.z, players[1].velocity.z, 0.0001);
            scenario.assert.near(firstPlayer.look.yawRadians, players[1].look.yawRadians, 0.0001);
            scenario.assert.near(firstPlayer.look.pitchRadians, players[1].look.pitchRadians, 0.0001);

            scenario.assert.equal(firstPlayer.currentHealth, firstPlayer.health.current);
            scenario.assert.equal(firstPlayer.maxHealth, firstPlayer.health.max);
            scenario.assert.isTrue(firstPlayer.health.current > 0);
            scenario.assert.isTrue(firstPlayer.alive);
            scenario.assert.isTrue(firstPlayer.weapon.weaponId != nil);
            scenario.assert.isTrue(firstPlayer.weapon.ammoInMagazine >= 0);
            scenario.assert.isTrue(firstPlayer.weapon.reserveAmmo >= 0);
            scenario.assert.isTrue(firstPlayer.weapon.nextAllowedFireTick >= 0);
            scenario.assert.equal(false, firstPlayer.weapon.isReloading);

            scenario.assert.equal(snapshot.livingPlayerCount, snapshot.match.livingPlayerCount);
            scenario.assert.equal("WaitingForPlayers", snapshot.match.phase);
            scenario.assert.equal(nil, snapshot.match.winnerPlayerId);
            scenario.assert.isTrue(snapshot.safeZone.currentRadius > 0);
            scenario.assert.isTrue(snapshot.safeZone.targetRadius > 0);
            scenario.assert.isTrue(snapshot.safeZone.center.x != nil);

            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
    }

    [Fact]
    public void ScenarioNestedSnapshotWrappersDoNotExposeDirectMutation()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            var snapshot = scenario.observe.latest(player);
            snapshot.player(player.playerId).position.x = 99;
            """;

        Assert.Throws<ScriptRuntimeException>(() => new WattleScenarioScriptHost().Execute(source));
    }

    [Fact]
    public void ScenarioEventsExposeHistoryAndCanBeCleared()
    {
        const string source = """
            scenario.server.start("graybox");
            var player = scenario.players.connect();
            var rejected = scenario.players.input(player, {
                sequence = 40,
                clientTick = 400,
                moveX = 2.0,
                moveY = 0.0,
                yawRadians = 0.0,
                pitchRadians = 0.0
            });

            scenario.assert.equal(false, rejected);
            scenario.assert.equal("player.input.rejected", scenario.events.latest.type);
            scenario.assert.equal(player.playerId, scenario.events.latest.playerId);
            scenario.assert.equal("sequence=40", scenario.events.latest.detail);
            scenario.assert.event("server.started");
            scenario.assert.event("player.connected");
            scenario.assert.event("player.input.rejected");

            var countBeforeClear = scenario.events.count;
            var types = scenario.events.types;
            scenario.assert.equal("server.started", types[1]);
            scenario.assert.isTrue(countBeforeClear >= 3);

            scenario.events.clear();
            scenario.assert.equal(0, scenario.events.count);
            scenario.assert.equal(nil, scenario.events.latest);
            return true;
            """;

        DynValue result = new WattleScenarioScriptHost().Execute(source);

        Assert.True(result.Boolean);
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
