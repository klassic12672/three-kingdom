using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterEducationWorldStateTests
{
    private static readonly CampaignDate Date = new(200, 6, 1);
    private static readonly EntityId Ward = new("character:test/education_ward");
    private static readonly EntityId Teacher = new("character:test/education_teacher");
    private static readonly EntityId OtherTeacher = new("character:test/education_other_teacher");
    private static readonly EntityId AbilityA = new("ability:test/education_a");
    private static readonly EntityId AbilityB = new("ability:test/education_b");
    private static readonly EntityId Trait = new("trait:test/education_not_ability");
    private static readonly EntityId GuardianshipId = new("guardianship:test/education_primary");

    [Fact]
    public void ExactActivePrimaryGuardianEducationProducesAnImmutableCandidateAndEffectiveAbility()
    {
        CharacterWorldState world = CreateWorld();
        string before = Serialize(world.CaptureSnapshot());
        EntityId commandId = new("command:test/education_success");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);

        CharacterEducationMutationPlan plan = world.PreparePrimaryGuardianEducation(
            new CompletePrimaryGuardianEducationAction(Ward, GuardianshipId, AbilityA),
            ActiveGuardianship(),
            Date,
            10,
            commandId,
            eventId);

        Assert.Equal(before, Serialize(world.CaptureSnapshot()));
        CharacterEducationAttainment attainment = plan.Attainment;
        Assert.Equal(CharacterEducationContractVersions.Attainment, attainment.ContractVersion);
        Assert.Equal(
            CharacterEducationIds.DeriveAttainmentId(eventId, Ward, Teacher, AbilityA),
            attainment.AttainmentId);
        Assert.Equal(Ward, attainment.WardCharacterId);
        Assert.Equal(Teacher, attainment.TeacherCharacterId);
        Assert.Equal(GuardianshipId, attainment.PrimaryGuardianshipId);
        Assert.Equal(AbilityA, attainment.AbilityId);
        Assert.Equal(Date, attainment.ResolutionDate);
        Assert.Equal(10, attainment.ResolutionTurnIndex);
        Assert.Equal(commandId, attainment.SourceCommandId);
        Assert.Equal(eventId, attainment.SourceEventId);

        Assert.True(plan.CharacterPlan.Candidate.TryGetCharacterProfile(
            Ward,
            out AuthoritativeCharacterProfile? candidateWard));
        Assert.Equal([AbilityA], candidateWard.AbilityIds);
        Assert.Equal(attainment, Assert.Single(candidateWard.EducationAttainments));
        CharacterWorldSnapshot candidateSnapshot = plan.CharacterPlan.Candidate.CaptureSnapshot();
        CharacterDefinition candidateDefinition = candidateSnapshot.CharacterDefinitions
            .Single(item => item.Id == Ward);
        CharacterState candidateState = candidateSnapshot.CharacterStates
            .Single(item => item.CharacterId == Ward);
        Assert.Empty(candidateDefinition.AbilityIds);
        Assert.Equal(attainment, Assert.Single(candidateState.EducationAttainments!));

        world.ApplyPrepared(plan.CharacterPlan);

        Assert.True(world.TryGetCharacterProfile(Ward, out AuthoritativeCharacterProfile? appliedWard));
        Assert.Equal([AbilityA], appliedWard.AbilityIds);
        Assert.Equal(attainment, Assert.Single(appliedWard.EducationAttainments));
    }

    [Fact]
    public void EffectiveAbilitiesAreTheCanonicalUnionOfBaselineAndAttainedAbilities()
    {
        CharacterEducationAttainment attainment = Attainment(
            AbilityA,
            Teacher,
            "canonical-union");
        CharacterWorldState world = CreateWorld(
            wardBaselineAbilities: [AbilityB],
            wardAttainments: [attainment]);

        Assert.True(world.TryGetCharacterProfile(Ward, out AuthoritativeCharacterProfile? ward));
        Assert.Equal(new[] { AbilityA, AbilityB }.Order(), ward.AbilityIds);
        Assert.Equal(attainment, Assert.Single(ward.EducationAttainments));
        Assert.Equal(
            [AbilityB],
            world.CaptureSnapshot().CharacterDefinitions
                .Single(item => item.Id == Ward)
                .AbilityIds);
    }

    [Fact]
    public void RuntimeEligibilityRequiresExactActiveGuardianshipAndCurrentParticipants()
    {
        CompletePrimaryGuardianEducationAction action = new(Ward, GuardianshipId, AbilityA);
        CharacterGuardianshipState active = ActiveGuardianship();

        AssertPreparationInvalid(CreateWorld(), action, active with
        {
            GuardianshipId = new EntityId("guardianship:test/stale"),
        });
        AssertPreparationInvalid(CreateWorld(), action, active with
        {
            WardCharacterId = OtherTeacher,
        });
        AssertPreparationInvalid(CreateWorld(), action, active with
        {
            Status = CharacterGuardianshipStatus.Ended,
            EndDate = Date,
            EndTurnIndex = 9,
            EndSourceCommandId = new EntityId("command:test/ended"),
            EndSourceEventId = new EntityId("event:test/ended"),
            EndReason = CharacterGuardianshipEndReason.Revoked,
        });
        AssertPreparationInvalid(
            CreateWorld(wardCondition: CharacterConditionState.Default with
            {
                VitalStatus = CharacterVitalStatus.Dead,
                HealthStatus = CharacterHealthStatus.Critical,
                IsIncapacitated = true,
            }),
            action,
            active);
        AssertPreparationInvalid(
            CreateWorld(wardCondition: CharacterConditionState.Default with
            {
                IsIncapacitated = true,
            }),
            action,
            active);
        AssertPreparationInvalid(
            CreateWorld(wardBirthDate: new CampaignDate(182, 6, 1)),
            action,
            active);
        AssertPreparationInvalid(
            CreateWorld(teacherCondition: CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = OtherTeacher,
            }),
            action,
            active);
        AssertPreparationInvalid(
            CreateWorld(teacherCondition: CharacterConditionState.Default with
            {
                VitalStatus = CharacterVitalStatus.Dead,
                HealthStatus = CharacterHealthStatus.Critical,
                IsIncapacitated = true,
            }),
            action,
            active);
        AssertPreparationInvalid(
            CreateWorld(teacherCondition: CharacterConditionState.Default with
            {
                IsIncapacitated = true,
            }),
            action,
            active);
        AssertPreparationInvalid(
            CreateWorld(teacherBirthDate: new CampaignDate(190, 1, 1)),
            action,
            active);
        AssertPreparationInvalid(
            CreateWorld(),
            action,
            active with { GuardianCharacterId = Ward });
    }

    [Fact]
    public void WardCustodyDoesNotBlockEducation()
    {
        CharacterWorldState world = CreateWorld(
            wardCondition: CharacterConditionState.Default with
            {
                CustodyStatus = CharacterCustodyStatus.Captive,
                CustodianId = OtherTeacher,
            });

        CharacterEducationMutationPlan plan = Prepare(world, AbilityA, "ward-custody");

        Assert.Equal(AbilityA, plan.Attainment.AbilityId);
    }

    [Fact]
    public void EducationHasNoMinimumWardAgeBeyondBeingBorn()
    {
        CharacterWorldState world = CreateWorld(wardBirthDate: Date);
        CharacterGuardianshipState guardianship = ActiveGuardianship() with
        {
            EstablishedDate = Date,
            EstablishedTurnIndex = 10,
        };
        EntityId commandId = new("command:test/education_newborn_ward");

        CharacterEducationMutationPlan plan = world.PreparePrimaryGuardianEducation(
            new CompletePrimaryGuardianEducationAction(Ward, GuardianshipId, AbilityA),
            guardianship,
            Date,
            10,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(Date, commandId));

        Assert.Equal(AbilityA, plan.Attainment.AbilityId);
    }

    [Fact]
    public void RuntimeRequiresListedTargetAndTeacherBaselineAndRejectsWardCurrentAbility()
    {
        AssertPreparationInvalid(
            CreateWorld(),
            new CompletePrimaryGuardianEducationAction(Ward, GuardianshipId, Trait),
            ActiveGuardianship());
        AssertPreparationInvalid(
            CreateWorld(teacherBaselineAbilities: [AbilityB]),
            new CompletePrimaryGuardianEducationAction(Ward, GuardianshipId, AbilityA),
            ActiveGuardianship());
        AssertPreparationInvalid(
            CreateWorld(wardBaselineAbilities: [AbilityA]),
            new CompletePrimaryGuardianEducationAction(Ward, GuardianshipId, AbilityA),
            ActiveGuardianship());
        AssertPreparationInvalid(
            CreateWorld(wardAttainments: [Attainment(AbilityA, Teacher, "already-attained")]),
            new CompletePrimaryGuardianEducationAction(Ward, GuardianshipId, AbilityA),
            ActiveGuardianship());

        CharacterEducationAttainment teacherAcquiredOnly = AttainmentFor(
            Teacher,
            OtherTeacher,
            AbilityA,
            "teacher-acquired-only",
            new CampaignDate(190, 1, 1));
        CharacterWorldState acquiredOnlyTeacher = CreateWorld(
            teacherBirthDate: new CampaignDate(180, 1, 1),
            teacherBaselineAbilities: [AbilityB],
            teacherAttainments: [teacherAcquiredOnly]);
        Assert.True(acquiredOnlyTeacher.TryGetCharacterProfile(
            Teacher,
            out AuthoritativeCharacterProfile? acquiredProfile));
        Assert.Contains(AbilityA, acquiredProfile.AbilityIds);
        AssertPreparationInvalid(
            acquiredOnlyTeacher,
            new CompletePrimaryGuardianEducationAction(Ward, GuardianshipId, AbilityA),
            ActiveGuardianship());
    }

    [Fact]
    public void SnapshotRejectsMalformedAttainmentEvidenceAndReferences()
    {
        CharacterWorldSnapshot source = Snapshot(
            wardAttainments: [Attainment(AbilityA, Teacher, "snapshot-valid")]);
        CharacterEducationAttainment valid = source.CharacterStates
            .Single(item => item.CharacterId == Ward)
            .EducationAttainments!
            .Single();

        AssertInvalid(WithWardAttainments(source, null));
        AssertInvalid(WithWardAttainments(source, [null!]));
        AssertInvalid(WithWardAttainments(source, [valid with { ContractVersion = 2 }]));
        AssertInvalid(WithWardAttainments(source, [valid with { TeacherCharacterId = default }]));
        AssertInvalid(WithWardAttainments(source, [valid with { PrimaryGuardianshipId = default }]));
        AssertInvalid(WithWardAttainments(source, [valid with { ResolutionDate = default }]));
        AssertInvalid(WithWardAttainments(source, [valid with { ResolutionTurnIndex = -1 }]));
        AssertInvalid(WithWardAttainments(source, [valid with
        {
            SourceEventId = new EntityId("event:test/forged"),
        }]));
        AssertInvalid(WithWardAttainments(source, [valid with
        {
            AttainmentId = new EntityId("character_education_attainment:test/forged"),
        }]));
        AssertInvalid(WithWardAttainments(source, [valid with
        {
            WardCharacterId = OtherTeacher,
        }]));
        AssertInvalid(WithWardAttainments(source, [Reissue(
            valid,
            teacherCharacterId: new EntityId("character:test/missing"))]));
        AssertInvalid(WithWardAttainments(source, [Reissue(valid, abilityId: Trait)]));
    }

    [Fact]
    public void SnapshotRejectsHistoricalAgeBaselineAndUniquenessViolations()
    {
        CharacterEducationAttainment first = Attainment(AbilityA, Teacher, "unique-first");
        CharacterEducationAttainment secondSameAbility = Attainment(
            AbilityA,
            Teacher,
            "unique-second");
        CharacterEducationAttainment abilityB = Attainment(
            AbilityB,
            Teacher,
            "canonical-second");
        CharacterEducationAttainment[] nonCanonical = [first, abilityB];
        Array.Reverse(nonCanonical);
        if (nonCanonical[0].AttainmentId.CompareTo(nonCanonical[1].AttainmentId) < 0)
        {
            Array.Reverse(nonCanonical);
        }

        AssertInvalid(Snapshot(
            wardBirthDate: new CampaignDate(180, 1, 1),
            wardAttainments: [first]));
        AssertInvalid(Snapshot(
            wardBirthDate: Date.AddDays(1),
            wardAttainments: [first]));
        AssertInvalid(Snapshot(
            teacherBirthDate: Date.AddDays(1),
            wardAttainments: [first]));
        AssertInvalid(Snapshot(
            teacherBirthDate: new CampaignDate(190, 1, 1),
            wardAttainments: [first]));
        AssertInvalid(Snapshot(
            wardBaselineAbilities: [AbilityA],
            wardAttainments: [first]));
        AssertInvalid(Snapshot(
            teacherBaselineAbilities: [AbilityB],
            wardAttainments: [first]));
        AssertInvalid(Snapshot(wardAttainments: new[] { first, secondSameAbility }
            .OrderBy(item => item.AttainmentId)
            .ToArray()));
        AssertInvalid(Snapshot(wardAttainments: [first, first]));
        AssertInvalid(Snapshot(wardAttainments: nonCanonical));
    }

    [Fact]
    public void AttainmentLimitIsEnforcedBySnapshotAndPreparationWithoutMutation()
    {
        CharacterEducationAttainment[] tooMany = Enumerable.Range(
                0,
                CharacterEducationLimits.MaximumAttainmentsPerCharacter + 1)
            .Select(index => Attainment(
                new EntityId($"ability:test/education_limit_{index:D2}"),
                Teacher,
                $"limit-{index:D2}"))
            .OrderBy(item => item.AttainmentId)
            .ToArray();
        AssertInvalid(Snapshot(
            additionalAbilities: tooMany.Select(item => item.AbilityId).ToArray(),
            teacherBaselineAbilities: tooMany.Select(item => item.AbilityId).ToArray(),
            wardAttainments: tooMany));

        CharacterEducationAttainment[] atLimit = Enumerable.Range(
                0,
                CharacterEducationLimits.MaximumAttainmentsPerCharacter)
            .Select(index => Attainment(
                new EntityId($"ability:test/education_capacity_{index:D2}"),
                Teacher,
                $"capacity-{index:D2}"))
            .OrderBy(item => item.AttainmentId)
            .ToArray();
        EntityId[] listed = atLimit.Select(item => item.AbilityId)
            .Append(AbilityA)
            .Distinct()
            .Order()
            .ToArray();
        CharacterWorldState full = CreateWorld(
            additionalAbilities: listed,
            teacherBaselineAbilities: listed,
            wardAttainments: atLimit);
        string before = Serialize(full.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => Prepare(
            full,
            AbilityA,
            "capacity-rejected"));
        Assert.Equal(before, Serialize(full.CaptureSnapshot()));
    }

    [Fact]
    public void SnapshotCaptureAndQueriesDefensivelyCopyAttainmentsAndEffectiveAbilities()
    {
        CharacterEducationAttainment original = Attainment(
            AbilityA,
            Teacher,
            "defensive-copy");
        CharacterEducationAttainment[] mutableAttainments = [original];
        CharacterWorldSnapshot source = Snapshot(wardAttainments: mutableAttainments);
        CharacterWorldState world = new(source, Date);

        mutableAttainments[0] = Attainment(AbilityB, Teacher, "mutated-source");
        CharacterWorldSnapshot capture = world.CaptureSnapshot();
        CharacterState capturedWard = capture.CharacterStates
            .Single(item => item.CharacterId == Ward);
        ((CharacterEducationAttainment[])capturedWard.EducationAttainments!)[0] =
            Attainment(AbilityB, Teacher, "mutated-capture");
        Assert.True(world.TryGetCharacterProfile(Ward, out AuthoritativeCharacterProfile? profile));
        ((EntityId[])profile.AbilityIds)[0] = AbilityB;
        ((CharacterEducationAttainment[])profile.EducationAttainments)[0] =
            Attainment(AbilityB, Teacher, "mutated-profile");

        Assert.True(world.TryGetCharacterProfile(Ward, out AuthoritativeCharacterProfile? fresh));
        Assert.Equal([AbilityA], fresh.AbilityIds);
        Assert.Equal(original, Assert.Single(fresh.EducationAttainments));
        Assert.Equal(
            original,
            Assert.Single(world.CaptureSnapshot().CharacterStates
                .Single(item => item.CharacterId == Ward)
                .EducationAttainments!));
    }

    private static CharacterEducationMutationPlan Prepare(
        CharacterWorldState world,
        EntityId abilityId,
        string suffix)
    {
        EntityId commandId = new($"command:test/{suffix}");
        return world.PreparePrimaryGuardianEducation(
            new CompletePrimaryGuardianEducationAction(Ward, GuardianshipId, abilityId),
            ActiveGuardianship(),
            Date,
            10,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(Date, commandId));
    }

    private static void AssertPreparationInvalid(
        CharacterWorldState world,
        CompletePrimaryGuardianEducationAction action,
        CharacterGuardianshipState guardianship)
    {
        string before = Serialize(world.CaptureSnapshot());
        EntityId commandId = new("command:test/education_invalid");
        Assert.Throws<SimulationValidationException>(() => world.PreparePrimaryGuardianEducation(
            action,
            guardianship,
            Date,
            10,
            commandId,
            CharacterFamilyIds.DeriveActionEventId(Date, commandId)));
        Assert.Equal(before, Serialize(world.CaptureSnapshot()));
    }

    private static CharacterGuardianshipState ActiveGuardianship() => new(
        CharacterGuardianshipContractVersions.State,
        GuardianshipId,
        Ward,
        Teacher,
        Date.AddDays(-30),
        3,
        new EntityId("command:test/guardianship_established"),
        new EntityId("event:test/guardianship_established"),
        CharacterGuardianshipStatus.Active,
        null,
        null,
        null,
        null,
        null);

    private static CharacterEducationAttainment Attainment(
        EntityId abilityId,
        EntityId teacherCharacterId,
        string suffix) => AttainmentFor(
        Ward,
        teacherCharacterId,
        abilityId,
        suffix,
        Date);

    private static CharacterEducationAttainment AttainmentFor(
        EntityId wardCharacterId,
        EntityId teacherCharacterId,
        EntityId abilityId,
        string suffix,
        CampaignDate resolutionDate)
    {
        EntityId commandId = new($"command:test/education-{suffix}");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(resolutionDate, commandId);
        return new CharacterEducationAttainment(
            CharacterEducationContractVersions.Attainment,
            CharacterEducationIds.DeriveAttainmentId(
                eventId,
                wardCharacterId,
                teacherCharacterId,
                abilityId),
            wardCharacterId,
            teacherCharacterId,
            GuardianshipId,
            abilityId,
            resolutionDate,
            8,
            commandId,
            eventId);
    }

    private static CharacterEducationAttainment Reissue(
        CharacterEducationAttainment source,
        EntityId? teacherCharacterId = null,
        EntityId? abilityId = null)
    {
        EntityId teacher = teacherCharacterId ?? source.TeacherCharacterId;
        EntityId ability = abilityId ?? source.AbilityId;
        return source with
        {
            AttainmentId = CharacterEducationIds.DeriveAttainmentId(
                source.SourceEventId,
                source.WardCharacterId,
                teacher,
                ability),
            TeacherCharacterId = teacher,
            AbilityId = ability,
        };
    }

    private static CharacterWorldState CreateWorld(
        CampaignDate? wardBirthDate = null,
        CampaignDate? teacherBirthDate = null,
        CharacterConditionState? wardCondition = null,
        CharacterConditionState? teacherCondition = null,
        IReadOnlyList<EntityId>? wardBaselineAbilities = null,
        IReadOnlyList<EntityId>? teacherBaselineAbilities = null,
        IReadOnlyList<EntityId>? additionalAbilities = null,
        IReadOnlyList<CharacterEducationAttainment>? wardAttainments = null,
        IReadOnlyList<CharacterEducationAttainment>? teacherAttainments = null) => new(
        Snapshot(
            wardBirthDate,
            teacherBirthDate,
            wardCondition,
            teacherCondition,
            wardBaselineAbilities,
            teacherBaselineAbilities,
            additionalAbilities,
            wardAttainments,
            teacherAttainments),
        Date);

    private static CharacterWorldSnapshot Snapshot(
        CampaignDate? wardBirthDate = null,
        CampaignDate? teacherBirthDate = null,
        CharacterConditionState? wardCondition = null,
        CharacterConditionState? teacherCondition = null,
        IReadOnlyList<EntityId>? wardBaselineAbilities = null,
        IReadOnlyList<EntityId>? teacherBaselineAbilities = null,
        IReadOnlyList<EntityId>? additionalAbilities = null,
        IReadOnlyList<CharacterEducationAttainment>? wardAttainments = null,
        IReadOnlyList<CharacterEducationAttainment>? teacherAttainments = null)
    {
        EntityId[] abilityIds = new[] { AbilityA, AbilityB }
            .Concat(additionalAbilities ?? [])
            .Distinct()
            .Order()
            .ToArray();
        CharacterIdentityDefinition[] identities = abilityIds
            .Select(id => new CharacterIdentityDefinition(
                CharacterContractVersions.Definition,
                id,
                CharacterIdentityKind.Ability,
                new EntityId($"loc:test/{id.Value.Replace(':', '/')}")))
            .Append(new CharacterIdentityDefinition(
                CharacterContractVersions.Definition,
                Trait,
                CharacterIdentityKind.Trait,
                new EntityId("loc:test/education_not_ability")))
            .OrderBy(item => item.Id)
            .ToArray();
        CharacterDefinition[] definitions =
        [
            Definition(
                Ward,
                wardBirthDate ?? new CampaignDate(190, 6, 2),
                wardBaselineAbilities ?? []),
            Definition(
                Teacher,
                teacherBirthDate ?? new CampaignDate(160, 1, 1),
                teacherBaselineAbilities ?? abilityIds),
            Definition(
                OtherTeacher,
                new CampaignDate(158, 1, 1),
                abilityIds),
        ];
        CharacterState[] states =
        [
            State(Ward, wardCondition, wardAttainments ?? []),
            State(Teacher, teacherCondition, teacherAttainments ?? []),
            State(OtherTeacher, null, []),
        ];
        return new CharacterWorldSnapshot(
            CharacterContractVersions.Snapshot,
            identities,
            definitions.OrderBy(item => item.Id).ToArray(),
            [],
            [],
            states.OrderBy(item => item.CharacterId).ToArray(),
            [],
            []);
    }

    private static CharacterDefinition Definition(
        EntityId id,
        CampaignDate birthDate,
        IReadOnlyList<EntityId> abilities)
    {
        EntityId nameKey = new($"loc:test/{id.Value.Replace(':', '/')}");
        return new CharacterDefinition(
            CharacterContractVersions.Definition,
            id,
            nameKey,
            birthDate,
            abilities.Order().ToArray(),
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

    private static CharacterState State(
        EntityId id,
        CharacterConditionState? condition,
        IReadOnlyList<CharacterEducationAttainment> attainments) => new(
        CharacterContractVersions.State,
        id,
        [],
        [],
        condition ?? CharacterConditionState.Default,
        attainments);

    private static CharacterWorldSnapshot WithWardAttainments(
        CharacterWorldSnapshot source,
        IReadOnlyList<CharacterEducationAttainment>? attainments) => source with
        {
            CharacterStates = source.CharacterStates.Select(state =>
                state.CharacterId == Ward
                    ? state with { EducationAttainments = attainments }
                    : state).ToArray(),
        };

    private static void AssertInvalid(CharacterWorldSnapshot snapshot) =>
        Assert.Throws<SimulationValidationException>(() => new CharacterWorldState(snapshot, Date));

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());
}
