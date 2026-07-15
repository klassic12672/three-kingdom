using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public static class CharacterResourceContractVersions
{
    public const int Snapshot = 1;
    public const int State = 1;
    public const int Action = 1;
    public const int Outcome = 1;
    public const int AuthoritativeQuery = 1;
}

public static class CharacterResourceLimits
{
    public const int RecentLedgerEntriesPerCharacter = 64;
}

public static class CharacterResourceSystem
{
    public const string SystemId = "simulation.character_resources";
    public const int Version = 1;
}

public sealed record CharacterWealthAccountState(
    int ContractVersion,
    EntityId AccountId,
    EntityId CharacterId,
    long Wealth);

public enum WealthLedgerDirection
{
    Outgoing = 0,
    Incoming = 1,
}

public sealed record WealthTransferRecord(
    int ContractVersion,
    EntityId TransferId,
    EntityId SourceCharacterId,
    EntityId RecipientCharacterId,
    long Amount,
    CampaignDate ResolutionDate,
    long ResolutionTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId);

public sealed record WealthLedgerEntry(
    int ContractVersion,
    EntityId EntryId,
    EntityId TransferId,
    EntityId CharacterId,
    EntityId CounterpartyCharacterId,
    WealthLedgerDirection Direction,
    long Amount,
    CampaignDate ResolutionDate,
    long ResolutionTurnIndex,
    EntityId SourceCommandId,
    EntityId SourceEventId);

public sealed record CharacterWealthHistoryAggregate(
    int ContractVersion,
    EntityId CharacterId,
    long FoldedIncomingCount,
    long FoldedIncomingAmount,
    long FoldedOutgoingCount,
    long FoldedOutgoingAmount,
    CampaignDate? EarliestDate,
    CampaignDate? LatestDate)
{
    public static CharacterWealthHistoryAggregate Empty(EntityId characterId) => new(
        CharacterResourceContractVersions.State,
        characterId,
        0,
        0,
        0,
        0,
        null,
        null);

    [JsonIgnore]
    public long TotalFoldedCount => checked(FoldedIncomingCount + FoldedOutgoingCount);
}

public sealed record CharacterResourceWorldSnapshot(
    int ContractVersion,
    IReadOnlyList<CharacterWealthAccountState> Accounts,
    IReadOnlyList<WealthLedgerEntry> LedgerEntries,
    IReadOnlyList<CharacterWealthHistoryAggregate> History)
{
    public static CharacterResourceWorldSnapshot Empty { get; } = new(
        CharacterResourceContractVersions.Snapshot,
        [],
        [],
        []);

    public CharacterResourceWorldSnapshot Canonicalize() => this with
    {
        Accounts = Accounts.OrderBy(item => item.CharacterId).ToArray(),
        LedgerEntries = LedgerEntries
            .OrderBy(item => item.CharacterId)
            .ThenBy(item => item.ResolutionTurnIndex)
            .ThenBy(item => item.ResolutionDate)
            .ThenBy(item => item.EntryId)
            .ToArray(),
        History = History.OrderBy(item => item.CharacterId).ToArray(),
    };
}

public interface IAuthoritativeCharacterResourceWorldQuery
{
    IReadOnlyList<CharacterWealthAccountState> Accounts { get; }

    IReadOnlyList<WealthLedgerEntry> LedgerEntries { get; }

    IReadOnlyList<CharacterWealthHistoryAggregate> History { get; }

    long GetWealth(EntityId characterId);

    bool TryGetAccount(
        EntityId characterId,
        [NotNullWhen(true)] out CharacterWealthAccountState? account);

    IReadOnlyList<WealthLedgerEntry> GetLedgerEntries(EntityId characterId);

    bool TryGetHistory(
        EntityId characterId,
        [NotNullWhen(true)] out CharacterWealthHistoryAggregate? history);
}

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(TransferWealthAction), "transfer_wealth.v1")]
public interface ICharacterResourceAction;

public sealed record TransferWealthAction(EntityId RecipientCharacterId, long Amount)
    : ICharacterResourceAction;

[method: JsonConstructor]
public sealed record CharacterResourceActionCommandPayload(ICharacterResourceAction Action)
    : ICampaignCommandPayload;

public enum WealthTransferCancellationReason
{
    InsufficientWealth = 0,
    RecipientOverflow = 1,
}

[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "$type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(WealthTransferredOutcome), "wealth_transferred.v1")]
[JsonDerivedType(typeof(WealthTransferCancelledOutcome), "wealth_transfer_cancelled.v1")]
public interface ICharacterResourceActionOutcome;

public sealed record WealthTransferredOutcome(
    int ContractVersion,
    WealthTransferRecord Transfer,
    long SourceWealthAfter,
    long RecipientWealthAfter,
    WealthLedgerEntry OutgoingEntry,
    WealthLedgerEntry IncomingEntry) : ICharacterResourceActionOutcome;

public sealed record WealthTransferCancelledOutcome(
    int ContractVersion,
    WealthTransferCancellationReason Reason) : ICharacterResourceActionOutcome;

public sealed record CharacterResourceActionResolvedEventPayload(
    EntityId ActingCharacterId,
    ICharacterResourceAction Action,
    ICharacterResourceActionOutcome Outcome) : ICampaignEventPayload;

public static class CharacterResourceIds
{
    public static EntityId DeriveWealthAccountId(EntityId characterId)
    {
        RequireId(characterId, nameof(characterId));
        return Hash("wealth_account", "wealth-account.v1", characterId.Value);
    }

    public static EntityId DeriveActionEventId(CampaignDate resolutionDate, EntityId commandId)
    {
        RequireDate(resolutionDate, nameof(resolutionDate));
        RequireId(commandId, nameof(commandId));
        return Hash(
            "event",
            "character-resource-action-event.v1",
            FormatDate(resolutionDate),
            commandId.Value);
    }

    public static EntityId DeriveWealthTransferId(EntityId eventId)
    {
        RequireId(eventId, nameof(eventId));
        return Hash("wealth_transfer", "wealth-transfer.v1", eventId.Value);
    }

    public static EntityId DeriveWealthLedgerEntryId(
        EntityId transferId,
        EntityId characterId,
        WealthLedgerDirection direction)
    {
        RequireId(transferId, nameof(transferId));
        RequireId(characterId, nameof(characterId));
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        return Hash(
            "wealth_ledger_entry",
            "wealth-ledger-entry.v1",
            transferId.Value,
            characterId.Value,
            ((int)direction).ToString(CultureInfo.InvariantCulture));
    }

    private static EntityId Hash(string entityNamespace, string domain, params string[] fields)
    {
        StringBuilder canonical = new();
        AppendField(canonical, domain);
        foreach (string field in fields)
        {
            AppendField(canonical, field);
        }

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return new EntityId($"{entityNamespace}:sha256/{Convert.ToHexStringLower(digest)}");
    }

    private static void AppendField(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
        target.Append(';');
    }

    private static string FormatDate(CampaignDate value) => string.Concat(
        value.Year.ToString("D4", CultureInfo.InvariantCulture),
        "-",
        value.Month.ToString("D2", CultureInfo.InvariantCulture),
        "-",
        value.Day.ToString("D2", CultureInfo.InvariantCulture));

    private static void RequireId(EntityId value, string parameterName)
    {
        if (!value.IsValid)
        {
            throw new ArgumentException("A valid stable ID is required.", parameterName);
        }
    }

    private static void RequireDate(CampaignDate value, string parameterName)
    {
        if (!value.IsValid)
        {
            throw new ArgumentException("A valid campaign date is required.", parameterName);
        }
    }
}
