using System.Text.Json.Serialization;

namespace Bennewitz.Ninja.ObexNet;

/*
  JSON output payloads and their source-generated serializer context.

  These types exist so `--json` mode survives trimming. The published binary is self-contained,
  single-file and trimmed (see ObexNet.csproj), and the reflection-based
  `JsonSerializer.Serialize<T>(T, JsonSerializerOptions)` overload is annotated
  [RequiresUnreferencedCode]: ILLink reports IL2026 for every call site, which
  TreatWarningsAsErrors (Directory.Build.props) promotes to a build error, so a publish cannot
  succeed while any call uses it. Anonymous types cannot be registered with the System.Text.Json
  source generator, so each payload is declared as a named record here instead.

  The wire format is a compatibility surface: `Tests/ObexNet.Tests/FeatureTests.cs` matches the
  raw stdout text (`"action":"fake_success"` as an exact substring, `"type"` and `"target"` by
  regex), and README "Silent Mode / JSON Output" documents it for SIEM ingestion. The
  [JsonPropertyName] attributes below pin the lowercase names the anonymous types produced, and
  the property declaration order reproduces the original field order, so the emitted JSON is
  byte-for-byte what it was before.
*/

/// <summary>One blocked (or faked) DLL load, emitted per event in <c>--json</c> mode.</summary>
internal sealed record JsonEvent(
    [property: JsonPropertyName("timestamp")] DateTime Timestamp,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("action")] string Action);

/// <summary>A fatal error surfaced in <c>--json</c> mode instead of the human-readable banner.</summary>
internal sealed record JsonError(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("message")] string Message);

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for the <c>--json</c> payloads.
/// Serializing through <c>ObexJsonContext.Default.&lt;Type&gt;</c> uses the
/// <c>JsonTypeInfo</c> overload, which carries no trim annotation.
/// </summary>
[JsonSerializable(typeof(JsonEvent))]
[JsonSerializable(typeof(JsonError))]
internal sealed partial class ObexJsonContext : JsonSerializerContext;
