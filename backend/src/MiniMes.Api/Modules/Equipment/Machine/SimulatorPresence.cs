namespace MiniMes.Api.Modules.Equipment.Machine;

/// <summary>How many equipment simulators are connected to the machine hub. Safe for concurrent callers.</summary>
public sealed class SimulatorPresence
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Increment() => Interlocked.Increment(ref _count);

    /// <summary>Never goes below zero, even when a disconnect is reported twice.</summary>
    public void Decrement()
    {
        int current;
        do
        {
            current = Volatile.Read(ref _count);
            if (current == 0)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _count, current - 1, current) != current);
    }
}
