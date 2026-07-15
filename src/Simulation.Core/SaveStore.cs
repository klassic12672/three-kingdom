using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core;

public sealed class SaveStore
{
    private readonly SaveSchemaRegistry schemaRegistry;

    public SaveStore(SaveSchemaRegistry? schemaRegistry = null)
    {
        this.schemaRegistry = schemaRegistry ?? new SaveSchemaRegistry();
    }

    public void SaveAtomic(string path, SaveEnvelope envelope, Action<string>? beforeCommit = null)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = GetTemporaryPath(fullPath);
        try
        {
            ValidateEnvelope(envelope, null);
            WriteCompressed(temporary, envelope);
            ValidateEnvelope(ReadCompressed(temporary), null);
            beforeCommit?.Invoke(temporary);
            Commit(temporary, fullPath);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    public void SaveAutosave(
        string path,
        SaveEnvelope envelope,
        int generations = 3,
        Action<string>? beforeCommit = null)
    {
        if (generations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(generations));
        }

        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = GetTemporaryPath(fullPath);
        string? priorBackup = null;
        try
        {
            ValidateEnvelope(envelope, null);
            WriteCompressed(temporary, envelope);
            ValidateEnvelope(ReadCompressed(temporary), null);
            beforeCommit?.Invoke(temporary);

            if (File.Exists(fullPath))
            {
                priorBackup = GetTemporaryPath(fullPath + ".prior");
                File.Copy(fullPath, priorBackup, overwrite: false);
            }

            RotateGenerations(fullPath, generations);
            Commit(temporary, fullPath);
            if (priorBackup is not null)
            {
                Commit(priorBackup, GenerationPath(fullPath, 1));
                priorBackup = null;
            }
        }
        finally
        {
            TryDelete(temporary);
            if (priorBackup is not null)
            {
                TryDelete(priorBackup);
            }
        }
    }

    public SaveEnvelope Load(
        string path,
        IEnumerable<ContentManifestReference>? availableContent = null)
    {
        SaveEnvelope envelope = ReadCompressed(Path.GetFullPath(path));
        ValidateEnvelope(envelope, availableContent);
        return envelope;
    }

    public SaveLoadResult LoadWithRecovery(
        string primaryPath,
        int generations = 3,
        IEnumerable<ContentManifestReference>? availableContent = null)
    {
        string fullPath = Path.GetFullPath(primaryPath);
        List<string> diagnostics = [];
        foreach (string candidate in CandidatePaths(fullPath, generations))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                SaveEnvelope envelope = Load(candidate, availableContent);
                string? diagnostic = candidate == fullPath
                    ? null
                    : $"Primary save could not be loaded; recovered from '{candidate}'. {string.Join(" ", diagnostics)}";
                return new SaveLoadResult(envelope, candidate, diagnostic);
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException or SaveCompatibilityException)
            {
                diagnostics.Add($"{Path.GetFileName(candidate)}: {exception.Message}");
            }
        }

        throw new SaveCompatibilityException(
            diagnostics.Count == 0
                ? "No save or autosave generation exists."
                : $"No valid save generation was found. {string.Join(" ", diagnostics)}");
    }

    private SaveEnvelope ReadCompressed(string path)
    {
        try
        {
            using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using GZipStream gzip = new(file, CompressionMode.Decompress, leaveOpen: false);
            JsonNode? node = JsonNode.Parse(gzip);
            if (node is not JsonObject source)
            {
                throw new SaveCompatibilityException("Save root must be a JSON object.");
            }

            JsonObject migrated = schemaRegistry.MigrateToCurrent(source);
            try
            {
                return migrated.Deserialize<SaveEnvelope>(CanonicalJson.Options)
                    ?? throw new SaveCompatibilityException("Save envelope is empty.");
            }
            catch (Exception exception) when (SaveDataExceptionPolicy.IsRecoverableDataFailure(exception))
            {
                Exception cause = SaveDataExceptionPolicy.GetDiagnosticCause(exception);
                throw new SaveCompatibilityException(
                    $"Save '{path}' contains invalid serialized data: {cause.Message}",
                    exception);
            }
        }
        catch (SaveCompatibilityException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            || SaveDataExceptionPolicy.IsRecoverableDataFailure(exception))
        {
            Exception cause = SaveDataExceptionPolicy.GetDiagnosticCause(exception);
            throw new SaveCompatibilityException(
                $"Save '{path}' is corrupt or unreadable: {cause.Message}",
                exception);
        }
    }

    private static void WriteCompressed(string path, SaveEnvelope envelope)
    {
        using FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using (GZipStream gzip = new(file, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            JsonSerializer.Serialize(gzip, envelope, CanonicalJson.Options);
        }

        file.Flush(flushToDisk: true);
    }

    private static void ValidateEnvelope(
        SaveEnvelope envelope,
        IEnumerable<ContentManifestReference>? availableContent)
    {
        if (envelope is null
            || envelope.ContentManifests is null
            || envelope.Snapshot is null
            || envelope.DiagnosticCommands is null
            || envelope.DiagnosticEvents is null)
        {
            throw new SaveCompatibilityException("Save envelope is missing required objects or collections.");
        }

        if (envelope.ContractVersion != ContractVersions.SaveEnvelope)
        {
            throw new SaveCompatibilityException($"Unsupported save-envelope contract version {envelope.ContractVersion}.");
        }

        if (envelope.SchemaVersion != SaveEnvelope.CurrentSchemaVersion)
        {
            throw new SaveCompatibilityException($"Unsupported save schema version {envelope.SchemaVersion}.");
        }

        if (envelope.ContentManifests.Any(manifest => manifest is null
            || !manifest.PackId.IsValid
            || string.IsNullOrWhiteSpace(manifest.Version)
            || string.IsNullOrWhiteSpace(manifest.Checksum)))
        {
            throw new SaveCompatibilityException("Save contains an invalid content manifest reference.");
        }

        if (envelope.ContentManifests.Select(manifest => manifest.PackId).Distinct().Count()
            != envelope.ContentManifests.Count)
        {
            throw new SaveCompatibilityException("Save contains duplicate content manifest IDs.");
        }

        if (envelope.DiagnosticCommands.Any(command => command is null)
            || envelope.DiagnosticEvents.Any(campaignEvent => campaignEvent is null))
        {
            throw new SaveCompatibilityException("Save diagnostics contain null entries.");
        }

        if (envelope.Seed != envelope.Snapshot.RootSeed)
        {
            throw new SaveCompatibilityException("Save seed does not match snapshot seed.");
        }

        try
        {
            _ = WorldState.Restore(envelope.Snapshot);
        }
        catch (SimulationValidationException exception)
        {
            throw new SaveCompatibilityException("Save snapshot failed authoritative simulation validation.", exception);
        }

        string actualChecksum = SimulationChecksum.Compute(envelope.Snapshot).Value;
        if (!StringComparer.Ordinal.Equals(actualChecksum, envelope.Checksum))
        {
            throw new SaveCompatibilityException(
                $"Save checksum does not match its authoritative snapshot (stored {envelope.Checksum}, actual {actualChecksum}).");
        }

        if (availableContent is null)
        {
            return;
        }

        HashSet<string> available = availableContent
            .Select(ContentKey)
            .ToHashSet(StringComparer.Ordinal);
        string[] missing = envelope.ContentManifests
            .Where(item => item.RequiredForSimulation && !available.Contains(ContentKey(item)))
            .Select(item => $"{item.PackId}@{item.Version} ({item.Checksum})")
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
        {
            throw new SaveCompatibilityException($"Missing required content manifests: {string.Join(", ", missing)}");
        }
    }

    private static string ContentKey(ContentManifestReference manifest) =>
        $"{manifest.PackId.Value}\n{manifest.Version}\n{manifest.Checksum}";

    private static IEnumerable<string> CandidatePaths(string path, int generations)
    {
        yield return path;
        for (int generation = 1; generation <= generations; generation++)
        {
            yield return GenerationPath(path, generation);
        }
    }

    private static void RotateGenerations(string path, int generations)
    {
        TryDelete(GenerationPath(path, generations));
        for (int generation = generations - 1; generation >= 1; generation--)
        {
            string source = GenerationPath(path, generation);
            if (File.Exists(source))
            {
                File.Move(source, GenerationPath(path, generation + 1));
            }
        }
    }

    private static void Commit(string temporary, string destination)
    {
        if (File.Exists(destination))
        {
            File.Move(temporary, destination, overwrite: true);
        }
        else
        {
            File.Move(temporary, destination);
        }
    }

    private static string GenerationPath(string path, int generation) => $"{path}.{generation}";

    private static string GetTemporaryPath(string path) => $"{path}.{Guid.NewGuid():N}.tmp";

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
