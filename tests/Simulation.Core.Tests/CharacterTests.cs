using System.Diagnostics;
using System.Text.Json;
using Simulation.Core;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterTests
{
    private static readonly CampaignDate CurrentDate = new(200, 5, 10);
    private static readonly EntityId CharacterA = new("character:test/a");
    private static readonly EntityId CharacterB = new("character:test/b");
    private static readonly EntityId CharacterC = new("character:test/c");
    private static readonly EntityId FamilyA = new("family:test/a");
    private static readonly EntityId FamilyB = new("family:test/b");
    private static readonly EntityId HouseholdA = new("household:test/a");
    private static readonly EntityId HouseholdB = new("household:test/b");
    private readonly ITestOutputHelper output;

    public CharacterTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void CharacterConstructionCanonicalizesShuffledTopLevelInput()
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterWorldSnapshot shuffled = source with
        {
            IdentityDefinitions = source.IdentityDefinitions.Reverse().ToArray(),
            CharacterDefinitions = source.CharacterDefinitions.Reverse().ToArray(),
            CharacterStates = source.CharacterStates.Reverse().ToArray(),
        };

        CharacterWorldState first = new(source, CurrentDate);
        CharacterWorldState second = new(shuffled, CurrentDate);

        Assert.Equal(Serialize(first.CaptureSnapshot()), Serialize(second.CaptureSnapshot()));
        Assert.Equal([CharacterA, CharacterB, CharacterC], second.Profiles.Select(item => item.CharacterId));
        Assert.Equal([CharacterA, CharacterB], Assert.Single(second.Households).MemberIds);
    }

    [Fact]
    public void CharacterConstructionRejectsUnsupportedVersions()
    {
        CharacterWorldSnapshot source = Fixture();

        AssertInvalid(source with { ContractVersion = 2 });
        AssertInvalid(source with
        {
            IdentityDefinitions = ReplaceFirst(
                source.IdentityDefinitions,
                source.IdentityDefinitions[0] with { ContractVersion = 2 }),
        });
        AssertInvalid(source with
        {
            CharacterDefinitions = ReplaceFirst(
                source.CharacterDefinitions,
                source.CharacterDefinitions[0] with { ContractVersion = 2 }),
        });
        AssertInvalid(source with
        {
            FamilyDefinitions = [source.FamilyDefinitions[0] with { ContractVersion = 2 }],
        });
        AssertInvalid(source with
        {
            HouseholdDefinitions = [source.HouseholdDefinitions[0] with { ContractVersion = 2 }],
        });
        AssertInvalid(source with
        {
            CharacterStates = ReplaceFirst(
                source.CharacterStates,
                source.CharacterStates[0] with { ContractVersion = 2 }),
        });
        AssertInvalid(source with
        {
            FamilyStates = [source.FamilyStates[0] with { ContractVersion = 2 }],
        });
        AssertInvalid(source with
        {
            HouseholdStates = [source.HouseholdStates[0] with { ContractVersion = 2 }],
        });
    }

    [Fact]
    public void CharacterConstructionRejectsDuplicateAndInvalidDefinitionIds()
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterDefinition first = source.CharacterDefinitions[0];

        AssertInvalid(source with
        {
            CharacterDefinitions = [.. source.CharacterDefinitions, first with { }],
        });
        AssertInvalid(source with
        {
            FamilyDefinitions = [source.FamilyDefinitions[0] with { Id = first.Id }],
        });
        AssertInvalid(source with
        {
            CharacterDefinitions = ReplaceFirst(
                source.CharacterDefinitions,
                first with { Id = default }),
        });
        AssertInvalid(source with
        {
            CharacterDefinitions = ReplaceFirst(
                source.CharacterDefinitions,
                first with { NameKey = default }),
        });
    }

    [Fact]
    public void CharacterConstructionRejectsDuplicateAndInvalidStateIds()
    {
        CharacterWorldSnapshot source = Fixture();

        AssertInvalid(source with
        {
            CharacterStates = [.. source.CharacterStates, source.CharacterStates[0] with { }],
        });
        AssertInvalid(source with
        {
            FamilyStates = [.. source.FamilyStates, source.FamilyStates[0] with { }],
        });
        AssertInvalid(source with
        {
            HouseholdStates = [.. source.HouseholdStates, source.HouseholdStates[0] with { }],
        });
        AssertInvalid(source with
        {
            CharacterStates = ReplaceFirst(
                source.CharacterStates,
                source.CharacterStates[0] with { CharacterId = default }),
        });
        AssertInvalid(source with
        {
            HouseholdStates = [source.HouseholdStates[0] with { HeadCharacterId = default }],
        });
    }

    [Fact]
    public void CharacterConstructionRequiresExactlyOneStatePerDefinition()
    {
        CharacterWorldSnapshot source = Fixture();

        AssertInvalid(source with { CharacterStates = source.CharacterStates.Skip(1).ToArray() });
        AssertInvalid(source with { FamilyStates = [] });
        AssertInvalid(source with { HouseholdStates = [] });
        AssertInvalid(source with
        {
            CharacterStates = [
                .. source.CharacterStates,
                new CharacterState(CharacterContractVersions.State, new EntityId("character:test/d"), []),
            ],
        });
        AssertInvalid(source with
        {
            FamilyStates = [source.FamilyStates[0] with { FamilyId = HouseholdA }],
        });
        AssertInvalid(source with
        {
            HouseholdStates = [source.HouseholdStates[0] with { HouseholdId = FamilyA }],
        });
    }

    [Theory]
    [InlineData(CharacterIdentityKind.Ability)]
    [InlineData(CharacterIdentityKind.Aptitude)]
    [InlineData(CharacterIdentityKind.Trait)]
    [InlineData(CharacterIdentityKind.Ambition)]
    [InlineData(CharacterIdentityKind.Reputation)]
    public void CharacterConstructionRequiresMatchingIdentityDefinitionKinds(
        CharacterIdentityKind referenceKind)
    {
        CharacterWorldSnapshot source = Fixture();
        EntityId wrongId = source.IdentityDefinitions
            .First(item => item.Kind != referenceKind)
            .Id;
        CharacterDefinition character = source.CharacterDefinitions.Single(item => item.Id == CharacterA);
        CharacterDefinition mistyped = SetIdentityReferences(character, referenceKind, [wrongId]);

        AssertInvalid(source with
        {
            CharacterDefinitions = source.CharacterDefinitions
                .Select(item => item.Id == CharacterA ? mistyped : item)
                .ToArray(),
        });
    }

    [Fact]
    public void CharacterConstructionRejectsDanglingIdentityDefinition()
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterDefinition character = source.CharacterDefinitions.Single(item => item.Id == CharacterA);

        AssertInvalid(source with
        {
            CharacterDefinitions = source.CharacterDefinitions
                .Select(item => item.Id == CharacterA
                    ? item with { AbilityIds = [new EntityId("ability:test/missing")] }
                    : item)
                .ToArray(),
        });
        AssertInvalid(source with
        {
            IdentityDefinitions = source.IdentityDefinitions
                .Select(item => item.Id == character.AbilityIds[0]
                    ? item with { Kind = (CharacterIdentityKind)999 }
                    : item)
                .ToArray(),
        });
    }

    [Fact]
    public void CharacterConstructionRejectsInvalidParentage()
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterState child = source.CharacterStates.Single(item => item.CharacterId == CharacterC);

        AssertInvalid(WithCharacterState(source, child with
        {
            ParentIds = [new EntityId("character:test/missing")],
        }));
        AssertInvalid(WithCharacterState(source, child with { ParentIds = [CharacterC] }));

        CharacterState parent = source.CharacterStates.Single(item => item.CharacterId == CharacterA);
        CharacterWorldSnapshot cycle = WithCharacterState(source, parent with { ParentIds = [CharacterC] });
        AssertInvalid(cycle);

        CharacterDefinition parentDefinition = source.CharacterDefinitions.Single(item => item.Id == CharacterA);
        CharacterDefinition childDefinition = source.CharacterDefinitions.Single(item => item.Id == CharacterC);
        AssertInvalid(source with
        {
            CharacterDefinitions = source.CharacterDefinitions
                .Select(item => item.Id == parentDefinition.Id
                    ? item with { BirthDate = childDefinition.BirthDate }
                    : item)
                .ToArray(),
        });
        AssertInvalid(source with
        {
            CharacterDefinitions = source.CharacterDefinitions
                .Select(item => item.Id == childDefinition.Id
                    ? item with { BirthDate = CurrentDate.AddDays(1) }
                    : item)
                .ToArray(),
        });
    }

    [Fact]
    public void CharacterConstructionRejectsNonCanonicalNestedIds()
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterDefinition character = source.CharacterDefinitions.Single(item => item.Id == CharacterA);
        EntityId ability = character.AbilityIds[0];

        AssertInvalid(source with
        {
            CharacterDefinitions = source.CharacterDefinitions
                .Select(item => item.Id == CharacterA
                    ? item with { AbilityIds = [ability, ability] }
                    : item)
                .ToArray(),
        });
        AssertInvalid(WithCharacterState(
            source,
            source.CharacterStates.Single(item => item.CharacterId == CharacterC) with
            {
                ParentIds = [CharacterB, CharacterA],
            }));
        AssertInvalid(source with
        {
            FamilyStates = [source.FamilyStates[0] with { MemberIds = [CharacterC, CharacterA] }],
        });
        AssertInvalid(source with
        {
            HouseholdStates = [source.HouseholdStates[0] with { MemberIds = [CharacterB, CharacterA] }],
        });
    }

    [Fact]
    public void CharacterConstructionRejectsInvalidFamilyMembership()
    {
        CharacterWorldSnapshot source = Fixture();

        AssertInvalid(source with
        {
            FamilyStates = [source.FamilyStates[0] with { MemberIds = [CharacterA, CharacterA] }],
        });
        AssertInvalid(source with
        {
            FamilyStates = [source.FamilyStates[0] with
            {
                MemberIds = [CharacterA, new EntityId("character:test/missing")],
            }],
        });
        AssertInvalid(source with
        {
            FamilyDefinitions = [.. source.FamilyDefinitions, FamilyDefinition(FamilyB)],
            FamilyStates = [
                .. source.FamilyStates,
                new FamilyState(CharacterContractVersions.State, FamilyB, [CharacterA]),
            ],
        });
    }

    [Fact]
    public void CharacterConstructionRejectsInvalidHouseholdMembership()
    {
        CharacterWorldSnapshot source = Fixture();

        AssertInvalid(source with
        {
            HouseholdStates = [source.HouseholdStates[0] with { MemberIds = [CharacterA, CharacterA] }],
        });
        AssertInvalid(source with
        {
            HouseholdStates = [source.HouseholdStates[0] with
            {
                MemberIds = [CharacterA, new EntityId("character:test/missing")],
            }],
        });
        AssertInvalid(source with
        {
            HouseholdStates = [source.HouseholdStates[0] with { HeadCharacterId = CharacterC }],
        });
        AssertInvalid(source with
        {
            HouseholdDefinitions = [.. source.HouseholdDefinitions, HouseholdDefinition(HouseholdB)],
            HouseholdStates = [
                .. source.HouseholdStates,
                new HouseholdState(
                    CharacterContractVersions.State,
                    HouseholdB,
                    CharacterA,
                    [CharacterA]),
            ],
        });
    }

    [Fact]
    public void AuthoritativeCharacterProfilesKeepFamilyAndHouseholdMembershipIndependent()
    {
        CharacterWorldState world = new(Fixture(), CurrentDate);

        IAuthoritativeCharacterWorldQuery query = world;
        Assert.True(query.TryGetCharacterProfile(CharacterA, out AuthoritativeCharacterProfile? both));
        Assert.Equal(FamilyA, both.FamilyId);
        Assert.Equal(HouseholdA, both.HouseholdId);

        Assert.True(query.TryGetCharacterProfile(CharacterB, out AuthoritativeCharacterProfile? householdOnly));
        Assert.Null(householdOnly.FamilyId);
        Assert.Equal(HouseholdA, householdOnly.HouseholdId);

        Assert.True(query.TryGetCharacterProfile(CharacterC, out AuthoritativeCharacterProfile? familyOnly));
        Assert.Equal(FamilyA, familyOnly.FamilyId);
        Assert.Null(familyOnly.HouseholdId);
        Assert.Equal([CharacterA], familyOnly.ParentIds);
        Assert.Equal([CharacterC], both.ChildIds);
    }

    [Fact]
    public void CharacterAgeChangesThroughResolvedTurnOnBirthday()
    {
        CharacterDefinition birthdayCharacter = CharacterDefinition(
            CharacterA,
            new CampaignDate(190, 5, 10));
        CharacterWorldSnapshot snapshot = CharacterOnlySnapshot(birthdayCharacter);
        CampaignSimulation simulation = new(WorldState.Create(
            new CampaignDate(200, 5, 7),
            42,
            [],
            GeographicWorldSnapshot.Empty,
            snapshot));
        IWorldQuery world = simulation.World;

        Assert.True(world.Characters.TryGetCharacterProfile(CharacterA, out AuthoritativeCharacterProfile? before));
        Assert.Equal(9, before.Age);

        simulation.ResolveTurn();

        Assert.Equal(new CampaignDate(200, 5, 10), world.Calendar.Date);
        Assert.True(world.Characters.TryGetCharacterProfile(CharacterA, out AuthoritativeCharacterProfile? onBirthday));
        Assert.Equal(10, onBirthday.Age);
    }

    [Fact]
    public void CharacterQueriesAndSnapshotsAreDefensiveCopies()
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterDefinition character = source.CharacterDefinitions.Single(item => item.Id == CharacterA);
        EntityId[] mutableAbilityIds = character.AbilityIds.ToArray();
        CharacterWorldSnapshot mutableSource = source with
        {
            CharacterDefinitions = source.CharacterDefinitions
                .Select(item => item.Id == CharacterA ? item with { AbilityIds = mutableAbilityIds } : item)
                .ToArray(),
        };
        CharacterWorldState world = new(mutableSource, CurrentDate);

        mutableAbilityIds[0] = new EntityId("ability:test/mutated_source");
        AuthoritativeCharacterProfile profile = Assert.Single(world.Profiles, item => item.CharacterId == CharacterA);
        ((EntityId[])profile.AbilityIds)[0] = new EntityId("ability:test/mutated_query");
        AuthoritativeCharacterProfile freshProfile = Assert.Single(world.Profiles, item => item.CharacterId == CharacterA);
        Assert.Equal(new EntityId("ability:test/command"), Assert.Single(freshProfile.AbilityIds));

        AuthoritativeHouseholdView household = Assert.Single(world.Households);
        ((EntityId[])household.MemberIds)[0] = CharacterC;
        Assert.Equal([CharacterA, CharacterB], Assert.Single(world.Households).MemberIds);

        CharacterWorldSnapshot captured = world.CaptureSnapshot();
        ((CharacterDefinition[])captured.CharacterDefinitions)[0] =
            captured.CharacterDefinitions[0] with { NameKey = new EntityId("loc:test/mutated") };
        ((EntityId[])captured.CharacterStates.Single(item => item.CharacterId == CharacterC).ParentIds)[0] = CharacterB;
        CharacterWorldSnapshot freshCapture = world.CaptureSnapshot();
        Assert.NotEqual(new EntityId("loc:test/mutated"), freshCapture.CharacterDefinitions[0].NameKey);
        Assert.Equal(
            [CharacterA],
            freshCapture.CharacterStates.Single(item => item.CharacterId == CharacterC).ParentIds);

        Assert.False(world.TryGetCharacterProfile(new EntityId("character:test/missing"), out _));
        Assert.False(world.TryGetHousehold(new EntityId("household:test/missing"), out _));
    }

    [Fact]
    public void CharacterSnapshotCaptureRestoresEquivalentQueries()
    {
        CharacterWorldState source = new(Fixture(), CurrentDate);
        CharacterWorldSnapshot captured = source.CaptureSnapshot();
        CharacterWorldState restored = new(captured, CurrentDate);

        Assert.Equal(Serialize(captured), Serialize(restored.CaptureSnapshot()));
        Assert.Equal(Serialize(source.Profiles), Serialize(restored.Profiles));
        Assert.Equal(Serialize(source.Households), Serialize(restored.Households));
    }

    [Fact]
    public void SimulationChecksumCanonicalizesRawCharacterOrderAndRemainsMutationSensitive()
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterWorldSnapshot shuffled = source with
        {
            IdentityDefinitions = source.IdentityDefinitions.Reverse().ToArray(),
            CharacterDefinitions = source.CharacterDefinitions.Reverse().ToArray(),
            CharacterStates = source.CharacterStates.Reverse().ToArray(),
        };
        CharacterWorldSnapshot changed = source with
        {
            HouseholdStates = [source.HouseholdStates[0] with { HeadCharacterId = CharacterB }],
        };

        SimulationChecksum first = Checksum(source);
        SimulationChecksum second = Checksum(shuffled);
        SimulationChecksum mutated = Checksum(changed);

        Assert.Equal(first, second);
        Assert.NotEqual(first, mutated);
    }

    [Fact]
    public void CharacterWorldStateConstructsAndQueriesOneThousandCharacters()
    {
        const int characterCount = 1_000;
        CharacterDefinition[] definitions = Enumerable.Range(0, characterCount)
            .Select(index => CharacterDefinition(
                new EntityId($"character:performance/{index:D4}"),
                new CampaignDate(150 + (index % 40), 1 + (index % 12), 1 + (index % 27))))
            .ToArray();
        CharacterState[] states = definitions
            .Select(definition => new CharacterState(
                CharacterContractVersions.State,
                definition.Id,
                []))
            .ToArray();
        EntityId[] members = definitions.Select(item => item.Id).ToArray();
        CharacterWorldSnapshot snapshot = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [FamilyDefinition(FamilyA)],
            [HouseholdDefinition(HouseholdA)],
            states,
            [new FamilyState(CharacterContractVersions.State, FamilyA, members)],
            [new HouseholdState(CharacterContractVersions.State, HouseholdA, members[0], members)]);

        Stopwatch constructionTimer = Stopwatch.StartNew();
        CharacterWorldState world = new(snapshot, CurrentDate);
        constructionTimer.Stop();

        Stopwatch queryTimer = Stopwatch.StartNew();
        IReadOnlyList<AuthoritativeCharacterProfile> profiles = world.Profiles;
        IReadOnlyList<AuthoritativeHouseholdView> households = world.Households;
        int profileLookupCount = profiles.Count(profile =>
            world.TryGetCharacterProfile(profile.CharacterId, out AuthoritativeCharacterProfile? found)
            && found.CharacterId == profile.CharacterId);
        int householdLookupCount = households.Count(household =>
            world.TryGetHousehold(household.HouseholdId, out AuthoritativeHouseholdView? found)
            && found.HouseholdId == household.HouseholdId);
        queryTimer.Stop();

        output.WriteLine(
            "characters={0}; households={1}; profileLookups={2}; householdLookups={3}; constructionElapsedMs={4:F3}; queryElapsedMs={5:F3}",
            characterCount,
            households.Count,
            profileLookupCount,
            householdLookupCount,
            constructionTimer.Elapsed.TotalMilliseconds,
            queryTimer.Elapsed.TotalMilliseconds);
        Assert.Equal(characterCount, profiles.Count);
        Assert.Equal(characterCount, profileLookupCount);
        Assert.Single(households);
        Assert.Equal(characterCount, households[0].MemberIds.Count);
        Assert.Equal(1, householdLookupCount);
    }

    private static CharacterWorldSnapshot Fixture()
    {
        CharacterIdentityDefinition ability = Identity(
            "ability:test/command",
            CharacterIdentityKind.Ability);
        CharacterIdentityDefinition aptitude = Identity(
            "aptitude:test/cavalry",
            CharacterIdentityKind.Aptitude);
        CharacterIdentityDefinition trait = Identity(
            "trait:test/calm",
            CharacterIdentityKind.Trait);
        CharacterIdentityDefinition ambition = Identity(
            "ambition:test/order",
            CharacterIdentityKind.Ambition);
        CharacterIdentityDefinition reputation = Identity(
            "reputation:test/steadfast",
            CharacterIdentityKind.Reputation);
        CharacterDefinition characterA = CharacterDefinition(CharacterA, new CampaignDate(160, 1, 1)) with
        {
            AbilityIds = [ability.Id],
            AptitudeIds = [aptitude.Id],
            TraitIds = [trait.Id],
            AmbitionIds = [ambition.Id],
            ReputationIds = [reputation.Id],
        };
        CharacterDefinition characterB = CharacterDefinition(CharacterB, new CampaignDate(170, 2, 2));
        CharacterDefinition characterC = CharacterDefinition(CharacterC, new CampaignDate(190, 3, 3));

        return new CharacterWorldSnapshot(
            CharacterContractVersions.Snapshot,
            [trait, ability, reputation, aptitude, ambition],
            [characterB, characterC, characterA],
            [FamilyDefinition(FamilyA)],
            [HouseholdDefinition(HouseholdA)],
            [
                new CharacterState(CharacterContractVersions.State, CharacterB, []),
                new CharacterState(CharacterContractVersions.State, CharacterC, [CharacterA]),
                new CharacterState(CharacterContractVersions.State, CharacterA, []),
            ],
            [new FamilyState(CharacterContractVersions.State, FamilyA, [CharacterA, CharacterC])],
            [new HouseholdState(
                CharacterContractVersions.State,
                HouseholdA,
                CharacterA,
                [CharacterA, CharacterB])]);
    }

    private static CharacterWorldSnapshot CharacterOnlySnapshot(CharacterDefinition character) => new(
        CharacterContractVersions.Snapshot,
        [],
        [character],
        [],
        [],
        [new CharacterState(CharacterContractVersions.State, character.Id, [])],
        [],
        []);

    private static CharacterIdentityDefinition Identity(string id, CharacterIdentityKind kind) => new(
        CharacterContractVersions.Definition,
        new EntityId(id),
        kind,
        new EntityId($"loc:test/{id.Replace(':', '_').Replace('/', '_')}"));

    private static CharacterDefinition CharacterDefinition(EntityId id, CampaignDate birthDate) => new(
        CharacterContractVersions.Definition,
        id,
        new EntityId($"loc:test/{id.Value.Replace(':', '_').Replace('/', '_')}"),
        birthDate,
        [],
        [],
        [],
        [],
        []);

    private static FamilyDefinition FamilyDefinition(EntityId id) => new(
        CharacterContractVersions.Definition,
        id,
        new EntityId($"loc:test/{id.Value.Replace(':', '_').Replace('/', '_')}"));

    private static HouseholdDefinition HouseholdDefinition(EntityId id) => new(
        CharacterContractVersions.Definition,
        id,
        new EntityId($"loc:test/{id.Value.Replace(':', '_').Replace('/', '_')}"));

    private static CharacterDefinition SetIdentityReferences(
        CharacterDefinition character,
        CharacterIdentityKind kind,
        IReadOnlyList<EntityId> ids) => kind switch
        {
            CharacterIdentityKind.Ability => character with { AbilityIds = ids },
            CharacterIdentityKind.Aptitude => character with { AptitudeIds = ids },
            CharacterIdentityKind.Trait => character with { TraitIds = ids },
            CharacterIdentityKind.Ambition => character with { AmbitionIds = ids },
            CharacterIdentityKind.Reputation => character with { ReputationIds = ids },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static CharacterWorldSnapshot WithCharacterState(
        CharacterWorldSnapshot snapshot,
        CharacterState replacement) => snapshot with
        {
            CharacterStates = snapshot.CharacterStates
                .Select(item => item.CharacterId == replacement.CharacterId ? replacement : item)
                .ToArray(),
        };

    private static IReadOnlyList<T> ReplaceFirst<T>(IReadOnlyList<T> items, T replacement) =>
        [replacement, .. items.Skip(1)];

    private static void AssertInvalid(CharacterWorldSnapshot snapshot) =>
        Assert.Throws<SimulationValidationException>(() => new CharacterWorldState(snapshot, CurrentDate));

    private static SimulationChecksum Checksum(CharacterWorldSnapshot characters) =>
        SimulationChecksum.Compute(WorldState.Create(CurrentDate, 42, []).CaptureSnapshot() with
        {
            Characters = characters,
        });

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());
}
