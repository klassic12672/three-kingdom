using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Simulation.Core;

public readonly record struct RandomStreamState(string Context, ulong State);

public sealed class DeterministicRandomStreams
{
    private readonly ulong rootSeed;
    private readonly SortedDictionary<string, ulong> states = new(StringComparer.Ordinal);

    public DeterministicRandomStreams(ulong rootSeed, IEnumerable<RandomStreamState>? restored = null)
    {
        this.rootSeed = rootSeed;
        if (restored is null)
        {
            return;
        }

        foreach (RandomStreamState stream in restored.OrderBy(item => item.Context, StringComparer.Ordinal))
        {
            if (!states.TryAdd(ValidateContext(stream.Context), stream.State))
            {
                throw new SimulationValidationException($"Duplicate random stream context '{stream.Context}'.");
            }
        }
    }

    public ulong NextUInt64(string system, string context)
    {
        string key = ValidateContext($"{system}/{context}");
        ulong state = states.TryGetValue(key, out ulong existing) ? existing : DeriveSeed(rootSeed, key);
        ulong result = SplitMix64(ref state);
        states[key] = state;
        return result;
    }

    public int NextInt32(string system, string context, int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        }

        ulong bound = (ulong)exclusiveMaximum;
        ulong threshold = unchecked(0UL - bound) % bound;
        ulong value;
        do
        {
            value = NextUInt64(system, context);
        }
        while (value < threshold);

        return (int)(value % bound);
    }

    public IReadOnlyList<RandomStreamState> Capture() => states
        .Select(pair => new RandomStreamState(pair.Key, pair.Value))
        .ToArray();

    private static ulong DeriveSeed(ulong seed, string context)
    {
        byte[] contextBytes = Encoding.UTF8.GetBytes(context);
        byte[] input = new byte[sizeof(ulong) + contextBytes.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(input, seed);
        contextBytes.CopyTo(input, sizeof(ulong));
        byte[] hash = SHA256.HashData(input);
        return BinaryPrimitives.ReadUInt64LittleEndian(hash);
    }

    private static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong value = state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static string ValidateContext(string context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            throw new ArgumentException("Random stream context cannot be empty.", nameof(context));
        }

        return context;
    }
}
