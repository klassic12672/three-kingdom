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
        Assert.Equal(Serialize(first.Profiles), Serialize(second.Profiles));
        Assert.Equal([CharacterA, CharacterB, CharacterC], second.Profiles.Select(item => item.CharacterId));
        Assert.Equal([CharacterA, CharacterB], Assert.Single(second.Households).MemberIds);
    }

    [Fact]
    public void CharacterConstructionRejectsUnsupportedVersions()
    {
        CharacterWorldSnapshot source = Fixture();

        AssertInvalid(source with { ContractVersion = CharacterContractVersions.LegacySnapshot });
        AssertInvalid(source with { ContractVersion = CharacterContractVersions.Snapshot + 1 });
        AssertInvalid(source with
        {
            IdentityDefinitions = ReplaceFirst(
                source.IdentityDefinitions,
                source.IdentityDefinitions[0] with
                {
                    ContractVersion = CharacterContractVersions.LegacyDefinition,
                }),
        });
        AssertInvalid(source with
        {
            CharacterDefinitions = ReplaceFirst(
                source.CharacterDefinitions,
                source.CharacterDefinitions[0] with
                {
                    ContractVersion = CharacterContractVersions.LegacyDefinition,
                }),
        });
        AssertInvalid(source with
        {
            FamilyDefinitions = [source.FamilyDefinitions[0] with
            {
                ContractVersion = CharacterContractVersions.LegacyDefinition,
            }],
        });
        AssertInvalid(source with
        {
            HouseholdDefinitions = [source.HouseholdDefinitions[0] with
            {
                ContractVersion = CharacterContractVersions.LegacyDefinition,
            }],
        });
        AssertInvalid(source with
        {
            CharacterStates = ReplaceFirst(
                source.CharacterStates,
                source.CharacterStates[0] with
                {
                    ContractVersion = CharacterContractVersions.LegacyState,
                }),
        });
        AssertInvalid(source with
        {
            FamilyStates = [source.FamilyStates[0] with
            {
                ContractVersion = CharacterContractVersions.LegacyState,
            }],
        });
        AssertInvalid(source with
        {
            HouseholdStates = [source.HouseholdStates[0] with
            {
                ContractVersion = CharacterContractVersions.LegacyState,
            }],
        });
    }

    [Fact]
    public void CharacterConstructionRequiresCompleteVersionTwoDescriptorAndState()
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterDefinition definition = source.CharacterDefinitions[0];
        CharacterState state = source.CharacterStates[0];

        AssertInvalid(WithCharacterDefinition(source, definition with { StructuredName = null }));
        AssertInvalid(WithCharacterDefinition(source, definition with { ContentOrigin = null }));
        AssertInvalid(WithCharacterDefinition(source, definition with { FlawIds = null }));
        AssertInvalid(WithCharacterState(source, state with { ParentLinks = null }));
        AssertInvalid(WithCharacterState(source, state with { Condition = null }));
    }

    [Fact]
    public void CharacterConstructionValidatesStructuredNamesAndDescriptorIds()
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterDefinition definition = source.CharacterDefinitions.Single(item => item.Id == CharacterA);

        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            StructuredName = definition.StructuredName! with { PrimaryNameKey = default },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            StructuredName = definition.StructuredName! with
            {
                PrimaryNameKey = new EntityId("loc:test/not_the_primary_name"),
            },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            StructuredName = definition.StructuredName! with { CourtesyNameKey = new EntityId() },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with { CultureId = new EntityId() }));
        AssertInvalid(WithCharacterDefinition(source, definition with { OriginLocationId = new EntityId() }));
    }

    [Theory]
    [InlineData(CharacterOriginKind.LegacyUnknown)]
    [InlineData(CharacterOriginKind.Authored)]
    [InlineData(CharacterOriginKind.Custom)]
    [InlineData(CharacterOriginKind.Generated)]
    public void CharacterConstructionAcceptsEveryValidContentOriginKind(CharacterOriginKind kind)
    {
        CharacterDefinition definition = CharacterDefinition(CharacterA, new CampaignDate(160, 1, 1)) with
        {
            ContentOrigin = ValidOrigin(kind, CharacterA),
        };

        CharacterWorldState world = new(CharacterOnlySnapshot(definition), CurrentDate);

        Assert.Equal(kind, Assert.Single(world.Profiles).ContentOrigin.OriginKind);
    }

    [Fact]
    public void CharacterConstructionAcceptsSourceFreeFictionalAuthoredCompatibilityOrigin()
    {
        CharacterDefinition definition = CharacterDefinition(CharacterA, new CampaignDate(160, 1, 1)) with
        {
            ContentOrigin = new CharacterContentOrigin(
                CharacterOriginKind.Authored,
                CharacterHistoricalClassification.Fictional,
                CharacterA,
                new EntityId("content-pack:test/legacy-v1"),
                [],
                []),
        };

        CharacterWorldState world = new(CharacterOnlySnapshot(definition), CurrentDate);

        Assert.Empty(Assert.Single(world.Profiles).ContentOrigin.SourceIds);
    }

    [Fact]
    public void CharacterConstructionRejectsInvalidContentOriginMetadata()
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterDefinition definition = source.CharacterDefinitions.Single(item => item.Id == CharacterA);
        CharacterContentOrigin authored = definition.ContentOrigin!;

        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = authored with { OriginKind = (CharacterOriginKind)999 },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = authored with
            {
                HistoricalClassification = (CharacterHistoricalClassification)999,
            },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = authored with { RecordId = default },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = authored with { OwningPackId = new EntityId() },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = authored with { AppliedOverridePackIds = null! },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = authored with { SourceIds = null! },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = authored with
            {
                AppliedOverridePackIds = [
                    new EntityId("content-pack:test/z"),
                    new EntityId("content-pack:test/a"),
                ],
            },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = authored with
            {
                SourceIds = [new EntityId("source:test/a"), new EntityId("source:test/a")],
            },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = authored with
            {
                AppliedOverridePackIds = [authored.OwningPackId!.Value],
            },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = authored with { SourceIds = [] },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = CharacterContentOrigin.LegacyUnknown(definition.Id) with
            {
                HistoricalClassification = CharacterHistoricalClassification.Fictional,
            },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = ValidOrigin(CharacterOriginKind.Custom, definition.Id) with
            {
                HistoricalClassification = CharacterHistoricalClassification.Historical,
            },
        }));
        AssertInvalid(WithCharacterDefinition(source, definition with
        {
            ContentOrigin = ValidOrigin(CharacterOriginKind.Generated, definition.Id) with
            {
                OwningPackId = new EntityId("content-pack:test/generated"),
            },
        }));
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
                CharacterState(new EntityId("character:test/d")),
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
    [InlineData(CharacterIdentityKind.Flaw)]
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

        AssertInvalid(WithCharacterState(source, child with { ParentLinks = [null!] }));
        AssertInvalid(WithCharacterState(source, child with
        {
            ParentIds = [new EntityId("character:test/missing")],
            ParentLinks = [new CharacterParentLink(
                new EntityId("character:test/missing"),
                ParentChildLinkKind.Biological)],
        }));
        AssertInvalid(WithCharacterState(source, CharacterState(
            CharacterC,
            [(CharacterC, ParentChildLinkKind.Biological)])));

        CharacterState parent = source.CharacterStates.Single(item => item.CharacterId == CharacterA);
        CharacterWorldSnapshot cycle = WithCharacterState(
            source,
            CharacterState(CharacterA, [(CharacterC, ParentChildLinkKind.Biological)]));
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

    [Theory]
    [InlineData(ParentChildLinkKind.UnspecifiedLegacy)]
    [InlineData(ParentChildLinkKind.Biological)]
    [InlineData(ParentChildLinkKind.LegalAdoptive)]
    public void AuthoritativeQueriesIndexTypedParentAndChildLinks(ParentChildLinkKind kind)
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterState child = CharacterState(CharacterC, [(CharacterA, kind)]);
        CharacterWorldState world = new(WithCharacterState(source, child), CurrentDate);

        AuthoritativeCharacterProfile childProfile = Assert.Single(
            world.Profiles,
            item => item.CharacterId == CharacterC);
        CharacterParentLink parentLink = Assert.Single(childProfile.ParentLinks);
        Assert.Equal(CharacterA, parentLink.ParentCharacterId);
        Assert.Equal(kind, parentLink.Kind);
        Assert.Equal([CharacterA], childProfile.ParentIds);

        AuthoritativeCharacterProfile parentProfile = Assert.Single(
            world.Profiles,
            item => item.CharacterId == CharacterA);
        CharacterChildLink childLink = Assert.Single(parentProfile.ChildLinks);
        Assert.Equal(CharacterC, childLink.ChildCharacterId);
        Assert.Equal(kind, childLink.Kind);
        Assert.Equal([CharacterC], parentProfile.ChildIds);
    }

    [Fact]
    public void CharacterConstructionRequiresCanonicalTypedParentLinksMatchingRetainedIds()
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterState child = source.CharacterStates.Single(item => item.CharacterId == CharacterC);

        AssertInvalid(WithCharacterState(source, child with
        {
            ParentLinks = [new CharacterParentLink(CharacterB, ParentChildLinkKind.Biological)],
        }));
        AssertInvalid(WithCharacterState(source, child with
        {
            ParentIds = [CharacterA, CharacterB],
            ParentLinks = [
                new CharacterParentLink(CharacterB, ParentChildLinkKind.LegalAdoptive),
                new CharacterParentLink(CharacterA, ParentChildLinkKind.Biological),
            ],
        }));
        AssertInvalid(WithCharacterState(source, child with
        {
            ParentIds = [CharacterA],
            ParentLinks = [
                new CharacterParentLink(CharacterA, ParentChildLinkKind.Biological),
                new CharacterParentLink(CharacterA, ParentChildLinkKind.LegalAdoptive),
            ],
        }));
        AssertInvalid(WithCharacterState(source, child with
        {
            ParentLinks = [new CharacterParentLink(CharacterA, (ParentChildLinkKind)999)],
        }));
    }

    [Fact]
    public void CharacterConstructionValidatesConditionEnumsAndCustodyInvariants()
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterState state = source.CharacterStates.Single(item => item.CharacterId == CharacterB);

        AssertInvalid(WithCharacterState(source, state with
        {
            Condition = state.Condition! with { VitalStatus = (CharacterVitalStatus)999 },
        }));
        AssertInvalid(WithCharacterState(source, state with
        {
            Condition = state.Condition! with { HealthStatus = (CharacterHealthStatus)999 },
        }));
        AssertInvalid(WithCharacterState(source, state with
        {
            Condition = state.Condition! with { CustodyStatus = (CharacterCustodyStatus)999 },
        }));
        AssertInvalid(WithCharacterState(source, state with
        {
            Condition = CharacterConditionState.Default with
            {
                CustodianId = new EntityId("character:test/custodian"),
            },
        }));
        AssertInvalid(WithCharacterState(source, state with
        {
            Condition = CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
            },
        }));
        AssertInvalid(WithCharacterState(source, state with
        {
            Condition = CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = new EntityId(),
            },
        }));
        AssertInvalid(WithCharacterState(source, state with
        {
            Condition = CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = CharacterB,
            },
        }));
        AssertInvalid(WithCharacterState(source, state with
        {
            Condition = CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = new EntityId("character:test/missing"),
            },
        }));
        AssertInvalid(WithCharacterState(source, state with
        {
            Condition = CharacterConditionState.Default with
            {
                HealthStatus = CharacterHealthStatus.Critical,
            },
        }));
        AssertInvalid(WithCharacterState(source, state with
        {
            Condition = CharacterConditionState.Default with
            {
                VitalStatus = CharacterVitalStatus.Dead,
            },
        }));
        AssertInvalid(WithCharacterState(source, state with
        {
            Condition = new CharacterConditionState(
                CharacterVitalStatus.Dead,
                CharacterHealthStatus.Critical,
                IsIncapacitated: true,
                CharacterCustodyStatus.Hostage,
                CharacterA),
        }));

        CharacterConditionState valid = new(
            CharacterVitalStatus.Alive,
            CharacterHealthStatus.Critical,
            IsIncapacitated: true,
            CharacterCustodyStatus.Hostage,
            CharacterA);
        CharacterWorldState world = new(
            WithCharacterState(source, state with { Condition = valid }),
            CurrentDate);
        Assert.Equal(valid, Assert.Single(world.Profiles, item => item.CharacterId == CharacterB).Condition);

        CharacterConditionState deceased = new(
            CharacterVitalStatus.Dead,
            CharacterHealthStatus.Critical,
            IsIncapacitated: true,
            CharacterCustodyStatus.Free,
            null);
        world = new(WithCharacterState(source, state with { Condition = deceased }), CurrentDate);
        Assert.Equal(deceased, Assert.Single(world.Profiles, item => item.CharacterId == CharacterB).Condition);
    }

    [Theory]
    [InlineData(CharacterHealthStatus.Injured, false)]
    [InlineData(CharacterHealthStatus.Injured, true)]
    [InlineData(CharacterHealthStatus.Ill, false)]
    [InlineData(CharacterHealthStatus.Ill, true)]
    public void InjuredAndIllConditionsRemainIndependentOfIncapacity(
        CharacterHealthStatus health,
        bool isIncapacitated)
    {
        CharacterWorldSnapshot source = Fixture();
        CharacterState state = source.CharacterStates.Single(item => item.CharacterId == CharacterB);
        CharacterConditionState condition = new(
            CharacterVitalStatus.Alive,
            health,
            isIncapacitated,
            CharacterCustodyStatus.Free,
            null);

        CharacterWorldState world = new(
            WithCharacterState(source, state with { Condition = condition }),
            CurrentDate);

        Assert.Equal(
            condition,
            Assert.Single(world.Profiles, item => item.CharacterId == CharacterB).Condition);
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
                ParentLinks = [
                    new CharacterParentLink(CharacterB, ParentChildLinkKind.Biological),
                    new CharacterParentLink(CharacterA, ParentChildLinkKind.Biological),
                ],
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
    public void AuthoritativeVersionTwoProfileReturnsCompleteDescriptorAndAgencyState()
    {
        CharacterWorldState world = new(Fixture(), CurrentDate);

        Assert.True(world.TryGetCharacterProfile(CharacterA, out AuthoritativeCharacterProfile? profile));
        Assert.Equal(CharacterContractVersions.AuthoritativeQuery, profile.ContractVersion);
        Assert.Equal(profile.NameKey, profile.StructuredName.PrimaryNameKey);
        Assert.Equal(new EntityId("loc:test/character_test_a_courtesy"), profile.StructuredName.CourtesyNameKey);
        Assert.Equal(CharacterOriginKind.Authored, profile.ContentOrigin.OriginKind);
        Assert.Equal(CharacterHistoricalClassification.Inferred, profile.ContentOrigin.HistoricalClassification);
        Assert.Equal(new EntityId("character-record:test/a"), profile.ContentOrigin.RecordId);
        Assert.Equal(new EntityId("content-pack:test/base"), profile.ContentOrigin.OwningPackId);
        Assert.Equal(
            [new EntityId("content-pack:test/override")],
            profile.ContentOrigin.AppliedOverridePackIds);
        Assert.Equal([new EntityId("source:test/a")], profile.ContentOrigin.SourceIds);
        Assert.Equal(new EntityId("culture:test/han"), profile.CultureId);
        Assert.Equal(new EntityId("locality:test/chenliu"), profile.OriginLocationId);
        Assert.Equal([new EntityId("flaw:test/stubborn")], profile.FlawIds);
        Assert.Equal(CharacterConditionState.Default, profile.Condition);
        Assert.Empty(profile.ParentLinks);
        Assert.Equal(
            [new CharacterChildLink(CharacterC, ParentChildLinkKind.Biological)],
            profile.ChildLinks);
    }

    [Fact]
    public void CharacterAgencyContractsDoNotPersistConsentBoolean()
    {
        Type[] authoritativeTypes = [
            typeof(CharacterConditionState),
            typeof(CharacterDefinition),
            typeof(CharacterState),
            typeof(CharacterWorldSnapshot),
            typeof(AuthoritativeCharacterProfile),
        ];

        Assert.All(authoritativeTypes, type => Assert.DoesNotContain(
            type.GetProperties(),
            property => property.Name.Contains("Consent", StringComparison.OrdinalIgnoreCase)));
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
        EntityId[] mutableFlawIds = character.FlawIds!.ToArray();
        EntityId[] mutableOverridePackIds = character.ContentOrigin!.AppliedOverridePackIds.ToArray();
        EntityId[] mutableSourceIds = character.ContentOrigin.SourceIds.ToArray();
        CharacterState child = source.CharacterStates.Single(item => item.CharacterId == CharacterC);
        CharacterParentLink[] mutableParentLinks = child.ParentLinks!.ToArray();
        CharacterWorldSnapshot mutableSource = source with
        {
            CharacterDefinitions = source.CharacterDefinitions
                .Select(item => item.Id == CharacterA ? item with
                {
                    AbilityIds = mutableAbilityIds,
                    FlawIds = mutableFlawIds,
                    ContentOrigin = item.ContentOrigin! with
                    {
                        AppliedOverridePackIds = mutableOverridePackIds,
                        SourceIds = mutableSourceIds,
                    },
                } : item)
                .ToArray(),
            CharacterStates = source.CharacterStates
                .Select(item => item.CharacterId == CharacterC ? item with
                {
                    ParentLinks = mutableParentLinks,
                } : item)
                .ToArray(),
        };
        CharacterWorldState world = new(mutableSource, CurrentDate);

        mutableAbilityIds[0] = new EntityId("ability:test/mutated_source");
        mutableFlawIds[0] = new EntityId("flaw:test/mutated_source");
        mutableOverridePackIds[0] = new EntityId("content-pack:test/mutated_source");
        mutableSourceIds[0] = new EntityId("source:test/mutated_source");
        mutableParentLinks[0] = new CharacterParentLink(CharacterB, ParentChildLinkKind.LegalAdoptive);
        AuthoritativeCharacterProfile profile = Assert.Single(world.Profiles, item => item.CharacterId == CharacterA);
        ((EntityId[])profile.AbilityIds)[0] = new EntityId("ability:test/mutated_query");
        ((EntityId[])profile.FlawIds)[0] = new EntityId("flaw:test/mutated_query");
        ((EntityId[])profile.ContentOrigin.AppliedOverridePackIds)[0] =
            new EntityId("content-pack:test/mutated_query");
        ((EntityId[])profile.ContentOrigin.SourceIds)[0] = new EntityId("source:test/mutated_query");
        ((CharacterChildLink[])profile.ChildLinks)[0] =
            new CharacterChildLink(CharacterB, ParentChildLinkKind.LegalAdoptive);
        AuthoritativeCharacterProfile freshProfile = Assert.Single(world.Profiles, item => item.CharacterId == CharacterA);
        Assert.Equal(new EntityId("ability:test/command"), Assert.Single(freshProfile.AbilityIds));
        Assert.Equal(new EntityId("flaw:test/stubborn"), Assert.Single(freshProfile.FlawIds));
        Assert.Equal(
            new EntityId("content-pack:test/override"),
            Assert.Single(freshProfile.ContentOrigin.AppliedOverridePackIds));
        Assert.Equal(new EntityId("source:test/a"), Assert.Single(freshProfile.ContentOrigin.SourceIds));
        Assert.Equal(
            new CharacterChildLink(CharacterC, ParentChildLinkKind.Biological),
            Assert.Single(freshProfile.ChildLinks));

        AuthoritativeCharacterProfile childProfile = Assert.Single(
            world.Profiles,
            item => item.CharacterId == CharacterC);
        ((CharacterParentLink[])childProfile.ParentLinks)[0] =
            new CharacterParentLink(CharacterB, ParentChildLinkKind.LegalAdoptive);
        Assert.Equal(
            new CharacterParentLink(CharacterA, ParentChildLinkKind.Biological),
            Assert.Single(world.Profiles, item => item.CharacterId == CharacterC).ParentLinks.Single());

        AuthoritativeHouseholdView household = Assert.Single(world.Households);
        ((EntityId[])household.MemberIds)[0] = CharacterC;
        Assert.Equal([CharacterA, CharacterB], Assert.Single(world.Households).MemberIds);

        CharacterWorldSnapshot captured = world.CaptureSnapshot();
        ((CharacterDefinition[])captured.CharacterDefinitions)[0] =
            captured.CharacterDefinitions[0] with { NameKey = new EntityId("loc:test/mutated") };
        ((EntityId[])captured.CharacterStates.Single(item => item.CharacterId == CharacterC).ParentIds)[0] = CharacterB;
        ((CharacterParentLink[])captured.CharacterStates
            .Single(item => item.CharacterId == CharacterC).ParentLinks!)[0] =
            new CharacterParentLink(CharacterB, ParentChildLinkKind.LegalAdoptive);
        ((EntityId[])captured.CharacterDefinitions
            .Single(item => item.Id == CharacterA).ContentOrigin!.SourceIds)[0] =
            new EntityId("source:test/mutated_capture");
        CharacterWorldSnapshot freshCapture = world.CaptureSnapshot();
        Assert.NotEqual(new EntityId("loc:test/mutated"), freshCapture.CharacterDefinitions[0].NameKey);
        Assert.Equal(
            [CharacterA],
            freshCapture.CharacterStates.Single(item => item.CharacterId == CharacterC).ParentIds);
        Assert.Equal(
            new CharacterParentLink(CharacterA, ParentChildLinkKind.Biological),
            Assert.Single(freshCapture.CharacterStates
                .Single(item => item.CharacterId == CharacterC).ParentLinks!));
        Assert.Equal(
            new EntityId("source:test/a"),
            Assert.Single(freshCapture.CharacterDefinitions
                .Single(item => item.Id == CharacterA).ContentOrigin!.SourceIds));

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
            .Select(definition => CharacterState(definition.Id))
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
        Assert.All(profiles, profile =>
        {
            Assert.Equal(CharacterContractVersions.AuthoritativeQuery, profile.ContractVersion);
            Assert.Equal(profile.NameKey, profile.StructuredName.PrimaryNameKey);
            Assert.Equal(CharacterOriginKind.LegacyUnknown, profile.ContentOrigin.OriginKind);
            Assert.Equal(profile.CharacterId, profile.ContentOrigin.RecordId);
            Assert.Empty(profile.FlawIds);
            Assert.Equal(CharacterConditionState.Default, profile.Condition);
            Assert.Empty(profile.ParentLinks);
            Assert.Empty(profile.ChildLinks);
        });
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
        CharacterIdentityDefinition flaw = Identity(
            "flaw:test/stubborn",
            CharacterIdentityKind.Flaw);
        CharacterDefinition characterA = CharacterDefinition(CharacterA, new CampaignDate(160, 1, 1)) with
        {
            AbilityIds = [ability.Id],
            AptitudeIds = [aptitude.Id],
            TraitIds = [trait.Id],
            AmbitionIds = [ambition.Id],
            ReputationIds = [reputation.Id],
            FlawIds = [flaw.Id],
            StructuredName = new StructuredCharacterName(
                new EntityId("loc:test/character_test_a"),
                new EntityId("loc:test/character_test_a_courtesy")),
            ContentOrigin = new CharacterContentOrigin(
                CharacterOriginKind.Authored,
                CharacterHistoricalClassification.Inferred,
                new EntityId("character-record:test/a"),
                new EntityId("content-pack:test/base"),
                [new EntityId("content-pack:test/override")],
                [new EntityId("source:test/a")]),
            CultureId = new EntityId("culture:test/han"),
            OriginLocationId = new EntityId("locality:test/chenliu"),
        };
        CharacterDefinition characterB = CharacterDefinition(CharacterB, new CampaignDate(170, 2, 2));
        CharacterDefinition characterC = CharacterDefinition(CharacterC, new CampaignDate(190, 3, 3));

        return new CharacterWorldSnapshot(
            CharacterContractVersions.Snapshot,
            [trait, ability, flaw, reputation, aptitude, ambition],
            [characterB, characterC, characterA],
            [FamilyDefinition(FamilyA)],
            [HouseholdDefinition(HouseholdA)],
            [
                CharacterState(CharacterB),
                CharacterState(CharacterC, [(CharacterA, ParentChildLinkKind.Biological)]),
                CharacterState(CharacterA),
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
        [CharacterState(character.Id)],
        [],
        []);

    private static CharacterIdentityDefinition Identity(string id, CharacterIdentityKind kind) => new(
        CharacterContractVersions.Definition,
        new EntityId(id),
        kind,
        new EntityId($"loc:test/{id.Replace(':', '_').Replace('/', '_')}"));

    private static CharacterContentOrigin ValidOrigin(CharacterOriginKind kind, EntityId recordId) => kind switch
    {
        CharacterOriginKind.LegacyUnknown => CharacterContentOrigin.LegacyUnknown(recordId),
        CharacterOriginKind.Authored => new CharacterContentOrigin(
            kind,
            CharacterHistoricalClassification.Historical,
            recordId,
            new EntityId("content-pack:test/authored"),
            [],
            [new EntityId("source:test/authored")]),
        CharacterOriginKind.Custom => new CharacterContentOrigin(
            kind,
            CharacterHistoricalClassification.Fictional,
            recordId,
            new EntityId("content-pack:test/custom"),
            [],
            []),
        CharacterOriginKind.Generated => new CharacterContentOrigin(
            kind,
            CharacterHistoricalClassification.Fictional,
            recordId,
            null,
            [],
            []),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static CharacterDefinition CharacterDefinition(EntityId id, CampaignDate birthDate)
    {
        EntityId nameKey = new($"loc:test/{id.Value.Replace(':', '_').Replace('/', '_')}");
        return new CharacterDefinition(
            CharacterContractVersions.Definition,
            id,
            nameKey,
            birthDate,
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
    }

    private static CharacterState CharacterState(
        EntityId id,
        IReadOnlyList<(EntityId ParentId, ParentChildLinkKind Kind)>? parents = null,
        CharacterConditionState? condition = null)
    {
        (EntityId ParentId, ParentChildLinkKind Kind)[] canonicalParents = (parents ?? [])
            .OrderBy(item => item.ParentId)
            .ThenBy(item => item.Kind)
            .ToArray();
        return new CharacterState(
            CharacterContractVersions.State,
            id,
            canonicalParents.Select(item => item.ParentId).ToArray(),
            canonicalParents.Select(item => new CharacterParentLink(item.ParentId, item.Kind)).ToArray(),
            condition ?? CharacterConditionState.Default);
    }

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
            CharacterIdentityKind.Flaw => character with { FlawIds = ids },
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

    private static CharacterWorldSnapshot WithCharacterDefinition(
        CharacterWorldSnapshot snapshot,
        CharacterDefinition replacement) => snapshot with
        {
            CharacterDefinitions = snapshot.CharacterDefinitions
                .Select(item => item.Id == replacement.Id ? replacement : item)
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
