using System.Text.Json.Serialization;

namespace Latiprimere.EndToEnd.Reporting;

/// <summary>
/// Writes reports as JSON without reflection, so the harness keeps working when published with Native AOT.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HarnessReport))]
internal sealed partial class ReportJsonContext : JsonSerializerContext
{
}
