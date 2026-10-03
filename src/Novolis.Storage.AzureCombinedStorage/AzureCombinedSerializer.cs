using System.Text.Json;

namespace Novolis.Storage.AzureCombinedStorage;

internal static class AzureCombinedSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public static BinaryData Serialize<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new BinaryData(JsonSerializer.SerializeToUtf8Bytes(value, Options));
    }

    public static T Deserialize<T>(BinaryData payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return JsonSerializer.Deserialize<T>(payload.ToStream(), Options)
            ?? throw new InvalidOperationException($"The stored payload for {typeof(T).Name} was empty.");
    }
}
