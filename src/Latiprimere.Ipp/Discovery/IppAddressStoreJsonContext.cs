using System.Text.Json.Serialization;

namespace Latiprimere.Ipp.Discovery;

/// <summary>
/// The source-generated JSON reading and writing of the address store, which Native AOT needs.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StoredAddresses))]
internal sealed partial class IppAddressStoreJsonContext : JsonSerializerContext;
