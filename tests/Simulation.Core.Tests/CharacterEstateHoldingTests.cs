using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Simulation.Core;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterEstateHoldingTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private readonly ITestOutputHelper output;

    public CharacterEstateHoldingTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void ContractIsVersionOneDefaultEmptyAndStateExposesNoMutator()
    {
        CharacterEstateHoldingWorldState state = NewState(CreateCharacters(1));

        Assert.Equal(1, CharacterEstateHoldingContractVersions.Snapshot);
        Assert.Equal(1, CharacterEstateHoldingContractVersions.State);
        Assert.Equal(1, CharacterEstateHoldingContractVersions.AuthoritativeQuery);
        Assert.Equal(64, CharacterEstateHoldingLimits.HoldingsPerCharacter);
        Assert.Equal(
            "simulation.character_estate_holdings",
            CharacterEstateHoldingSystem.SystemId);
        Assert.Equal(1, CharacterEstateHoldingSystem.Version);
        Assert.Empty(state.Holdings);
        Assert.Equal(
            Serialize(CharacterEstateHoldingWorldSnapshot.Empty),
            Serialize(state.CaptureSnapshot()));

        string[] mutatorPrefixes = ["Add", "Apply", "Remove", "Replace", "Transfer", "Update"];
        Assert.DoesNotContain(
            typeof(CharacterEstateHoldingWorldState).GetMethods(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.DeclaredOnly),
            method => (method.IsPublic || method.IsAssembly)
                && mutatorPrefixes.Any(prefix => method.Name.StartsWith(
                    prefix,
                    StringComparison.Ordinal)));
    }

    [Fact]
    public void ConstructionRejectsNullUnsupportedAndMalformedSnapshotShape()
    {
        CharacterWorldState characters = CreateCharacters(1);

        Assert.Throws<SimulationValidationException>(() =>
            new CharacterEstateHoldingWorldState(null!, characters, Date));
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterEstateHoldingWorldState(
                CharacterEstateHoldingWorldSnapshot.Empty,
                null!,
                Date));
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterEstateHoldingWorldState(
                CharacterEstateHoldingWorldSnapshot.Empty,
                characters,
                default));
        AssertInvalid(
            CharacterEstateHoldingWorldSnapshot.Empty with { ContractVersion = 2 },
            characters);
        AssertInvalid(
            CharacterEstateHoldingWorldSnapshot.Empty with { Holdings = null! },
            characters);
        AssertInvalid(
            CharacterEstateHoldingWorldSnapshot.Empty with { Holdings = [null!] },
            characters);
    }

    [Fact]
    public void ConstructionRejectsInvalidVersionIdNamespaceAndDuplicateEstate()
    {
        CharacterWorldState characters = CreateCharacters(2);
        CharacterEstateHoldingState valid = Holding("estate:test/valid", Character(0));

        AssertInvalid(
            CharacterEstateHoldingWorldSnapshot.Empty with
            {
                Holdings = [valid with { ContractVersion = 2 }],
            },
            characters);
        AssertInvalid(
            CharacterEstateHoldingWorldSnapshot.Empty with
            {
                Holdings = [valid with { EstateId = default }],
            },
            characters);
        AssertInvalid(
            CharacterEstateHoldingWorldSnapshot.Empty with
            {
                Holdings = [valid with
                {
                    EstateId = new EntityId("property:test/wrong_namespace"),
                }],
            },
            characters);
        AssertInvalid(
            CharacterEstateHoldingWorldSnapshot.Empty with
            {
                Holdings = [valid, valid with { OwnerCharacterId = Character(1) }],
            },
            characters);
    }

    [Fact]
    public void ConstructionRejectsInvalidUnknownAndFutureBornOwners()
    {
        CharacterWorldState characters = CreateCharacters(
            2,
            birthDates: new Dictionary<EntityId, CampaignDate>
            {
                [Character(1)] = Date,
            });
        CharacterEstateHoldingState valid = Holding("estate:test/owner", Character(0));

        AssertInvalid(
            CharacterEstateHoldingWorldSnapshot.Empty with
            {
                Holdings = [valid with { OwnerCharacterId = default }],
            },
            characters);
        AssertInvalid(
            CharacterEstateHoldingWorldSnapshot.Empty with
            {
                Holdings = [valid with
                {
                    OwnerCharacterId = new EntityId("character:estate/missing"),
                }],
            },
            characters);
        AssertInvalid(
            CharacterEstateHoldingWorldSnapshot.Empty with
            {
                Holdings = [valid with { OwnerCharacterId = Character(1) }],
            },
            characters,
            Date.AddDays(-1));
    }

    [Fact]
    public void OwnerQueryDoesNotReusePersistedHoldingBirthEligibility()
    {
        AuthoritativeCharacterProfile futureProfile = Assert.Single(
            CreateCharacters(1).Profiles) with
        {
            BirthDate = Date.AddDays(2),
        };
        CharacterEstateHoldingWorldState state = new(
            CharacterEstateHoldingWorldSnapshot.Empty,
            new StubCharacterQuery(futureProfile),
            Date);

        Assert.Empty(state.GetHoldingsOwnedBy(futureProfile.CharacterId));
    }

    [Fact]
    public void ExactlySixtyFourHoldingsPerCharacterAreAcceptedAndSixtyFiveRejected()
    {
        CharacterWorldState characters = CreateCharacters(2);
        CharacterEstateHoldingState[] exact = Enumerable.Range(
                0,
                CharacterEstateHoldingLimits.HoldingsPerCharacter)
            .Select(index => Holding($"estate:test/exact_{index:D3}", Character(0)))
            .ToArray();
        CharacterEstateHoldingWorldState accepted = new(
            new CharacterEstateHoldingWorldSnapshot(
                CharacterEstateHoldingContractVersions.Snapshot,
                exact),
            characters,
            Date);

        Assert.Equal(64, accepted.GetHoldingsOwnedBy(Character(0)).Count);
        AssertInvalid(
            accepted.CaptureSnapshot() with
            {
                Holdings =
                [
                    .. accepted.Holdings,
                    Holding("estate:test/overflow", Character(0)),
                ],
            },
            characters);

        CharacterEstateHoldingWorldState splitAcrossOwners = new(
            new CharacterEstateHoldingWorldSnapshot(
                CharacterEstateHoldingContractVersions.Snapshot,
                [
                    .. exact,
                    Holding("estate:test/second_owner", Character(1)),
                ]),
            characters,
            Date);
        Assert.Equal(65, splitAcrossOwners.Holdings.Count);
    }

    [Fact]
    public void DeadIncapacitatedAndCaptiveOwnersRetainHoldings()
    {
        Dictionary<EntityId, CharacterConditionState> conditions = new()
        {
            [Character(1)] = new CharacterConditionState(
                CharacterVitalStatus.Dead,
                CharacterHealthStatus.Critical,
                IsIncapacitated: true,
                CharacterCustodyStatus.Free,
                null),
            [Character(2)] = CharacterConditionState.Default with
            {
                IsIncapacitated = true,
            },
            [Character(3)] = new CharacterConditionState(
                CharacterVitalStatus.Alive,
                CharacterHealthStatus.Healthy,
                IsIncapacitated: false,
                CharacterCustodyStatus.Captive,
                Character(0)),
        };
        CharacterWorldState characters = CreateCharacters(4, conditions);
        CharacterEstateHoldingWorldState state = NewState(
            characters,
            Holding("estate:test/dead", Character(1)),
            Holding("estate:test/incapacitated", Character(2)),
            Holding("estate:test/captive", Character(3)));

        Assert.Single(state.GetHoldingsOwnedBy(Character(1)));
        Assert.Single(state.GetHoldingsOwnedBy(Character(2)));
        Assert.Single(state.GetHoldingsOwnedBy(Character(3)));
    }

    [Fact]
    public void ConstructionCanonicalizesShuffleAndQueriesReturnDefensiveCopies()
    {
        CharacterWorldState characters = CreateCharacters(3);
        CharacterEstateHoldingState[] shuffled =
        [
            Holding("estate:test/z", Character(0)),
            Holding("estate:test/a", Character(1)),
            Holding("estate:test/m", Character(0)),
        ];
        CharacterEstateHoldingWorldState first = NewState(characters, shuffled);
        CharacterEstateHoldingWorldState second = NewState(
            characters,
            shuffled.Reverse().ToArray());

        Assert.Equal(
            Serialize(first.CaptureSnapshot()),
            Serialize(second.CaptureSnapshot()));
        Assert.Equal(Checksum(characters, first), Checksum(characters, second));
        Assert.Equal(
            ["estate:test/a", "estate:test/m", "estate:test/z"],
            first.Holdings.Select(item => item.EstateId.Value));
        Assert.Equal(
            ["estate:test/m", "estate:test/z"],
            first.GetHoldingsOwnedBy(Character(0)).Select(item => item.EstateId.Value));
        Assert.True(first.TryGetHolding(
            new EntityId("estate:test/m"),
            out CharacterEstateHoldingState? queried));
        Assert.Equal(Character(0), queried.OwnerCharacterId);
        Assert.False(first.TryGetHolding(new EntityId("estate:test/missing"), out _));
        Assert.Throws<SimulationValidationException>(() => first.GetHoldingsOwnedBy(
            new EntityId("character:estate/missing")));

        CharacterEstateHoldingState[] global =
            Assert.IsType<CharacterEstateHoldingState[]>(first.Holdings);
        global[0] = global[0] with { OwnerCharacterId = Character(2) };
        CharacterEstateHoldingState[] owned = Assert.IsType<CharacterEstateHoldingState[]>(
            first.GetHoldingsOwnedBy(Character(0)));
        owned[0] = owned[0] with { EstateId = new EntityId("estate:test/replaced") };
        CharacterEstateHoldingState[] captured = Assert.IsType<CharacterEstateHoldingState[]>(
            first.CaptureSnapshot().Holdings);
        captured[0] = captured[0] with { OwnerCharacterId = Character(2) };

        Assert.Equal(Character(1), first.Holdings[0].OwnerCharacterId);
        Assert.Equal(
            ["estate:test/m", "estate:test/z"],
            first.GetHoldingsOwnedBy(Character(0)).Select(item => item.EstateId.Value));
    }

    [Fact]
    public void OwnerReplacementPreservesStableEstateIdentityRepresentationally()
    {
        CharacterWorldState characters = CreateCharacters(2);
        EntityId estateId = new("estate:test/inherited_identity");
        CharacterEstateHoldingWorldState before = NewState(
            characters,
            new CharacterEstateHoldingState(
                CharacterEstateHoldingContractVersions.State,
                estateId,
                Character(0)));
        CharacterEstateHoldingWorldState after = NewState(
            characters,
            Assert.Single(before.Holdings) with { OwnerCharacterId = Character(1) });
        CharacterEstateHoldingWorldState changedIdentity = NewState(
            characters,
            Assert.Single(before.Holdings) with
            {
                EstateId = new EntityId("estate:test/different_identity"),
            });

        CharacterEstateHoldingState prior = Assert.Single(before.Holdings);
        CharacterEstateHoldingState replaced = Assert.Single(after.Holdings);
        Assert.Equal(prior.EstateId, replaced.EstateId);
        Assert.NotEqual(prior.OwnerCharacterId, replaced.OwnerCharacterId);
        Assert.NotEqual(Checksum(characters, before), Checksum(characters, after));
        Assert.NotEqual(
            Checksum(characters, before),
            Checksum(characters, changedIdentity));
        Assert.True(after.TryGetHolding(estateId, out CharacterEstateHoldingState? queried));
        Assert.Equal(Character(1), queried.OwnerCharacterId);
    }

    [Fact]
    public void ThousandCharacterEightThousandHoldingFixtureRecordsRawMeasurementWithoutThreshold()
    {
        CharacterWorldState characters = CreateCharacters(1_000);
        CharacterEstateHoldingWorldSnapshot input = new(
            CharacterEstateHoldingContractVersions.Snapshot,
            Enumerable.Range(0, 1_000)
                .SelectMany(characterIndex => Enumerable.Range(0, 8)
                    .Select(holdingIndex => Holding(
                        $"estate:performance/c{characterIndex:D4}_h{holdingIndex:D2}",
                        Character(characterIndex))))
                .Reverse()
                .ToArray());
        Stopwatch construction = Stopwatch.StartNew();
        CharacterEstateHoldingWorldState state = new(input, characters, Date);
        construction.Stop();

        Stopwatch queryAndSnapshot = Stopwatch.StartNew();
        for (int index = 0; index < 1_000; index++)
        {
            Assert.Equal(8, state.GetHoldingsOwnedBy(Character(index)).Count);
        }

        CharacterEstateHoldingWorldSnapshot snapshot = state.CaptureSnapshot();
        string serialized = Serialize(snapshot);
        queryAndSnapshot.Stop();
        Assert.Equal(8_000, snapshot.Holdings.Count);
        Assert.NotEmpty(serialized);

        WorldState integratedWorld = WorldState.Create(
            Date,
            99,
            [],
            GeographicWorldSnapshot.Empty,
            characters.CaptureSnapshot(),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            snapshot);
        CampaignSimulation integrated = new(integratedWorld);
        Stopwatch checksumTimer = Stopwatch.StartNew();
        SimulationChecksum checksum = SimulationChecksum.Compute(
            integrated.World.CaptureSnapshot());
        checksumTimer.Stop();
        SaveEnvelope envelope = SaveEnvelope.Create(
            "0.1.0",
            [],
            integrated,
            DateTimeOffset.Parse(
                "2026-07-15T00:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture));
        string savePath = Path.Combine(
            Path.GetTempPath(),
            $"sp04c3-performance-{Guid.NewGuid():N}.save.gz");
        Stopwatch saveTimer = Stopwatch.StartNew();
        try
        {
            SaveStore store = new();
            store.SaveAtomic(savePath, envelope);
            saveTimer.Stop();
            long saveBytes = new FileInfo(savePath).Length;
            Stopwatch loadTimer = Stopwatch.StartNew();
            SaveEnvelope loaded = store.Load(savePath);
            loadTimer.Stop();
            Assert.Equal(checksum.Value, loaded.Checksum);
            Assert.Equal(8_000, loaded.Snapshot.CharacterEstateHoldings.Holdings.Count);
            output.WriteLine(
                $"SP-04C3 full-world fixture: checksum_ms={checksumTimer.Elapsed.TotalMilliseconds:F3}; "
                + $"save_ms={saveTimer.Elapsed.TotalMilliseconds:F3}; "
                + $"load_ms={loadTimer.Elapsed.TotalMilliseconds:F3}; "
                + $"save_bytes={saveBytes}; checksum={checksum.Value}");
        }
        finally
        {
            File.Delete(savePath);
        }

        output.WriteLine(
            $"SP-04C3 raw fixture: construction_ms={construction.Elapsed.TotalMilliseconds:F3}; "
            + $"owner_queries_snapshot_json_ms={queryAndSnapshot.Elapsed.TotalMilliseconds:F3}; "
            + $"holdings={snapshot.Holdings.Count}; json_chars={serialized.Length}");
    }

    private static CharacterEstateHoldingWorldState NewState(
        CharacterWorldState characters,
        params CharacterEstateHoldingState[] holdings) => new(
        new CharacterEstateHoldingWorldSnapshot(
            CharacterEstateHoldingContractVersions.Snapshot,
            holdings),
        characters,
        Date);

    private static CharacterEstateHoldingState Holding(string estateId, EntityId owner) => new(
        CharacterEstateHoldingContractVersions.State,
        new EntityId(estateId),
        owner);

    private static CharacterWorldState CreateCharacters(
        int count,
        IReadOnlyDictionary<EntityId, CharacterConditionState>? conditions = null,
        IReadOnlyDictionary<EntityId, CampaignDate>? birthDates = null)
    {
        CharacterDefinition[] definitions = Enumerable.Range(0, count)
            .Select(index =>
            {
                EntityId id = Character(index);
                EntityId nameKey = new($"loc:estate/character_{index:D3}");
                return new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    id,
                    nameKey,
                    birthDates is not null
                        && birthDates.TryGetValue(id, out CampaignDate birthDate)
                        ? birthDate
                        : new CampaignDate(160, 1, 1),
                    [],
                    [],
                    [],
                    [],
                    [],
                    new StructuredCharacterName(nameKey, null),
                    CharacterContentOrigin.LegacyUnknown(id),
                    null,
                    null,
                    []);
            })
            .ToArray();
        CharacterState[] states = definitions
            .Select(definition => new CharacterState(
                CharacterContractVersions.State,
                definition.Id,
                [],
                [],
                conditions is not null
                    && conditions.TryGetValue(
                        definition.Id,
                        out CharacterConditionState? condition)
                        ? condition
                        : CharacterConditionState.Default))
            .ToArray();
        return new CharacterWorldState(
            new CharacterWorldSnapshot(
                CharacterContractVersions.Snapshot,
                [],
                definitions,
                [],
                [],
                states,
                [],
                []),
            Date);
    }

    private static EntityId Character(int index) =>
        new($"character:estate/c{index:D3}");

    private static SimulationChecksum Checksum(
        CharacterWorldState characters,
        CharacterEstateHoldingWorldState holdings) =>
        SimulationChecksum.Compute(WorldState.Create(
            Date,
            99,
            [],
            GeographicWorldSnapshot.Empty,
            characters.CaptureSnapshot(),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            holdings.CaptureSnapshot()).CaptureSnapshot());

    private static void AssertInvalid(
        CharacterEstateHoldingWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters,
        CampaignDate? snapshotDate = null) =>
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterEstateHoldingWorldState(
                snapshot,
                characters,
                snapshotDate ?? Date));

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed class StubCharacterQuery(AuthoritativeCharacterProfile profile)
        : IAuthoritativeCharacterWorldQuery
    {
        public IReadOnlyList<AuthoritativeCharacterProfile> Profiles => [profile];

        public IReadOnlyList<AuthoritativeHouseholdView> Households => [];

        public bool TryGetCharacterProfile(
            EntityId id,
            [NotNullWhen(true)] out AuthoritativeCharacterProfile? result)
        {
            result = id == profile.CharacterId ? profile : null;
            return result is not null;
        }

        public bool TryGetHousehold(
            EntityId id,
            [NotNullWhen(true)] out AuthoritativeHouseholdView? household)
        {
            household = null;
            return false;
        }
    }
}
