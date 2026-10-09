using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;

namespace RaceReadiness;

internal static class Exact
{
    internal static SortedDictionary<string, object?> Leaves(object? value)
    {
        var rows = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        Walk(value, "root", rows); return rows;
    }
    internal static string Hash(object? value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Leaves(value))));
    private static void Walk(object? value, string path, IDictionary<string, object?> rows)
    {
        if (value is null) { rows[path] = null; return; }
        if (value is float f) { rows[path] = $"float:{BitConverter.SingleToInt32Bits(f):X8}"; return; }
        if (value is double d) { rows[path] = $"double:{BitConverter.DoubleToInt64Bits(d):X16}"; return; }
        if (value is string s) { rows[path] = "string:" + s; return; }
        var type = value.GetType();
        if (type.IsEnum) { rows[path] = $"enum:{type.FullName}:{Enum.Format(type, value, "D")}"; return; }
        if (type.IsPrimitive) { rows[path] = $"{type.FullName}:{value}"; return; }
        if (value is TrackStateSnapshot surface)
        {
            rows[path + ".Segments"] = surface.SegmentCount; rows[path + ".Lanes"] = surface.LinesCount;
            for (var sIndex = 0; sIndex < surface.SegmentCount; sIndex++) for (var lane = 0; lane < surface.LinesCount; lane++)
                Walk(surface.GetSurface(sIndex, lane), $"{path}[{sIndex},{lane}]", rows);
            return;
        }
        if (value is IEnumerable sequence)
        { var i = 0; foreach (var item in sequence) Walk(item, $"{path}[{i++}]", rows); rows[path + ".Count"] = i; return; }
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0))
            Walk(p.GetValue(value), path + "." + p.Name, rows);
    }
}
