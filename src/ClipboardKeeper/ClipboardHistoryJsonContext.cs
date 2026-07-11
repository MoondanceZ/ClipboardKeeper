using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClipboardKeeper;

[JsonSerializable(typeof(List<ClipboardHistoryItem>))]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class ClipboardHistoryJsonContext : JsonSerializerContext;
