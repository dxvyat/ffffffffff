using System.Text.Json;
using System.Text.Json.Serialization;

namespace DXVPrivateTweek;

public sealed record TweakResult(bool Success, string Message, bool Changed = false);

public sealed class TweakDefinition
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Risk { get; init; }
    public required string Preview { get; init; }
    public required Func<TweakContext, Task<TweakResult>> ApplyAsync { get; init; }
    public required Func<TweakContext, Task<TweakResult>> RollbackAsync { get; init; }
}

public sealed class RegistryValueSnapshot
{
    public required string KeyPath { get; init; }
    public required string ValueName { get; init; }
    public bool Exists { get; init; }
    public string? Kind { get; init; }
    public string? Data { get; init; }
}

public sealed class PowerPlanSnapshot
{
    public string? ActiveGuid { get; init; }
}

public sealed class BackupDocument
{
    public string TweakId { get; init; } = "";
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public object? Payload { get; init; }
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
