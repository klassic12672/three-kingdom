using System.Reflection;
using System.Text.Json;

namespace Simulation.Core;

public class SimulationException : Exception
{
    public SimulationException(string message)
        : base(message)
    {
    }

    public SimulationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class SimulationValidationException : SimulationException
{
    public SimulationValidationException(string message)
        : base(message)
    {
    }
}

public sealed class SaveCompatibilityException : SimulationException
{
    public SaveCompatibilityException(string message)
        : base(message)
    {
    }

    public SaveCompatibilityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal static class SaveDataExceptionPolicy
{
    public static bool IsRecoverableDataFailure(Exception exception)
    {
        if (ContainsFatalException(exception))
        {
            return false;
        }

        Exception cause = UnwrapInvocation(exception);
        return cause is JsonException
            or NotSupportedException
            or ArgumentException
            or FormatException
            or InvalidOperationException
            or OverflowException;
    }

    public static Exception GetDiagnosticCause(Exception exception)
    {
        Exception cause = UnwrapInvocation(exception);
        while (cause.InnerException is not null && IsRecoverableDataFailure(cause.InnerException))
        {
            cause = UnwrapInvocation(cause.InnerException);
        }

        return cause;
    }

    private static Exception UnwrapInvocation(Exception exception)
    {
        Exception current = exception;
        while (current is TargetInvocationException { InnerException: not null })
        {
            current = current.InnerException;
        }

        return current;
    }

    private static bool ContainsFatalException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OutOfMemoryException
                or StackOverflowException
                or AccessViolationException
                or AppDomainUnloadedException)
            {
                return true;
            }
        }

        return false;
    }
}
