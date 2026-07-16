using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Simulation.Core;

namespace Game.Application;

public sealed record CharacterBattleSetupContribution(
    EntityId CharacterId,
    IReadOnlyList<EntityId> AbilityIds,
    IReadOnlyList<EntityId> AptitudeIds,
    IReadOnlyList<EntityId> TraitIds,
    IReadOnlyList<EntityId> FlawIds,
    CharacterHealthStatus HealthStatus,
    bool IsIncapacitated,
    IReadOnlyDictionary<EntityId, RelationshipDimensions> DirectionalRelationshipModifiers);

public sealed record CharacterBattleResultContribution(
    EntityId CharacterId,
    CharacterHealthStatus? ResultingWoundHealthStatus,
    bool ResultingWoundIncapacitated,
    EntityId? CaptorCharacterId,
    bool ReleasesCustody,
    ResolveCharacterSuccessionDeathAction? DeathAction,
    IReadOnlyList<RelationshipActionCommandPayload> SharedMemories);

public sealed class CharacterBattleBoundaryAdapter
{
    public const int MaximumParticipants = 64;
    public const int MaximumResultContributions = 64;

    private readonly IWorldQuery world;

    public CharacterBattleBoundaryAdapter(IWorldQuery world)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public IReadOnlyList<CharacterBattleSetupContribution> CreateSetupContributions(
        IReadOnlyList<EntityId> participantCharacterIds)
    {
        EntityId[] participants = ValidateCanonicalCharacters(
            participantCharacterIds,
            MaximumParticipants,
            "Battle participants");
        CharacterBattleSetupContribution[] result = participants
            .Select(characterId =>
            {
                _ = world.Characters.TryGetCharacterProfile(
                    characterId,
                    out AuthoritativeCharacterProfile? profile);
                SortedDictionary<EntityId, RelationshipDimensions> modifiers = [];
                foreach (EntityId targetId in participants.Where(
                             item => item != characterId))
                {
                    modifiers.Add(
                        targetId,
                        GetDirectionalRelationshipDimensions(
                            characterId,
                            targetId));
                }

                return new CharacterBattleSetupContribution(
                    characterId,
                    ReadOnly(profile!.AbilityIds.Order().ToArray()),
                    ReadOnly(profile.AptitudeIds.Order().ToArray()),
                    ReadOnly(profile.TraitIds.Order().ToArray()),
                    ReadOnly(profile.FlawIds.Order().ToArray()),
                    profile.Condition.HealthStatus,
                    profile.Condition.IsIncapacitated,
                    new ReadOnlyDictionary<EntityId, RelationshipDimensions>(
                        modifiers));
            })
            .ToArray();
        return ReadOnly(result);
    }

    public IReadOnlyList<CampaignCommand> CreateResultCommands(
        EntityId battleResultId,
        CampaignDate resolutionDate,
        IReadOnlyList<CharacterBattleResultContribution> contributions)
    {
        if (!battleResultId.IsValid || !resolutionDate.IsValid)
        {
            throw new ArgumentException(
                "Battle result identity and resolution date must be valid.");
        }

        if (contributions is null
            || contributions.Count > MaximumResultContributions
            || contributions.Any(item => item is null))
        {
            throw new ArgumentException(
                $"Battle results retain at most {MaximumResultContributions} non-null contributions.");
        }

        CharacterBattleResultContribution[] canonical = contributions
            .OrderBy(item => item.CharacterId)
            .ToArray();
        if (canonical.Any(item => !item.CharacterId.IsValid)
            || canonical.Select(item => item.CharacterId).Distinct().Count()
                != canonical.Length)
        {
            throw new ArgumentException(
                "Battle result character IDs must be valid and unique.");
        }

        int deathCount = canonical.Count(item => item.DeathAction is not null);
        bool hasOtherConditionOutcome = canonical.Any(item =>
            item.ResultingWoundHealthStatus is not null
            || item.CaptorCharacterId is not null
            || item.ReleasesCustody);
        if (deathCount > 1
            || deathCount == 1 && hasOtherConditionOutcome)
        {
            throw new ArgumentException(
                "An exact succession death must be the only condition outcome in a result batch. Resolve wounds and custody outcomes first, then recompute and submit one exact death.");
        }

        List<CampaignCommand> commands = [];
        foreach (CharacterBattleResultContribution contribution in canonical)
        {
            if (!world.Characters.TryGetCharacterProfile(
                    contribution.CharacterId,
                    out AuthoritativeCharacterProfile? profile)
                || profile.Condition.VitalStatus != CharacterVitalStatus.Alive)
            {
                throw new ArgumentException(
                    $"Battle result character '{contribution.CharacterId}' must be living and known.");
            }

            CharacterConditionState projected = profile.Condition with { };
            RelationshipActionCommandPayload[] memories = CanonicalizeMemories(
                contribution.SharedMemories,
                contribution.CharacterId);
            int conditionOutcomeCount =
                (contribution.ResultingWoundHealthStatus is null ? 0 : 1)
                + (contribution.CaptorCharacterId is null ? 0 : 1)
                + (contribution.ReleasesCustody ? 1 : 0)
                + (contribution.DeathAction is null ? 0 : 1);
            if (conditionOutcomeCount > 1)
            {
                throw new ArgumentException(
                    "A character battle result may contribute only one wound, custody, rescue, or death outcome.");
            }

            for (int index = 0; index < memories.Length; index++)
            {
                commands.Add(CampaignCommand.Create(
                    DeriveCommandId(
                        battleResultId,
                        contribution.CharacterId,
                        "memory",
                        index),
                    contribution.CharacterId,
                    resolutionDate,
                    memories[index],
                    priority: 0));
            }

            if (contribution.ResultingWoundHealthStatus
                is CharacterHealthStatus resultingHealth)
            {
                ApplyCharacterWoundAction wound = new(
                    contribution.CharacterId,
                    projected,
                    resultingHealth,
                    contribution.ResultingWoundIncapacitated);
                projected = ProjectWound(projected, wound);
                commands.Add(CampaignCommand.Create(
                    DeriveCommandId(
                        battleResultId,
                        contribution.CharacterId,
                        "wound",
                        0),
                    CharacterConditionSystem.AuthoritativeActorId,
                    resolutionDate,
                    new CharacterConditionActionCommandPayload(wound),
                    priority: 10));
            }
            else if (contribution.ResultingWoundIncapacitated)
            {
                throw new ArgumentException(
                    "Battle wound incapacitation requires a resulting wound health status.");
            }

            if (contribution.CaptorCharacterId is not null
                && contribution.ReleasesCustody)
            {
                throw new ArgumentException(
                    "A battle result cannot capture and release the same character.");
            }

            if (contribution.CaptorCharacterId is EntityId captorId)
            {
                if (captorId == contribution.CharacterId
                    || !world.Characters.TryGetCharacterProfile(
                        captorId,
                        out AuthoritativeCharacterProfile? captor)
                    || captor.Condition.VitalStatus != CharacterVitalStatus.Alive)
                {
                    throw new ArgumentException(
                        "Battle capture requires a distinct living known captor.");
                }

                if (projected.CustodyStatus != CharacterCustodyStatus.Free)
                {
                    throw new ArgumentException(
                        "Battle capture requires a currently free character.");
                }

                EnterCharacterCustodyAction capture = new(
                    contribution.CharacterId,
                    projected,
                    CharacterCustodyStatus.Captive,
                    captorId);
                projected = projected with
                {
                    CustodyStatus = CharacterCustodyStatus.Captive,
                    CustodianId = captorId,
                };
                commands.Add(CampaignCommand.Create(
                    DeriveCommandId(
                        battleResultId,
                        contribution.CharacterId,
                        "capture",
                        0),
                    CharacterConditionSystem.AuthoritativeActorId,
                    resolutionDate,
                    new CharacterConditionActionCommandPayload(capture),
                    priority: 20));
            }
            else if (contribution.ReleasesCustody)
            {
                if (projected.CustodyStatus == CharacterCustodyStatus.Free)
                {
                    throw new ArgumentException(
                        "Battle rescue requires a character currently in custody.");
                }

                ReleaseCharacterCustodyAction rescue = new(
                    contribution.CharacterId,
                    projected);
                projected = projected with
                {
                    CustodyStatus = CharacterCustodyStatus.Free,
                    CustodianId = null,
                };
                commands.Add(CampaignCommand.Create(
                    DeriveCommandId(
                        battleResultId,
                        contribution.CharacterId,
                        "rescue",
                        0),
                    CharacterConditionSystem.AuthoritativeActorId,
                    resolutionDate,
                    new CharacterConditionActionCommandPayload(rescue),
                    priority: 20));
            }

            if (contribution.DeathAction is ResolveCharacterSuccessionDeathAction death)
            {
                if (death.CharacterId != contribution.CharacterId
                    || death.ExpectedCurrent != projected)
                {
                    throw new ArgumentException(
                        "Caller-supplied succession death must exactly match the projected battle condition.");
                }

                commands.Add(CampaignCommand.Create(
                    DeriveCommandId(
                        battleResultId,
                        contribution.CharacterId,
                        "death",
                        0),
                    CharacterConditionSystem.AuthoritativeActorId,
                    resolutionDate,
                    new CharacterConditionActionCommandPayload(death),
                    priority: 30));
            }
        }

        return ReadOnly(commands
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.CommandId)
            .ToArray());
    }

    private RelationshipDimensions GetDirectionalRelationshipDimensions(
        EntityId subjectCharacterId,
        EntityId targetCharacterId)
    {
        if (!world.Relationships.TryGetSubjectHistory(
                subjectCharacterId,
                out SubjectRelationshipHistory? history))
        {
            return RelationshipDimensions.Zero with { };
        }

        DetailedDirectionalRelationship? relationship = history.DetailedRelationships
            .SingleOrDefault(item =>
                item.TargetCharacterId == targetCharacterId);
        return relationship is null
            ? RelationshipDimensions.Zero with { }
            : relationship.Dimensions with { };
    }

    private EntityId[] ValidateCanonicalCharacters(
        IReadOnlyList<EntityId> characterIds,
        int maximum,
        string label)
    {
        if (characterIds is null
            || characterIds.Count is < 1
            || characterIds.Count > maximum)
        {
            throw new ArgumentException(
                $"{label} require from 1 through {maximum} entries.");
        }

        EntityId[] canonical = characterIds.Order().ToArray();
        if (canonical.Any(item => !item.IsValid)
            || canonical.Distinct().Count() != canonical.Length
            || canonical.Any(item =>
                !world.Characters.TryGetCharacterProfile(
                    item,
                    out AuthoritativeCharacterProfile? profile)
                || profile.Condition.VitalStatus != CharacterVitalStatus.Alive))
        {
            throw new ArgumentException(
                $"{label} must contain unique living known character IDs.");
        }

        return canonical;
    }

    private static RelationshipActionCommandPayload[] CanonicalizeMemories(
        IReadOnlyList<RelationshipActionCommandPayload> memories,
        EntityId subjectCharacterId)
    {
        if (memories is null || memories.Count > MaximumResultContributions)
        {
            throw new ArgumentException(
                $"Battle result memories retain at most {MaximumResultContributions} entries per contribution.");
        }

        return memories.Select(item =>
            {
                if (item is null
                    || item.TargetCharacterId == subjectCharacterId
                    || item.WitnessIds is null)
                {
                    throw new ArgumentException(
                        "Battle relationship memories require a distinct target and witness collection.");
                }

                return item with
                {
                    WitnessIds = Array.AsReadOnly(
                        item.WitnessIds.Order().ToArray()),
                };
            })
            .OrderBy(CanonicalMemoryKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static CharacterConditionState ProjectWound(
        CharacterConditionState current,
        ApplyCharacterWoundAction wound)
    {
        bool worsens = wound.ResultingHealthStatus switch
        {
            CharacterHealthStatus.Injured =>
                current.HealthStatus == CharacterHealthStatus.Healthy,
            CharacterHealthStatus.Critical =>
                current.HealthStatus != CharacterHealthStatus.Critical,
            _ => false,
        };
        if (!worsens
            || wound.ResultingHealthStatus == CharacterHealthStatus.Critical
                && !wound.ResultingIncapacitated
            || current.IsIncapacitated && !wound.ResultingIncapacitated)
        {
            throw new ArgumentException(
                "Battle wound must worsen health without restoring capacity.");
        }

        return current with
        {
            HealthStatus = wound.ResultingHealthStatus,
            IsIncapacitated = wound.ResultingIncapacitated,
        };
    }

    private static string CanonicalMemoryKey(
        RelationshipActionCommandPayload memory) => string.Join(
        '\n',
        memory.TargetCharacterId.Value,
        memory.MeaningId.Value,
        ((int)memory.Publicity).ToString(CultureInfo.InvariantCulture),
        memory.InitialSeverity.ToString(CultureInfo.InvariantCulture),
        memory.DecayIntervalTurns.ToString(CultureInfo.InvariantCulture),
        memory.Impact.Affection.ToString(CultureInfo.InvariantCulture),
        memory.Impact.Trust.ToString(CultureInfo.InvariantCulture),
        memory.Impact.Respect.ToString(CultureInfo.InvariantCulture),
        memory.Impact.Attraction.ToString(CultureInfo.InvariantCulture),
        memory.Impact.Obligation.ToString(CultureInfo.InvariantCulture),
        memory.Impact.Fear.ToString(CultureInfo.InvariantCulture),
        memory.Impact.Resentment.ToString(CultureInfo.InvariantCulture),
        memory.Impact.Rivalry.ToString(CultureInfo.InvariantCulture),
        memory.Impact.Compatibility.ToString(CultureInfo.InvariantCulture),
        string.Join('\n', memory.WitnessIds.Select(item => item.Value)));

    private static EntityId DeriveCommandId(
        EntityId battleResultId,
        EntityId characterId,
        string kind,
        int index)
    {
        string canonical = string.Join(
            '\n',
            "character-battle-boundary-command.v1",
            battleResultId.Value,
            characterId.Value,
            kind,
            index.ToString(CultureInfo.InvariantCulture));
        return new EntityId(
            $"command:battle/{Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
                .ToLowerInvariant()}");
    }

    private static ReadOnlyCollection<T> ReadOnly<T>(T[] values) =>
        Array.AsReadOnly(values);
}
