using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterMarriageTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly CampaignCalendar Calendar = new(Date, 10);
    private static readonly EntityId StandardPracticeId =
        new("marriage_practice:test/standard");
    private readonly ITestOutputHelper output;

    public CharacterMarriageTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void ContractVersionsAreExplicitDefaultEmptyAndPlatformNeutral()
    {
        CharacterMarriageWorldState state = NewState(CreateCharacters(2));

        Assert.Equal(2, CharacterMarriageContractVersions.Snapshot);
        Assert.Equal(1, CharacterMarriageContractVersions.State);
        Assert.Equal(1, CharacterMarriageContractVersions.Practice);
        Assert.Equal(1, CharacterMarriageContractVersions.Eligibility);
        Assert.Equal(1, CharacterMarriageContractVersions.Action);
        Assert.Equal(1, CharacterMarriageContractVersions.Outcome);
        Assert.Equal(2, CharacterMarriageContractVersions.AuthoritativeQuery);
        Assert.Equal(18, CharacterMarriageLimits.MinimumAdultAge);
        Assert.Equal(64, CharacterMarriageLimits.RetainedRecordsPerCategoryPerCharacter);
        Assert.Equal(64, CharacterMarriageLimits.ActiveLegalRelationshipsPerCharacter);
        Assert.Equal("simulation.character_marriages", CharacterMarriageSystem.SystemId);
        Assert.Equal(2, CharacterMarriageSystem.Version);
        Assert.Equal(Serialize(CharacterMarriageWorldSnapshot.Empty), Serialize(state.CaptureSnapshot()));

        Assert.Contains(
            typeof(CharacterMarriageWorldState).GetMethods(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly),
            method => method.Name == nameof(CharacterMarriageWorldState.ValidateAction));

        string serialized = Serialize(state.CaptureSnapshot());
        Assert.DoesNotContain("Godot", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Steam", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            typeof(CharacterMarriageWorldState).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name?.Contains("Godot", StringComparison.OrdinalIgnoreCase) == true
                || assembly.Name?.Contains("Steam", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void ConstructionRejectsNullUnsupportedAndMalformedSnapshotShape()
    {
        CharacterWorldState characters = CreateCharacters(2);

        Assert.Throws<SimulationValidationException>(() =>
            new CharacterMarriageWorldState(null!, characters, Calendar));
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterMarriageWorldState(CharacterMarriageWorldSnapshot.Empty, null!, Calendar));
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterMarriageWorldState(CharacterMarriageWorldSnapshot.Empty, characters, default));
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { ContractVersion = 3 }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { Practices = null! }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { Proposals = null! }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { Betrothals = null! }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { Unions = null! }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { Invitations = null! }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { RomanceRoutes = null! }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { History = null! }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { Practices = [null!] }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { Proposals = [null!] }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { Betrothals = [null!] }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { Unions = [null!] }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { Invitations = [null!] }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { RomanceRoutes = [null!] }, characters);
        AssertInvalid(CharacterMarriageWorldSnapshot.Empty with { History = [null!] }, characters);
    }

    [Fact]
    public void PracticeValidationRejectsVersionNamespaceLimitsUnknownFlagsAndDuplicates()
    {
        CharacterWorldState characters = CreateCharacters(2);
        MarriagePracticeState valid = Practice();

        AssertInvalid(Snapshot(valid with { ContractVersion = 2 }), characters);
        AssertInvalid(Snapshot(valid with { PracticeId = default }), characters);
        AssertInvalid(
            Snapshot(valid with { PracticeId = new EntityId("culture:test/not_a_practice") }),
            characters);
        AssertInvalid(Snapshot(valid with { MinimumLegalUnionAge = 17 }), characters);
        AssertInvalid(Snapshot(valid with { MinimumRomanceAge = 101 }), characters);
        AssertInvalid(Snapshot(valid with { MaximumActivePrincipalSpousesPerCharacter = 9 }), characters);
        AssertInvalid(Snapshot(valid with { MaximumActiveConcubinageUnionsPerPrincipal = 65 }), characters);
        AssertInvalid(Snapshot(valid with { MaximumActiveConcubinageUnionsPerPartner = -1 }), characters);
        AssertInvalid(
            Snapshot(valid with { ProhibitedKinship = (MarriageProhibitedKinship)8 }),
            characters);
        AssertInvalid(Snapshot([valid, valid]), characters);
    }

    [Fact]
    public void RecordValidationRejectsBadVersionNamespaceEnumDateTerminalAndReferences()
    {
        CharacterWorldState characters = CreateCharacters(4);
        MarriagePracticeState practice = Practice();
        MarriageProposalState accepted = AcceptedProposal("valid", Character(0), Character(1));
        MarriageUnionState union = Union("valid", accepted);
        RomanceRouteState route = Route("valid", Character(2), Character(3));
        CharacterMarriageWorldSnapshot valid = Snapshot(
            [practice],
            [accepted],
            [],
            [union],
            [route],
            []);
        _ = new CharacterMarriageWorldState(valid, characters, Calendar);

        AssertInvalid(valid with
        {
            Proposals = [accepted with { ContractVersion = 2 }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [accepted with { ProposalId = new EntityId("proposal:test/wrong") }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [accepted with { SourceCommandId = new EntityId("event:test/wrong") }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [accepted with { Kind = (MarriageProposalKind)99 }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [accepted with { CreatedDate = default }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [accepted with { CreatedTurnIndex = -1 }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [accepted with { ResolutionDate = null }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [accepted with { ResolutionDate = accepted.CreatedDate.AddDays(-1) }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [accepted with { ResolutionTurnIndex = -1 }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [accepted with { ResolutionCommandId = new EntityId("event:test/wrong") }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [accepted with
            {
                Status = MarriageProposalStatus.Active,
                ResolutionDate = Date,
                ResolutionTurnIndex = 1,
                ResolutionCommandId = new EntityId("command:test/terminal"),
            }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [accepted with { PracticeId = new EntityId("marriage_practice:test/missing") }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [accepted with { RecipientCharacterId = new EntityId("character:test/missing") }],
            Betrothals = [],
            Unions = [],
        }, characters);
        MarriageProposalState political = AcceptedProposal(
            "valid_political",
            Character(0),
            Character(1),
            kind: MarriageProposalKind.PoliticalBetrothal,
            consent: MarriageConsentKind.PoliticalArrangement);
        PoliticalBetrothalState betrothal = Betrothal("valid_political", political);
        AssertInvalid(
            Snapshot(
                [practice],
                [political],
                [betrothal with { BetrothalId = new EntityId("betrothal:test/wrong") }],
                [],
                [],
                []),
            characters);
        AssertInvalid(valid with
        {
            Unions = [union with { UnionId = new EntityId("union:test/wrong") }],
            Betrothals = [],
        }, characters);
        AssertInvalid(valid with
        {
            Unions = [union with
            {
                Status = MarriageUnionStatus.Ended,
                EndDate = Date,
                EndTurnIndex = 2,
                EndCommandId = new EntityId("command:test/end"),
                EndReason = null,
            }],
            Betrothals = [],
        }, characters);
        AssertInvalid(valid with
        {
            RomanceRoutes = [route with { RouteId = new EntityId("romance:test/wrong") }],
            Betrothals = [],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            RomanceRoutes = [route with { ProgressLevel = 5 }],
            Betrothals = [],
            Unions = [],
        }, characters);
    }

    [Fact]
    public void PersistedDatesAndTurnsCannotExceedTheAuthoritativeCalendar()
    {
        CharacterWorldState characters = CreateCharacters(4);
        MarriagePracticeState practice = Practice();
        MarriageProposalState accepted = AcceptedProposal("calendar", Character(0), Character(1));
        MarriageUnionState union = Union("calendar", accepted);
        MarriageProposalState refused = TerminalProposal("calendar_refused", Character(2), Character(3));
        RomanceRouteState endedRoute = EndedRoute("calendar_ended", Character(2), Character(3));
        CharacterMarriageWorldSnapshot valid = Snapshot(
            [practice],
            [accepted, refused],
            [],
            [union],
            [endedRoute],
            []);
        _ = new CharacterMarriageWorldState(valid, characters, Calendar);

        AssertInvalid(valid with
        {
            Proposals = [accepted with { CreatedTurnIndex = Calendar.TurnIndex + 1 }, refused],
            Unions = [],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals =
            [
                accepted,
                refused with { ResolutionTurnIndex = Calendar.TurnIndex + 1 },
            ],
        }, characters);
        AssertInvalid(valid with
        {
            Unions =
            [
                union with
                {
                    Status = MarriageUnionStatus.Ended,
                    EndDate = Date,
                    EndTurnIndex = Calendar.TurnIndex + 1,
                    EndCommandId = new EntityId("command:test/calendar/end"),
                    EndReason = MarriageUnionEndReason.Annulled,
                },
            ],
        }, characters);
        AssertInvalid(valid with
        {
            RomanceRoutes =
            [
                endedRoute with { ResolutionTurnIndex = Calendar.TurnIndex + 1 },
            ],
        }, characters);
        AssertInvalid(valid with
        {
            RomanceRoutes =
            [
                Route("calendar_future", Character(2), Character(3)) with
                {
                    StartDate = Date.AddDays(1),
                },
            ],
        }, characters);
    }

    [Fact]
    public void PracticesAreExplicitAndDoNotDeriveFromCultureFamilyOrHousehold()
    {
        Dictionary<EntityId, EntityId?> cultures = new()
        {
            [Character(0)] = new EntityId("culture:test/north"),
            [Character(1)] = new EntityId("culture:test/south"),
        };
        CharacterWorldState characters = CreateCharacters(
            2,
            cultures: cultures,
            separateHouseholds: true);
        CharacterMarriageWorldState state = NewState(characters, Practice());
        MarriageEligibilityResult eligible = state.EvaluateEligibility(
            Eligibility(
                MarriageEligibilityCategory.VoluntaryLegalUnion,
                Character(0),
                Character(1),
                MarriageUnionForm.PrincipalSpouse),
            Date);

        Assert.True(eligible.IsEligible);
        Assert.True(characters.TryGetCharacterProfile(Character(0), out AuthoritativeCharacterProfile? first));
        Assert.True(characters.TryGetCharacterProfile(Character(1), out AuthoritativeCharacterProfile? second));
        Assert.NotEqual(first.CultureId, second.CultureId);
        Assert.NotEqual(first.HouseholdId, second.HouseholdId);
        Assert.Single(state.Practices);
        Assert.Equal(StandardPracticeId, state.Practices[0].PracticeId);

        CharacterMarriageWorldState noPractice = NewState(characters);
        MarriageEligibilityResult unknown = noPractice.EvaluateEligibility(
            Eligibility(
                MarriageEligibilityCategory.VoluntaryLegalUnion,
                Character(0),
                Character(1),
                MarriageUnionForm.PrincipalSpouse),
            Date);
        AssertIssue(unknown, MarriageEligibilityReason.UnknownPractice);
    }

    [Fact]
    public void AdultEligibilityUsesTheExactEighteenthBirthdayDayBoundary()
    {
        CharacterWorldState characters = CreateCharacters(
            3,
            birthDates: new Dictionary<EntityId, CampaignDate>
            {
                [Character(0)] = new CampaignDate(182, 5, 10),
                [Character(1)] = new CampaignDate(182, 5, 10),
                [Character(2)] = new CampaignDate(182, 5, 11),
            });
        CharacterMarriageWorldState state = NewState(characters, Practice());

        Assert.True(state.EvaluateEligibility(
            Eligibility(
                MarriageEligibilityCategory.VoluntaryLegalUnion,
                Character(0),
                Character(1),
                MarriageUnionForm.PrincipalSpouse),
            Date).IsEligible);
        Assert.True(state.EvaluateEligibility(
            Eligibility(
                MarriageEligibilityCategory.VoluntaryRomance,
                Character(0),
                Character(1)),
            Date).IsEligible);
        AssertIssue(
            state.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.VoluntaryLegalUnion,
                    Character(0),
                    Character(2),
                    MarriageUnionForm.PrincipalSpouse),
                Date),
            MarriageEligibilityReason.BelowMinimumAge);
        AssertIssue(
            state.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.VoluntaryRomance,
                    Character(0),
                    Character(2)),
                Date),
            MarriageEligibilityReason.BelowMinimumAge);
    }

    [Fact]
    public void AdultLegalUnionAndRomanceStateAreAccepted()
    {
        CharacterWorldState characters = CreateCharacters(2);
        MarriageProposalState proposal = AcceptedProposal(
            "adult",
            Character(0),
            Character(1),
            basis: MarriageBasis.Romantic);
        MarriageUnionState union = Union("adult", proposal);
        RomanceRouteState route = Route("adult", Character(0), Character(1));
        CharacterMarriageWorldState state = new(
            Snapshot([Practice()], [proposal], [], [union], [route], []),
            characters,
            Calendar);

        Assert.True(Assert.Single(state.Unions).IsActive);
        Assert.Equal(MarriageBasis.Romantic, state.Unions[0].Basis);
        Assert.Equal(MarriageConsentKind.Voluntary, state.Unions[0].ConsentKind);
        Assert.True(Assert.Single(state.RomanceRoutes).IsActive);
    }

    [Fact]
    public void MinorsMayOnlyEnterEnabledPoliticalBetrothalAndNeverRomanceOrLegalUnion()
    {
        CharacterWorldState characters = CreateCharacters(
            2,
            birthDates: new Dictionary<EntityId, CampaignDate>
            {
                [Character(0)] = new CampaignDate(188, 1, 1),
                [Character(1)] = new CampaignDate(187, 1, 1),
            });
        MarriageProposalState proposal = AcceptedProposal(
            "minor",
            Character(0),
            Character(1),
            kind: MarriageProposalKind.PoliticalBetrothal,
            basis: MarriageBasis.Political,
            consent: MarriageConsentKind.PoliticalArrangement);
        PoliticalBetrothalState betrothal = Betrothal("minor", proposal);
        CharacterMarriageWorldState state = new(
            Snapshot([Practice()], [proposal], [betrothal], [], [], []),
            characters,
            Calendar);

        Assert.True(Assert.Single(state.Betrothals).Status == PoliticalBetrothalStatus.Active);
        CharacterMarriageWorldState evaluator = NewState(characters, Practice());
        Assert.True(evaluator.EvaluateEligibility(
            Eligibility(
                MarriageEligibilityCategory.PoliticalBetrothal,
                Character(0),
                Character(1),
                MarriageUnionForm.PrincipalSpouse),
            Date).IsEligible);
        AssertIssue(
            evaluator.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.VoluntaryLegalUnion,
                    Character(0),
                    Character(1),
                    MarriageUnionForm.PrincipalSpouse),
                Date),
            MarriageEligibilityReason.BelowMinimumAge);
        AssertIssue(
            evaluator.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.VoluntaryRomance,
                    Character(0),
                    Character(1)),
                Date),
            MarriageEligibilityReason.BelowMinimumAge);
        AssertInvalid(
            Snapshot([Practice()], [], [], [], [Route("minor", Character(0), Character(1))], []),
            characters);
        AssertInvalid(
            Snapshot(
                [Practice(allowsMinorBetrothal: false)],
                [proposal],
                [betrothal],
                [],
                [],
                []),
            characters);

    }

    public static TheoryData<CharacterConditionState, MarriageEligibilityReason?>
        VoluntaryConditionMatrix => new()
        {
            { CharacterConditionState.Default, null },
            {
                new CharacterConditionState(
                    CharacterVitalStatus.Dead,
                    CharacterHealthStatus.Critical,
                    IsIncapacitated: true,
                    CharacterCustodyStatus.Free,
                    null),
                MarriageEligibilityReason.Dead
            },
            {
                CharacterConditionState.Default with { IsIncapacitated = true },
                MarriageEligibilityReason.Incapacitated
            },
            { Custody(CharacterCustodyStatus.Detained), MarriageEligibilityReason.InCustody },
            { Custody(CharacterCustodyStatus.Captive), MarriageEligibilityReason.InCustody },
            { Custody(CharacterCustodyStatus.Hostage), MarriageEligibilityReason.InCustody },
        };

    [Theory]
    [MemberData(nameof(VoluntaryConditionMatrix))]
    public void VoluntaryLegalUnionAndRomanceUseFullLifeAgencyAndCustodyMatrix(
        CharacterConditionState condition,
        MarriageEligibilityReason? expectedIssue)
    {
        CharacterWorldState characters = CreateCharacters(
            3,
            conditions: new Dictionary<EntityId, CharacterConditionState>
            {
                [Character(1)] = condition,
            });
        CharacterMarriageWorldState state = NewState(characters, Practice());

        foreach (MarriageEligibilityCategory category in new[]
                 {
                     MarriageEligibilityCategory.VoluntaryLegalUnion,
                     MarriageEligibilityCategory.VoluntaryRomance,
                 })
        {
            MarriageEligibilityResult result = state.EvaluateEligibility(
                Eligibility(
                    category,
                    Character(0),
                    Character(1),
                    category == MarriageEligibilityCategory.VoluntaryLegalUnion
                        ? MarriageUnionForm.PrincipalSpouse
                        : null),
                Date);
            if (expectedIssue is MarriageEligibilityReason issue)
            {
                AssertIssue(result, issue);
            }
            else
            {
                Assert.True(result.IsEligible);
            }
        }
    }

    [Theory]
    [MemberData(nameof(VoluntaryConditionMatrix))]
    public void PoliticalCategoriesRequireLifeButDoNotMisclassifyCustodyAsConsent(
        CharacterConditionState condition,
        MarriageEligibilityReason? voluntaryIssue)
    {
        CharacterWorldState characters = CreateCharacters(
            3,
            conditions: new Dictionary<EntityId, CharacterConditionState>
            {
                [Character(1)] = condition,
            });
        CharacterMarriageWorldState state = NewState(characters, Practice());

        MarriageEligibilityResult betrothal = state.EvaluateEligibility(
            Eligibility(
                MarriageEligibilityCategory.PoliticalBetrothal,
                Character(0),
                Character(1),
                MarriageUnionForm.PrincipalSpouse),
            Date);
        MarriageEligibilityResult coercive = state.EvaluateEligibility(
            Eligibility(
                MarriageEligibilityCategory.CoercivePoliticalAction,
                Character(0),
                Character(1)),
            Date);
        if (voluntaryIssue == MarriageEligibilityReason.Dead)
        {
            AssertIssue(betrothal, MarriageEligibilityReason.Dead);
            AssertIssue(coercive, MarriageEligibilityReason.Dead);
        }
        else
        {
            Assert.True(betrothal.IsEligible);
            Assert.True(coercive.IsEligible);
            Assert.DoesNotContain(
                betrothal.Issues,
                issue => issue.Reason is MarriageEligibilityReason.Incapacitated
                    or MarriageEligibilityReason.InCustody);
            Assert.DoesNotContain(
                coercive.Issues,
                issue => issue.Reason is MarriageEligibilityReason.Incapacitated
                    or MarriageEligibilityReason.InCustody);
        }
    }

    [Fact]
    public void DirectLineAndSiblingProhibitionsAreIndependentExplicitPracticeFlags()
    {
        Dictionary<EntityId, IReadOnlyList<EntityId>> parents = new()
        {
            [Character(1)] = [Character(0)],
            [Character(2)] = [Character(0)],
        };
        Dictionary<EntityId, CampaignDate> births = new()
        {
            [Character(0)] = new CampaignDate(140, 1, 1),
            [Character(1)] = new CampaignDate(165, 1, 1),
            [Character(2)] = new CampaignDate(166, 1, 1),
        };
        CharacterWorldState characters = CreateCharacters(3, birthDates: births, parents: parents);

        CharacterMarriageWorldState unrestricted = NewState(
            characters,
            Practice(flags: MarriageProhibitedKinship.None));
        Assert.True(EvaluateLegal(unrestricted, Character(0), Character(1)).IsEligible);
        Assert.True(EvaluateLegal(unrestricted, Character(1), Character(2)).IsEligible);

        CharacterMarriageWorldState directOnly = NewState(
            characters,
            Practice(flags: MarriageProhibitedKinship.DirectLine));
        AssertIssue(
            EvaluateLegal(directOnly, Character(0), Character(1)),
            MarriageEligibilityReason.ProhibitedDirectLineKinship);
        Assert.True(EvaluateLegal(directOnly, Character(1), Character(2)).IsEligible);

        CharacterMarriageWorldState siblingOnly = NewState(
            characters,
            Practice(flags: MarriageProhibitedKinship.Siblings));
        Assert.True(EvaluateLegal(siblingOnly, Character(0), Character(1)).IsEligible);
        AssertIssue(
            EvaluateLegal(siblingOnly, Character(1), Character(2)),
            MarriageEligibilityReason.ProhibitedSiblingKinship);

        CharacterMarriageWorldState both = NewState(
            characters,
            Practice(flags: MarriageProhibitedKinship.DirectLine | MarriageProhibitedKinship.Siblings));
        AssertIssue(
            EvaluateLegal(both, Character(0), Character(1)),
            MarriageEligibilityReason.ProhibitedDirectLineKinship);
        AssertIssue(
            EvaluateLegal(both, Character(1), Character(2)),
            MarriageEligibilityReason.ProhibitedSiblingKinship);
    }

    [Fact]
    public void BetrothalAndUnionMustExactlyMatchTheirAcceptedSourceProposal()
    {
        CharacterWorldState characters = CreateCharacters(4);
        MarriagePracticeState practice = Practice();
        MarriageProposalState legal = AcceptedProposal("legal", Character(0), Character(1));
        MarriageProposalState political = AcceptedProposal(
            "political",
            Character(2),
            Character(3),
            kind: MarriageProposalKind.PoliticalBetrothal,
            consent: MarriageConsentKind.PoliticalArrangement);
        MarriageUnionState union = Union("legal", legal);
        PoliticalBetrothalState betrothal = Betrothal("political", political);
        CharacterMarriageWorldSnapshot valid = Snapshot(
            [practice],
            [legal, political],
            [betrothal],
            [union],
            [],
            []);
        _ = new CharacterMarriageWorldState(valid, characters, Calendar);

        AssertInvalid(valid with
        {
            Unions = [union with { SourceProposalId = new EntityId("marriage_proposal:test/missing") }],
        }, characters);
        AssertInvalid(valid with
        {
            Proposals = [legal with { Status = MarriageProposalStatus.Refused }, political],
        }, characters);
        AssertInvalid(valid with
        {
            Unions = [union with { Basis = MarriageBasis.Romantic }],
        }, characters);
        AssertInvalid(valid with
        {
            Unions = [union with { StartTurnIndex = 2 }],
        }, characters);
        AssertInvalid(valid with
        {
            Betrothals = [betrothal with { IntendedForm = MarriageUnionForm.Concubinage }],
        }, characters);
        AssertInvalid(valid with
        {
            Betrothals = [betrothal with { FirstCharacterId = Character(1) }],
        }, characters);
    }

    [Fact]
    public void AcceptedProposalsOwnExactlyOneTypedOutcomeAndCreationCommandsAreUnique()
    {
        CharacterWorldState characters = CreateCharacters(6);
        MarriagePracticeState practice = Practice(maxPrincipal: 2);
        MarriageProposalState legal = AcceptedProposal("causal_legal", Character(0), Character(1));
        MarriageUnionState union = Union("causal_legal", legal);
        CharacterMarriageWorldSnapshot valid = Snapshot(
            [practice],
            [legal],
            [],
            [union],
            [],
            []);
        _ = new CharacterMarriageWorldState(valid, characters, Calendar);

        AssertInvalid(valid with { Unions = [] }, characters);
        AssertInvalid(valid with
        {
            Unions =
            [
                union,
                union with { UnionId = new EntityId("marriage_union:test/causal_duplicate") },
            ],
        }, characters);

        MarriageProposalState activeOne = ActiveProposal(
            "causal_active_one",
            Character(2),
            Character(3));
        MarriageProposalState activeTwo = ActiveProposal(
            "causal_active_two",
            Character(4),
            Character(5)) with
        {
            SourceCommandId = activeOne.SourceCommandId,
        };
        AssertInvalid(
            Snapshot([practice], [activeOne, activeTwo], [], [], [], []),
            characters);

        MarriageProposalState coerced = AcceptedProposal(
            "causal_coerced",
            Character(2),
            Character(3),
            basis: MarriageBasis.Political,
            consent: MarriageConsentKind.Coerced);
        RomanceRouteState reusedCommand = Route(
            "causal_reused",
            Character(4),
            Character(5)) with
        {
            SourceCommandId = coerced.SourceCommandId,
        };
        AssertInvalid(
            Snapshot(
                [practice],
                [coerced],
                [],
                [Union("causal_coerced", coerced)],
                [reusedCommand],
                []),
            characters);

        RomanceRouteState acceptanceCommandReused = Route(
            "causal_acceptance_reused",
            Character(4),
            Character(5)) with
        {
            SourceCommandId = coerced.ResolutionCommandId!.Value,
        };
        AssertInvalid(
            Snapshot(
                [practice],
                [coerced],
                [],
                [Union("causal_coerced", coerced)],
                [acceptanceCommandReused],
                []),
            characters);

        RomanceRouteState coerciveAdvance = VersionTwoRoute(
            "causal_coercive_advance",
            Character(4),
            Character(5),
            practice.PracticeId) with
        {
            LastPositiveProgressCommandId = coerced.SourceCommandId,
        };
        AssertInvalid(
            Snapshot(
                [practice],
                [coerced],
                [],
                [Union("causal_coerced", coerced)],
                [coerciveAdvance],
                []),
            characters);

        CampaignDate invitationDate = Date.AddDays(-1);
        EntityId coerciveInvitationId = CharacterMarriageIds.DeriveRomanceInvitationId(
            invitationDate,
            coerced.SourceCommandId);
        RomanceInvitationState coerciveInvitation = new(
            CharacterMarriageContractVersions.RomanceInvitationState,
            coerciveInvitationId,
            Character(4),
            Character(5),
            practice.PracticeId,
            invitationDate,
            1,
            coerced.SourceCommandId);
        AssertInvalid(
            new CharacterMarriageWorldSnapshot(
                CharacterMarriageContractVersions.Snapshot,
                [practice],
                [coerced],
                [],
                [Union("causal_coerced", coerced)],
                [],
                [],
                [coerciveInvitation]),
            characters);

        RomanceRouteState copiedCoerciveInvitation = VersionTwoRoute(
            "causal_coercive_invitation",
            Character(4),
            Character(5),
            practice.PracticeId) with
        {
            SourceInvitationId = coerciveInvitationId,
            InvitationCreatedDate = invitationDate,
            InvitationCreatedTurnIndex = 1,
            InvitationSourceCommandId = coerced.SourceCommandId,
        };
        copiedCoerciveInvitation = copiedCoerciveInvitation with
        {
            RouteId = CharacterMarriageIds.DeriveRomanceRouteId(
                coerciveInvitationId,
                copiedCoerciveInvitation.SourceCommandId),
        };
        AssertInvalid(
            Snapshot(
                [practice],
                [coerced],
                [],
                [Union("causal_coerced", coerced)],
                [copiedCoerciveInvitation],
                []),
            characters);

        RomanceRouteState invalidatedByCoercion = Route(
            "causal_invalidated",
            Character(4),
            Character(5)) with
        {
            Status = RomanceRouteStatus.Invalidated,
            ResolutionDate = coerced.ResolutionDate,
            ResolutionTurnIndex = coerced.ResolutionTurnIndex,
            ResolutionCommandId = coerced.ResolutionCommandId,
        };
        _ = new CharacterMarriageWorldState(
            Snapshot(
                [practice],
                [coerced],
                [],
                [Union("causal_coerced", coerced)],
                [invalidatedByCoercion],
                []),
            characters,
            Calendar);
    }

    [Fact]
    public void FulfilledBetrothalIdentifiesOneExactPoliticalArrangementUnion()
    {
        CharacterWorldState characters = CreateCharacters(2);
        MarriagePracticeState practice = Practice(maxPrincipal: 2);
        MarriageProposalState political = AcceptedProposal(
            "fulfillment_betrothal",
            Character(0),
            Character(1),
            kind: MarriageProposalKind.PoliticalBetrothal,
            basis: MarriageBasis.Political,
            consent: MarriageConsentKind.PoliticalArrangement);
        MarriageProposalState legal = AcceptedProposal(
            "fulfillment_union",
            Character(0),
            Character(1),
            basis: MarriageBasis.Political,
            consent: MarriageConsentKind.PoliticalArrangement);
        MarriageUnionState union = Union("fulfillment_union", legal);
        PoliticalBetrothalState fulfilled = Betrothal("fulfillment_betrothal", political) with
        {
            Status = PoliticalBetrothalStatus.Fulfilled,
            FulfillmentUnionId = union.UnionId,
            ResolutionDate = union.StartDate,
            ResolutionTurnIndex = union.StartTurnIndex,
            ResolutionCommandId = legal.ResolutionCommandId,
        };
        CharacterMarriageWorldSnapshot valid = Snapshot(
            [practice],
            [political, legal],
            [fulfilled],
            [union],
            [],
            []);

        CharacterMarriageWorldState state = new(valid, characters, Calendar);
        Assert.Equal(union.UnionId, Assert.Single(state.Betrothals).FulfillmentUnionId);
        AssertInvalid(valid with
        {
            Betrothals = [fulfilled with { FulfillmentUnionId = null }],
        }, characters);
        AssertInvalid(valid with
        {
            Betrothals =
            [
                fulfilled with
                {
                    FulfillmentUnionId = new EntityId("marriage_union:test/missing"),
                },
            ],
        }, characters);
        AssertInvalid(valid with
        {
            Betrothals =
            [
                fulfilled with
                {
                    ResolutionCommandId = new EntityId("command:test/wrong_fulfillment"),
                },
            ],
        }, characters);
        AssertInvalid(valid with
        {
            Betrothals =
            [
                fulfilled with
                {
                    Status = PoliticalBetrothalStatus.Released,
                },
            ],
        }, characters);
    }

    [Fact]
    public void PrincipalAndConcubinageRolesAndConfigurableLimitsAreEnforced()
    {
        CharacterWorldState characters = CreateCharacters(6);
        MarriagePracticeState practice = Practice(
            maxPrincipal: 1,
            maxConcubinagePrincipal: 1,
            maxConcubinagePartner: 1);
        MarriageProposalState principalProposal = AcceptedProposal(
            "principal",
            Character(0),
            Character(1));
        MarriageUnionState principal = Union("principal", principalProposal);
        MarriageProposalState concubinageProposal = AcceptedProposal(
            "concubinage",
            Character(2),
            Character(3),
            form: MarriageUnionForm.Concubinage,
            principal: Character(2));
        MarriageUnionState concubinage = Union("concubinage", concubinageProposal);
        CharacterMarriageWorldState state = new(
            Snapshot(
                [practice],
                [principalProposal, concubinageProposal],
                [],
                [principal, concubinage],
                [],
                []),
            characters,
            Calendar);

        AssertIssue(
            EvaluateLegal(state, Character(0), Character(4)),
            MarriageEligibilityReason.ActiveUnionLimitReached);
        AssertIssue(
            state.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.VoluntaryLegalUnion,
                    Character(2),
                    Character(4),
                    MarriageUnionForm.Concubinage,
                    Character(2)),
                Date),
            MarriageEligibilityReason.ActiveUnionLimitReached);
        AssertIssue(
            state.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.CoercivePoliticalAction,
                    Character(2),
                    Character(4),
                    MarriageUnionForm.Concubinage,
                    Character(2)),
                Date),
            MarriageEligibilityReason.ActiveUnionLimitReached);
        AssertIssue(
            state.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.VoluntaryLegalUnion,
                    Character(4),
                    Character(3),
                    MarriageUnionForm.Concubinage,
                    Character(4)),
                Date),
            MarriageEligibilityReason.ActiveUnionLimitReached);

        AssertIssue(
            state.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.VoluntaryLegalUnion,
                    Character(4),
                    Character(5),
                    MarriageUnionForm.Concubinage,
                    null),
                Date),
            MarriageEligibilityReason.InvalidConcubinagePrincipal);
        AssertIssue(
            state.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.VoluntaryLegalUnion,
                    Character(4),
                    Character(5),
                    MarriageUnionForm.PrincipalSpouse,
                    Character(4)),
                Date),
            MarriageEligibilityReason.InvalidConcubinagePrincipal);
        AssertInvalid(
            Snapshot(
                [practice],
                [principalProposal with { ConcubinagePrincipalCharacterId = Character(0) }],
                [],
                [],
                [],
                []),
            characters);
    }

    [Fact]
    public void EveryLegalRelationshipEligibilityCategoryHonorsTheGlobalActiveBound()
    {
        CharacterWorldState characters = CreateCharacters(66);
        MarriagePracticeState practice = Practice(
            maxPrincipal: 8,
            maxConcubinagePrincipal: 64,
            maxConcubinagePartner: 64);
        MarriageProposalState[] proposals = Enumerable.Range(1, 64)
            .Select(index => AcceptedProposal(
                $"global_limit_{index:D2}",
                Character(0),
                Character(index),
                form: MarriageUnionForm.Concubinage,
                principal: Character(0)))
            .ToArray();
        MarriageUnionState[] unions = proposals
            .Select((proposal, index) => Union($"global_limit_{index + 1:D2}", proposal))
            .ToArray();
        CharacterMarriageWorldState state = new(
            Snapshot([practice], proposals, [], unions, [], []),
            characters,
            Calendar);

        AssertIssue(
            state.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.PoliticalBetrothal,
                    Character(0),
                    Character(65),
                    MarriageUnionForm.PrincipalSpouse),
                Date),
            MarriageEligibilityReason.ActiveUnionLimitReached);
        AssertIssue(
            state.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.CoercivePoliticalAction,
                    Character(0),
                    Character(65),
                    MarriageUnionForm.PrincipalSpouse),
                Date),
            MarriageEligibilityReason.ActiveUnionLimitReached);
    }

    [Fact]
    public void CoerciveUnionEligibilityUsesThePracticesMinimumAgeForBothParticipants()
    {
        CharacterWorldState characters = CreateCharacters(
            3,
            birthDates: new Dictionary<EntityId, CampaignDate>
            {
                [Character(0)] = new CampaignDate(181, 5, 10),
                [Character(1)] = new CampaignDate(179, 5, 10),
                [Character(2)] = new CampaignDate(179, 5, 10),
            });
        CharacterMarriageWorldState state = NewState(
            characters,
            Practice(minLegal: 21));

        AssertIssue(
            state.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.CoercivePoliticalAction,
                    Character(0),
                    Character(1),
                    MarriageUnionForm.PrincipalSpouse),
                Date),
            MarriageEligibilityReason.BelowMinimumAge);
        Assert.True(state.EvaluateEligibility(
            Eligibility(
                MarriageEligibilityCategory.CoercivePoliticalAction,
                Character(1),
                Character(2),
                MarriageUnionForm.PrincipalSpouse),
            Date).IsEligible);
    }

    [Fact]
    public void DuplicateActivePairsAndBetrothalUnionConflictAreRejected()
    {
        CharacterWorldState characters = CreateCharacters(4);
        MarriagePracticeState practice = Practice(maxPrincipal: 2);
        MarriageProposalState activeOne = ActiveProposal("active_one", Character(0), Character(1));
        MarriageProposalState activeTwo = ActiveProposal("active_two", Character(1), Character(0));
        AssertInvalid(Snapshot([practice], [activeOne, activeTwo], [], [], [], []), characters);

        MarriageProposalState legal = AcceptedProposal("legal", Character(0), Character(1));
        MarriageProposalState political = AcceptedProposal(
            "political",
            Character(0),
            Character(1),
            kind: MarriageProposalKind.PoliticalBetrothal,
            consent: MarriageConsentKind.PoliticalArrangement);
        AssertInvalid(
            Snapshot(
                [practice],
                [legal, political],
                [Betrothal("political", political)],
                [Union("legal", legal)],
                [],
                []),
            characters);

        MarriageProposalState activeAgainstUnion = ActiveProposal(
            "active_against_union",
            Character(1),
            Character(0));
        AssertInvalid(
            Snapshot(
                [practice],
                [legal, activeAgainstUnion],
                [],
                [Union("legal", legal)],
                [],
                []),
            characters);

        MarriageProposalState politicalOtherPair = AcceptedProposal(
            "political_other_pair",
            Character(2),
            Character(3),
            kind: MarriageProposalKind.PoliticalBetrothal,
            consent: MarriageConsentKind.PoliticalArrangement);
        MarriageProposalState activeAgainstBetrothal = ActiveProposal(
            "active_against_betrothal",
            Character(3),
            Character(2));
        AssertInvalid(
            Snapshot(
                [practice],
                [politicalOtherPair, activeAgainstBetrothal],
                [Betrothal("political_other_pair", politicalOtherPair)],
                [],
                [],
                []),
            characters);

        RomanceRouteState routeOne = Route("one", Character(2), Character(3));
        RomanceRouteState routeTwo = Route("two", Character(2), Character(3));
        AssertInvalid(Snapshot([practice], [], [], [], [routeOne, routeTwo], []), characters);
    }

    [Fact]
    public void ExactlySixtyFourRetainedRecordsAreAcceptedAndSixtyFiveRejected()
    {
        CharacterWorldState characters = CreateCharacters(66);
        MarriagePracticeState practice = Practice();
        MarriageProposalState[] proposals = Enumerable.Range(1, 64)
            .Select(index => TerminalProposal($"retained_{index:D3}", Character(0), Character(index)))
            .ToArray();
        RomanceRouteState[] routes = Enumerable.Range(1, 64)
            .Select(index => EndedRoute($"retained_{index:D3}", Character(0), Character(index)))
            .ToArray();
        CharacterMarriageWorldState exact = new(
            Snapshot([practice], proposals, [], [], routes, []),
            characters,
            Calendar);

        Assert.Equal(64, exact.GetProposalsInvolving(Character(0)).Count);
        Assert.Equal(64, exact.GetRomanceRoutesInvolving(Character(0)).Count);
        AssertInvalid(
            exact.CaptureSnapshot() with
            {
                Proposals =
                [
                    .. exact.Proposals,
                    TerminalProposal("overflow", Character(0), Character(65)),
                ],
            },
            characters);
        AssertInvalid(
            exact.CaptureSnapshot() with
            {
                RomanceRoutes =
                [
                    .. exact.RomanceRoutes,
                    EndedRoute("overflow", Character(0), Character(65)),
                ],
            },
            characters);
    }

    [Fact]
    public void EstablishedUnionPersistsAcrossLaterIncapacityAndCustody()
    {
        CharacterWorldState initialCharacters = CreateCharacters(3);
        MarriageProposalState proposal = AcceptedProposal("persist", Character(0), Character(1));
        CharacterMarriageWorldState established = new(
            Snapshot([Practice()], [proposal], [], [Union("persist", proposal)], [], []),
            initialCharacters,
            Calendar);
        CharacterWorldState laterCharacters = CreateCharacters(
            3,
            conditions: new Dictionary<EntityId, CharacterConditionState>
            {
                [Character(0)] = CharacterConditionState.Default with { IsIncapacitated = true },
                [Character(1)] = Custody(CharacterCustodyStatus.Hostage),
            });
        CharacterMarriageWorldState restored = new(
            established.CaptureSnapshot(),
            laterCharacters,
            Calendar);

        Assert.True(Assert.Single(restored.Unions).IsActive);
        AssertIssue(
            restored.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.VoluntaryRomance,
                    Character(0),
                    Character(2)),
                Date),
            MarriageEligibilityReason.Incapacitated);
        AssertIssue(
            restored.EvaluateEligibility(
                Eligibility(
                    MarriageEligibilityCategory.VoluntaryRomance,
                    Character(1),
                    Character(2)),
                Date),
            MarriageEligibilityReason.InCustody);
    }

    [Fact]
    public void WidowRemarriagePolicyIsExplicitAndRetainsTheEndedUnion()
    {
        CharacterWorldState characters = CreateCharacters(
            3,
            conditions: new Dictionary<EntityId, CharacterConditionState>
            {
                [Character(1)] = new CharacterConditionState(
                    CharacterVitalStatus.Dead,
                    CharacterHealthStatus.Critical,
                    IsIncapacitated: true,
                    CharacterCustodyStatus.Free,
                    null),
            });
        MarriageProposalState oldProposal = AcceptedProposal("widow_old", Character(0), Character(1));
        MarriageUnionState oldUnion = Union("widow_old", oldProposal) with
        {
            Status = MarriageUnionStatus.Ended,
            EndDate = Date,
            EndTurnIndex = 2,
            EndCommandId = new EntityId("command:test/widow_old/end"),
            EndReason = MarriageUnionEndReason.SpouseDied,
        };

        CharacterMarriageWorldState disabled = new(
            Snapshot(
                [Practice(allowsWidowRemarriage: false)],
                [oldProposal],
                [],
                [oldUnion],
                [],
                []),
            characters,
            Calendar);
        AssertIssue(
            EvaluateLegal(disabled, Character(0), Character(2)),
            MarriageEligibilityReason.WidowRemarriageDisabled);
        Assert.Single(disabled.GetUnionsInvolving(Character(0)));

        CharacterMarriageWorldState enabled = new(
            disabled.CaptureSnapshot() with
            {
                Practices = [Practice(allowsWidowRemarriage: true)],
            },
            characters,
            Calendar);
        Assert.True(EvaluateLegal(enabled, Character(0), Character(2)).IsEligible);
        Assert.Equal(MarriageUnionStatus.Ended, Assert.Single(enabled.Unions).Status);
    }

    [Fact]
    public void CoercedStateMustRemainPoliticalAndCanNeverBeClassifiedAsRomantic()
    {
        CharacterWorldState characters = CreateCharacters(2);
        MarriageProposalState coercedPolitical = AcceptedProposal(
            "coerced",
            Character(0),
            Character(1),
            basis: MarriageBasis.Political,
            consent: MarriageConsentKind.Coerced);
        MarriageUnionState union = Union("coerced", coercedPolitical);
        CharacterMarriageWorldState state = new(
            Snapshot([Practice()], [coercedPolitical], [], [union], [], []),
            characters,
            Calendar);

        Assert.Equal(MarriageBasis.Political, Assert.Single(state.Unions).Basis);
        Assert.Equal(MarriageConsentKind.Coerced, state.Unions[0].ConsentKind);
        Assert.Empty(state.RomanceRoutes);
        AssertInvalid(
            state.CaptureSnapshot() with
            {
                Proposals = [coercedPolitical with { Basis = MarriageBasis.Romantic }],
                Unions = [],
            },
            characters);
        AssertInvalid(
            state.CaptureSnapshot() with
            {
                Proposals = [coercedPolitical with { Basis = MarriageBasis.Romantic }],
                Unions = [union with { Basis = MarriageBasis.Romantic }],
            },
            characters);
    }

    [Fact]
    public void ShuffledInputCanonicalizesQueriesAreDefensiveAndChecksumChangesWithState()
    {
        CharacterWorldState characters = CreateCharacters(4);
        MarriagePracticeState practiceA = Practice("a");
        MarriagePracticeState practiceB = Practice("b");
        MarriageProposalState proposalA = AcceptedProposal(
            "a",
            Character(0),
            Character(1),
            practiceId: practiceA.PracticeId);
        MarriageProposalState proposalB = AcceptedProposal(
            "b",
            Character(2),
            Character(3),
            practiceId: practiceB.PracticeId);
        MarriageUnionState unionA = Union("a", proposalA);
        MarriageUnionState unionB = Union("b", proposalB);
        CharacterMarriageHistoryAggregate historyA = new(
            CharacterMarriageContractVersions.State,
            Character(0),
            2,
            0,
            1,
            0,
            Date.AddDays(-10),
            Date.AddDays(-1));
        CharacterMarriageHistoryAggregate historyB = new(
            CharacterMarriageContractVersions.State,
            Character(2),
            1,
            0,
            1,
            0,
            Date.AddDays(-8),
            Date);
        CharacterMarriageWorldSnapshot ordered = Snapshot(
            [practiceA, practiceB],
            [proposalA, proposalB],
            [],
            [unionA, unionB],
            [],
            [historyA, historyB]);
        CharacterMarriageWorldSnapshot shuffled = ordered with
        {
            Practices = ordered.Practices.Reverse().ToArray(),
            Proposals = ordered.Proposals.Reverse().ToArray(),
            Unions = ordered.Unions.Reverse().ToArray(),
            History = ordered.History.Reverse().ToArray(),
        };
        CharacterMarriageWorldState first = new(ordered, characters, Calendar);
        CharacterMarriageWorldState second = new(shuffled, characters, Calendar);

        Assert.Equal(Serialize(first.CaptureSnapshot()), Serialize(second.CaptureSnapshot()));
        Assert.Equal(Checksum(characters, first), Checksum(characters, second));
        Assert.NotEqual(
            Checksum(characters, first),
            Checksum(
                characters,
                new CharacterMarriageWorldState(
                    first.CaptureSnapshot() with
                    {
                        History = [historyA with { FoldedProposalCount = 3 }, historyB],
                    },
                    characters,
                    Calendar)));

        MarriagePracticeState[] practices = Assert.IsType<MarriagePracticeState[]>(first.Practices);
        practices[0] = practices[0] with { AllowsWidowRemarriage = false };
        MarriageUnionState[] unions = Assert.IsType<MarriageUnionState[]>(first.Unions);
        unions[0] = unions[0] with { Status = MarriageUnionStatus.Ended };
        CharacterMarriageWorldSnapshot captured = first.CaptureSnapshot();
        MarriageProposalState[] capturedProposals = Assert.IsType<MarriageProposalState[]>(captured.Proposals);
        capturedProposals[0] = capturedProposals[0] with { Status = MarriageProposalStatus.Refused };
        Assert.All(first.Practices, practice => Assert.True(practice.AllowsWidowRemarriage));
        Assert.All(first.Unions, union => Assert.Equal(MarriageUnionStatus.Active, union.Status));
        Assert.All(first.Proposals, proposal => Assert.Equal(MarriageProposalStatus.Accepted, proposal.Status));
        Assert.True(first.TryGetUnion(unionA.UnionId, out MarriageUnionState? queried));
        Assert.Equal(unionA, queried);
    }

    [Fact]
    public void RomanceInvitationAndVersionTwoRouteQueriesAreCanonicalAndDefensive()
    {
        CharacterWorldState characters = CreateCharacters(4);
        MarriagePracticeState practice = Practice();
        EntityId offerCommand = new("command:test/query-romance-offer");
        RomanceInvitationState invitation = new(
            CharacterMarriageContractVersions.RomanceInvitationState,
            CharacterMarriageIds.DeriveRomanceInvitationId(Date.AddDays(-1), offerCommand),
            Character(2),
            Character(3),
            practice.PracticeId,
            Date.AddDays(-1),
            0,
            offerCommand);
        RomanceRouteState route = VersionTwoRoute(
            "query-v2",
            Character(0),
            Character(1),
            practice.PracticeId);
        CharacterMarriageWorldState state = new(
            new CharacterMarriageWorldSnapshot(
                CharacterMarriageContractVersions.Snapshot,
                [practice],
                [],
                [],
                [],
                [route],
                [],
                [invitation]),
            characters,
            Calendar);

        RomanceInvitationState[] invitations = Assert.IsType<RomanceInvitationState[]>(
            state.RomanceInvitations);
        invitations[0] = invitations[0] with { RecipientCharacterId = Character(0) };
        RomanceRouteState[] routes = Assert.IsType<RomanceRouteState[]>(state.RomanceRoutes);
        routes[0] = routes[0] with { ProgressLevel = 3 };
        CharacterMarriageWorldSnapshot captured = state.CaptureSnapshot();
        RomanceInvitationState[] capturedInvitations = Assert.IsType<
            RomanceInvitationState[]>(captured.Invitations);
        capturedInvitations[0] = capturedInvitations[0] with
        {
            RecipientCharacterId = Character(0),
        };

        Assert.Equal(invitation, Assert.Single(state.RomanceInvitations));
        Assert.Equal(route, Assert.Single(state.RomanceRoutes));
        Assert.True(state.TryGetRomanceInvitation(
            invitation.InvitationId,
            out RomanceInvitationState? queriedInvitation));
        Assert.Equal(invitation, queriedInvitation);
        Assert.True(state.TryGetRomanceRoute(route.RouteId, out RomanceRouteState? queriedRoute));
        Assert.Equal(route, queriedRoute);
        Assert.Equal(
            invitation,
            Assert.Single(state.GetRomanceInvitationsInvolving(Character(2))));
    }

    [Fact]
    public void VersionTwoRomanceRoutesRequireExactInvitationIdentityAndProgressSemantics()
    {
        CharacterWorldState characters = CreateCharacters(2);
        MarriagePracticeState practice = Practice();
        RomanceRouteState valid = VersionTwoRoute(
            "validation-v2",
            Character(0),
            Character(1),
            practice.PracticeId);
        CharacterMarriageWorldSnapshot snapshot = Snapshot(
            [practice],
            [],
            [],
            [],
            [valid],
            []);
        _ = new CharacterMarriageWorldState(snapshot, characters, Calendar);

        AssertInvalid(snapshot with
        {
            RomanceRoutes = [valid with { RouteId = new EntityId("romance_route:test/wrong-v2") }],
        }, characters);
        AssertInvalid(snapshot with
        {
            RomanceRoutes = [valid with { SourceInvitationId = new EntityId("romance_invitation:test/wrong") }],
        }, characters);
        AssertInvalid(snapshot with
        {
            RomanceRoutes = [valid with { ProgressLevel = 4 }],
        }, characters);
        AssertInvalid(snapshot with
        {
            RomanceRoutes =
            [
                valid with
                {
                    Status = RomanceRouteStatus.Completed,
                    ProgressLevel = 3,
                    ResolutionDate = Date,
                    ResolutionTurnIndex = 1,
                    ResolutionCommandId = valid.LastPositiveProgressCommandId,
                },
            ],
        }, characters);
        AssertInvalid(snapshot with
        {
            RomanceRoutes = [valid with { InvitationSourceCommandId = null }],
        }, characters);

        RomanceRouteState levelOne = valid with
        {
            ProgressLevel = 1,
            LastPositiveProgressDate = valid.StartDate,
            LastPositiveProgressTurnIndex = valid.StartTurnIndex,
            LastPositiveProgressCommandId = valid.SourceCommandId,
        };
        _ = new CharacterMarriageWorldState(
            snapshot with { RomanceRoutes = [levelOne] },
            characters,
            Calendar);
        AssertInvalid(snapshot with
        {
            RomanceRoutes =
            [
                levelOne with
                {
                    LastPositiveProgressCommandId = new EntityId(
                        "command:test/validation-v2/false-level-one-progress"),
                },
            ],
        }, characters);
        AssertInvalid(snapshot with
        {
            RomanceRoutes = [valid with { LastPositiveProgressCommandId = valid.SourceCommandId }],
        }, characters);
        AssertInvalid(snapshot with
        {
            RomanceRoutes =
            [
                valid with
                {
                    LastPositiveProgressCommandId = valid.InvitationSourceCommandId,
                },
            ],
        }, characters);

        CharacterWorldState fourCharacters = CreateCharacters(4);
        RomanceRouteState second = VersionTwoRoute(
            "validation-v2-second",
            Character(2),
            Character(3),
            practice.PracticeId) with
        {
            LastPositiveProgressCommandId = valid.LastPositiveProgressCommandId,
        };
        AssertInvalid(
            snapshot with { RomanceRoutes = [valid, second] },
            fourCharacters);
    }

    [Fact]
    public void FoldedHistoryCannotPredateItsCharacterBirth()
    {
        CharacterWorldState characters = CreateCharacters(
            2,
            birthDates: new Dictionary<EntityId, CampaignDate>
            {
                [Character(0)] = new CampaignDate(190, 1, 1),
            });
        CharacterMarriageHistoryAggregate valid = new(
            CharacterMarriageContractVersions.State,
            Character(0),
            1,
            0,
            0,
            0,
            new CampaignDate(190, 1, 1),
            Date);
        _ = new CharacterMarriageWorldState(
            Snapshot([Practice()], [], [], [], [], [valid]),
            characters,
            Calendar);

        AssertInvalid(
            Snapshot(
                [Practice()],
                [],
                [],
                [],
                [],
                [valid with { EarliestDate = new CampaignDate(189, 12, 30) }]),
            characters);
    }

    [Fact]
    public void EveryMarriageRecordCategoryAndConsentAffectTheChecksum()
    {
        CharacterWorldState characters = CreateCharacters(4);
        MarriagePracticeState practice = Practice(maxPrincipal: 2);
        MarriageProposalState legal = AcceptedProposal(
            "checksum_legal",
            Character(0),
            Character(1),
            basis: MarriageBasis.Political);
        MarriageProposalState political = AcceptedProposal(
            "checksum_betrothal",
            Character(2),
            Character(3),
            kind: MarriageProposalKind.PoliticalBetrothal,
            basis: MarriageBasis.Political,
            consent: MarriageConsentKind.PoliticalArrangement);
        MarriageUnionState union = Union("checksum_legal", legal);
        PoliticalBetrothalState betrothal = Betrothal("checksum_betrothal", political);
        RomanceRouteState route = Route("checksum_route", Character(0), Character(2));
        CharacterMarriageWorldSnapshot snapshot = Snapshot(
            [practice],
            [legal, political],
            [betrothal],
            [union],
            [route],
            []);
        _ = new CharacterMarriageWorldState(snapshot, characters, Calendar);
        SimulationChecksum baseline = Checksum(characters, snapshot);

        CharacterMarriageWorldSnapshot[] mutations =
        [
            snapshot with
            {
                Practices = [practice with { AllowsWidowRemarriage = false }],
            },
            snapshot with
            {
                Proposals =
                [
                    legal with { ConsentKind = MarriageConsentKind.PoliticalArrangement },
                    political,
                ],
                Unions =
                [
                    union with { ConsentKind = MarriageConsentKind.PoliticalArrangement },
                ],
            },
            snapshot with
            {
                Proposals =
                [
                    legal with
                    {
                        SourceCommandId = new EntityId("command:test/checksum_legal/changed"),
                    },
                    political,
                ],
            },
            snapshot with
            {
                Betrothals =
                [
                    betrothal with
                    {
                        BetrothalId = new EntityId("political_betrothal:test/checksum_changed"),
                    },
                ],
            },
            snapshot with
            {
                Unions =
                [
                    union with { UnionId = new EntityId("marriage_union:test/checksum_changed") },
                ],
            },
            snapshot with
            {
                RomanceRoutes = [route with { ProgressLevel = 2 }],
            },
        ];
        Assert.All(mutations, mutation =>
        {
            _ = new CharacterMarriageWorldState(mutation, characters, Calendar);
            Assert.NotEqual(baseline, Checksum(characters, mutation));
        });
    }

    [Fact]
    public void MarriageSnapshotChecksumAndSaveRoundTripRemainExact()
    {
        CharacterWorldState characters = CreateCharacters(2);
        MarriageProposalState proposal = AcceptedProposal("save", Character(0), Character(1));
        CharacterMarriageWorldSnapshot marriages = Snapshot(
            [Practice()],
            [proposal],
            [],
            [Union("save", proposal)],
            [],
            []);
        WorldSnapshot baseSnapshot = WorldState.Create(
            Date,
            20260715,
            [],
            GeographicWorldSnapshot.Empty,
            characters.CaptureSnapshot(),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty).CaptureSnapshot();
        WorldState world = WorldState.Restore(baseSnapshot with
        {
            Calendar = Calendar,
            CharacterMarriages = marriages,
        });
        CampaignSimulation simulation = new(world);
        SaveEnvelope expected = SaveEnvelope.Create(
            "0.1.0",
            [],
            simulation,
            DateTimeOffset.Parse(
                "2026-07-15T00:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture));
        string path = Path.Combine(Path.GetTempPath(), $"marriage-save-{Guid.NewGuid():N}.save.gz");
        try
        {
            new SaveStore().SaveAtomic(path, expected);
            SaveEnvelope loaded = new SaveStore().Load(path);

            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Equal(expected.Checksum, loaded.Checksum);
            Assert.Equal(
                Serialize(expected.Snapshot.CharacterMarriages),
                Serialize(loaded.Snapshot.CharacterMarriages));
            WorldState restored = WorldState.Restore(loaded.Snapshot);
            Assert.Single(restored.CharacterMarriages.Unions);
            Assert.Equal(expected.Checksum, SimulationChecksum.Compute(restored.CaptureSnapshot()).Value);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ThousandCharacterMarriageFixtureRecordsCountsChecksumAndRawTimings()
    {
        CharacterWorldState characters = CreateCharacters(1_000);
        MarriagePracticeState practice = Practice(maxPrincipal: 8);
        MarriageProposalState[] proposals = Enumerable.Range(0, 250)
            .Select(index => ActiveProposal(
                $"performance_{index:D3}",
                Character(index),
                Character(500 + index)))
            .ToArray();
        RomanceRouteState[] routes = Enumerable.Range(250, 250)
            .Select(index => Route(
                $"performance_{index:D3}",
                Character(index),
                Character(500 + index)))
            .ToArray();
        CharacterMarriageWorldSnapshot source = Snapshot(
            [practice],
            proposals,
            [],
            [],
            routes,
            []);

        Stopwatch construction = Stopwatch.StartNew();
        CharacterMarriageWorldState state = new(source, characters, Calendar);
        construction.Stop();
        Stopwatch queries = Stopwatch.StartNew();
        int queriedRecords = Enumerable.Range(0, 1_000).Sum(index =>
            state.GetProposalsInvolving(Character(index)).Count
            + state.GetRomanceRoutesInvolving(Character(index)).Count);
        queries.Stop();
        Stopwatch snapshotChecksum = Stopwatch.StartNew();
        CharacterMarriageWorldSnapshot snapshot = state.CaptureSnapshot();
        SimulationChecksum checksum = Checksum(characters, state);
        snapshotChecksum.Stop();

        Assert.Equal(1_000, characters.Profiles.Count);
        Assert.Equal(250, snapshot.Proposals.Count);
        Assert.Equal(250, snapshot.RomanceRoutes.Count);
        Assert.Equal(1_000, queriedRecords);
        Assert.False(string.IsNullOrWhiteSpace(checksum.Value));
        output.WriteLine(
            $"SP-04D0 raw fixture: characters=1000; proposals={snapshot.Proposals.Count}; "
            + $"romance_routes={snapshot.RomanceRoutes.Count}; queried_records={queriedRecords}; "
            + $"construction_ms={construction.Elapsed.TotalMilliseconds:F3}; "
            + $"queries_ms={queries.Elapsed.TotalMilliseconds:F3}; "
            + $"snapshot_checksum_ms={snapshotChecksum.Elapsed.TotalMilliseconds:F3}; "
            + $"checksum={checksum.Value}");
    }

    private static CharacterMarriageWorldState NewState(
        CharacterWorldState characters,
        params MarriagePracticeState[] practices) => new(
        Snapshot(practices, [], [], [], [], []),
        characters,
        Calendar);

    private static CharacterMarriageWorldSnapshot Snapshot(params MarriagePracticeState[] practices) =>
        Snapshot(practices, [], [], [], [], []);

    private static CharacterMarriageWorldSnapshot Snapshot(
        IReadOnlyList<MarriagePracticeState> practices,
        IReadOnlyList<MarriageProposalState> proposals,
        IReadOnlyList<PoliticalBetrothalState> betrothals,
        IReadOnlyList<MarriageUnionState> unions,
        IReadOnlyList<RomanceRouteState> romanceRoutes,
        IReadOnlyList<CharacterMarriageHistoryAggregate> history) => new(
        CharacterMarriageContractVersions.Snapshot,
        practices,
        proposals,
        betrothals,
        unions,
        romanceRoutes,
        history);

    private static MarriagePracticeState Practice(
        string suffix = "standard",
        int minLegal = 18,
        int minRomance = 18,
        int maxPrincipal = 1,
        int maxConcubinagePrincipal = 4,
        int maxConcubinagePartner = 1,
        bool allowsMinorBetrothal = true,
        bool allowsWidowRemarriage = true,
        MarriageProhibitedKinship flags = MarriageProhibitedKinship.DirectLine
            | MarriageProhibitedKinship.Siblings) => new(
        CharacterMarriageContractVersions.Practice,
        new EntityId($"marriage_practice:test/{suffix}"),
        minLegal,
        minRomance,
        maxPrincipal,
        maxConcubinagePrincipal,
        maxConcubinagePartner,
        allowsMinorBetrothal,
        allowsWidowRemarriage,
        flags);

    private static MarriageProposalState ActiveProposal(
        string suffix,
        EntityId proposer,
        EntityId recipient,
        MarriageProposalKind kind = MarriageProposalKind.LegalUnion,
        MarriageBasis basis = MarriageBasis.Political,
        MarriageUnionForm form = MarriageUnionForm.PrincipalSpouse,
        MarriageConsentKind consent = MarriageConsentKind.Voluntary,
        EntityId? principal = null,
        EntityId? practiceId = null) => new(
        CharacterMarriageContractVersions.State,
        new EntityId($"marriage_proposal:test/{suffix}"),
        kind,
        basis,
        form,
        consent,
        proposer,
        recipient,
        principal,
        practiceId ?? StandardPracticeId,
        Date.AddDays(-2),
        0,
        new EntityId($"command:test/{suffix}/create"),
        MarriageProposalStatus.Active,
        null,
        null,
        null);

    private static MarriageProposalState AcceptedProposal(
        string suffix,
        EntityId proposer,
        EntityId recipient,
        MarriageProposalKind kind = MarriageProposalKind.LegalUnion,
        MarriageBasis basis = MarriageBasis.Political,
        MarriageUnionForm form = MarriageUnionForm.PrincipalSpouse,
        MarriageConsentKind consent = MarriageConsentKind.Voluntary,
        EntityId? principal = null,
        EntityId? practiceId = null) => ActiveProposal(
            suffix,
            proposer,
            recipient,
            kind,
            basis,
            form,
            consent,
            principal,
            practiceId) with
        {
            Status = MarriageProposalStatus.Accepted,
            ResolutionDate = Date.AddDays(-1),
            ResolutionTurnIndex = 1,
            ResolutionCommandId = new EntityId($"command:test/{suffix}/accept"),
        };

    private static MarriageProposalState TerminalProposal(
        string suffix,
        EntityId proposer,
        EntityId recipient) => ActiveProposal(suffix, proposer, recipient) with
        {
            Status = MarriageProposalStatus.Refused,
            ResolutionDate = Date.AddDays(-1),
            ResolutionTurnIndex = 1,
            ResolutionCommandId = new EntityId($"command:test/{suffix}/refuse"),
        };

    private static PoliticalBetrothalState Betrothal(
        string suffix,
        MarriageProposalState proposal) => new(
        CharacterMarriageContractVersions.State,
        new EntityId($"political_betrothal:test/{suffix}"),
        Min(proposal.ProposerCharacterId, proposal.RecipientCharacterId),
        Max(proposal.ProposerCharacterId, proposal.RecipientCharacterId),
        proposal.ProposedForm,
        proposal.ConcubinagePrincipalCharacterId,
        proposal.PracticeId,
        proposal.ProposalId,
        proposal.ResolutionDate!.Value,
        proposal.ResolutionTurnIndex!.Value,
        PoliticalBetrothalStatus.Active,
        null,
        null,
        null,
        null);

    private static MarriageUnionState Union(
        string suffix,
        MarriageProposalState proposal) => new(
        CharacterMarriageContractVersions.State,
        new EntityId($"marriage_union:test/{suffix}"),
        Min(proposal.ProposerCharacterId, proposal.RecipientCharacterId),
        Max(proposal.ProposerCharacterId, proposal.RecipientCharacterId),
        proposal.ProposedForm,
        proposal.ConcubinagePrincipalCharacterId,
        proposal.Basis,
        proposal.ConsentKind,
        proposal.PracticeId,
        proposal.ProposalId,
        proposal.ResolutionDate!.Value,
        proposal.ResolutionTurnIndex!.Value,
        MarriageUnionStatus.Active,
        null,
        null,
        null,
        null);

    private static RomanceRouteState Route(
        string suffix,
        EntityId first,
        EntityId second) => new(
        CharacterMarriageContractVersions.State,
        new EntityId($"romance_route:test/{suffix}"),
        Min(first, second),
        Max(first, second),
        StandardPracticeId,
        1,
        Date.AddDays(-2),
        0,
        new EntityId($"command:test/{suffix}/romance"),
        RomanceRouteStatus.Active,
        null,
        null,
        null);

    private static RomanceRouteState EndedRoute(
        string suffix,
        EntityId first,
        EntityId second) => Route(suffix, first, second) with
        {
            Status = RomanceRouteStatus.Ended,
            ResolutionDate = Date.AddDays(-1),
            ResolutionTurnIndex = 1,
            ResolutionCommandId = new EntityId($"command:test/{suffix}/romance_end"),
        };

    private static RomanceRouteState VersionTwoRoute(
        string suffix,
        EntityId first,
        EntityId second,
        EntityId practiceId)
    {
        CampaignDate invitationDate = Date.AddDays(-2);
        EntityId invitationCommand = new($"command:test/{suffix}/invitation");
        EntityId invitationId = CharacterMarriageIds.DeriveRomanceInvitationId(
            invitationDate,
            invitationCommand);
        EntityId acceptanceCommand = new($"command:test/{suffix}/acceptance");
        return new RomanceRouteState(
            CharacterMarriageContractVersions.RomanceRouteState,
            CharacterMarriageIds.DeriveRomanceRouteId(invitationId, acceptanceCommand),
            Min(first, second),
            Max(first, second),
            practiceId,
            2,
            Date.AddDays(-1),
            1,
            acceptanceCommand,
            RomanceRouteStatus.Active,
            null,
            null,
            null,
            invitationId,
            first,
            invitationDate,
            0,
            invitationCommand,
            Date,
            2,
            new EntityId($"command:test/{suffix}/advance"));
    }

    private static MarriageEligibilityRequest Eligibility(
        MarriageEligibilityCategory category,
        EntityId first,
        EntityId second,
        MarriageUnionForm? form = null,
        EntityId? principal = null,
        EntityId? practiceId = null) => new(
        CharacterMarriageContractVersions.Eligibility,
        category,
        first,
        second,
        practiceId ?? StandardPracticeId,
        form,
        principal);

    private static MarriageEligibilityResult EvaluateLegal(
        CharacterMarriageWorldState state,
        EntityId first,
        EntityId second) => state.EvaluateEligibility(
        Eligibility(
            MarriageEligibilityCategory.VoluntaryLegalUnion,
            first,
            second,
            MarriageUnionForm.PrincipalSpouse),
        Date);

    private static void AssertIssue(
        MarriageEligibilityResult result,
        MarriageEligibilityReason reason)
    {
        Assert.False(result.IsEligible);
        Assert.Contains(result.Issues, issue => issue.Reason == reason);
    }

    private static CharacterConditionState Custody(CharacterCustodyStatus status) => new(
        CharacterVitalStatus.Alive,
        CharacterHealthStatus.Healthy,
        IsIncapacitated: false,
        status,
        Character(2));

    private static CharacterWorldState CreateCharacters(
        int count,
        IReadOnlyDictionary<EntityId, CharacterConditionState>? conditions = null,
        IReadOnlyDictionary<EntityId, CampaignDate>? birthDates = null,
        IReadOnlyDictionary<EntityId, IReadOnlyList<EntityId>>? parents = null,
        IReadOnlyDictionary<EntityId, EntityId?>? cultures = null,
        bool separateHouseholds = false)
    {
        CharacterDefinition[] definitions = Enumerable.Range(0, count)
            .Select(index =>
            {
                EntityId id = Character(index);
                EntityId nameKey = new($"loc:marriage/character_{index:D4}");
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
                    cultures is not null && cultures.TryGetValue(id, out EntityId? culture)
                        ? culture
                        : null,
                    null,
                    []);
            })
            .ToArray();
        CharacterState[] states = definitions
            .Select(definition =>
            {
                IReadOnlyList<EntityId> parentIds = parents is not null
                    && parents.TryGetValue(definition.Id, out IReadOnlyList<EntityId>? configured)
                    ? configured.Order().ToArray()
                    : [];
                return new CharacterState(
                    CharacterContractVersions.State,
                    definition.Id,
                    parentIds,
                    parentIds.Select(parent => new CharacterParentLink(
                            parent,
                            ParentChildLinkKind.Biological))
                        .ToArray(),
                    conditions is not null
                        && conditions.TryGetValue(
                            definition.Id,
                            out CharacterConditionState? condition)
                            ? condition
                            : CharacterConditionState.Default);
            })
            .ToArray();
        HouseholdDefinition[] householdDefinitions = separateHouseholds
            ? definitions.Select((_, index) => new HouseholdDefinition(
                    CharacterContractVersions.Definition,
                    new EntityId($"household:marriage/h{index:D4}"),
                    new EntityId($"loc:marriage/household_{index:D4}")))
                .ToArray()
            : [];
        HouseholdState[] householdStates = separateHouseholds
            ? definitions.Select((definition, index) => new HouseholdState(
                    CharacterContractVersions.State,
                    householdDefinitions[index].Id,
                    definition.Id,
                    [definition.Id]))
                .ToArray()
            : [];
        return new CharacterWorldState(
            new CharacterWorldSnapshot(
                CharacterContractVersions.Snapshot,
                [],
                definitions,
                [],
                householdDefinitions,
                states,
                [],
                householdStates),
            Date);
    }

    private static SimulationChecksum Checksum(
        CharacterWorldState characters,
        CharacterMarriageWorldState marriages) =>
        Checksum(characters, marriages.CaptureSnapshot());

    private static SimulationChecksum Checksum(
        CharacterWorldState characters,
        CharacterMarriageWorldSnapshot marriages)
    {
        WorldSnapshot snapshot = WorldState.Create(
            Date,
            20260715,
            [],
            GeographicWorldSnapshot.Empty,
            characters.CaptureSnapshot(),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty).CaptureSnapshot() with
        {
            Calendar = Calendar,
            CharacterMarriages = marriages,
        };
        return SimulationChecksum.Compute(snapshot);
    }

    private static void AssertInvalid(
        CharacterMarriageWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters) =>
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterMarriageWorldState(snapshot, characters, Calendar));

    private static EntityId Character(int index) =>
        new($"character:marriage/c{index:D4}");

    private static EntityId Min(EntityId first, EntityId second) =>
        first.CompareTo(second) < 0 ? first : second;

    private static EntityId Max(EntityId first, EntityId second) =>
        first.CompareTo(second) < 0 ? second : first;

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());
}
