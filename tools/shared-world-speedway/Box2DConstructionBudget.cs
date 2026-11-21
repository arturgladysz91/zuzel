using System.Text;
using System.Text.Json;
using Box2D.NET;
using static Box2D.NET.B2Worlds;

namespace SharedWorldSpeedway;

public static class Box2DConstructionBudget
{
    public const string LedgerVariable = "SPEEDWAY_BOX2D_LEDGER";
    public const string NoWorldsVariable = "SPEEDWAY_BOX2D_NO_WORLDS";
    public const int MaximumConstructions = 8;

    internal static B2WorldId Create(B2WorldDef definition, string adapter)
    {
        if (Environment.GetEnvironmentVariable(NoWorldsVariable) == "1")
            throw new InvalidOperationException("Box2D construction forbidden during MODEL validation.");
        if (Environment.GetEnvironmentVariable(LedgerVariable) is { Length: > 0 } ledger)
            Reserve(ledger, adapter);
        return b2CreateWorld(definition);
    }

    // Count reservations, including failed constructions, before calling Box2D. Exclusive
    // file access makes the same budget apply to concurrent processes and child test hosts.
    public static void Reserve(string ledger, string adapter)
    {
        using var stream = OpenExclusive(ledger);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        int count = 0;
        while (reader.ReadLine() is { } line)
        {
            using var entry = JsonDocument.Parse(line);
            if (entry.RootElement.GetProperty("Slot").GetInt32() != ++count)
                throw new InvalidOperationException("Corrupt Box2D ledger.");
        }
        if (count >= MaximumConstructions)
            throw new InvalidOperationException($"Box2D construction budget exhausted ({count}/{MaximumConstructions}).");
        stream.Seek(0, SeekOrigin.End);
        var reservation = new { Slot = count + 1, Adapter = adapter, Process = Environment.ProcessId,
            Scenario = Environment.GetEnvironmentVariable("SPEEDWAY_BOX2D_SCENARIO") };
        stream.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reservation) + "\n"));
        stream.Flush(flushToDisk: true);
    }

    private static FileStream OpenExclusive(string ledger)
    {
        if (!Path.IsPathFullyQualified(ledger))
            throw new ArgumentException("The study ledger requires an absolute path.", nameof(ledger));
        for (int attempt = 0; ; attempt++)
        {
            try { return new(ledger, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException error) when (attempt < 100 && (error.HResult & 0xffff) is 32 or 33)
            {
                Thread.Sleep(10);
            }
        }
    }
}
