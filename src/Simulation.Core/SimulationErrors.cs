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
