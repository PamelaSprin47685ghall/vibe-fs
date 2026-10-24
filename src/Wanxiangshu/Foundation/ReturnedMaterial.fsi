namespace Wanxiangshu.Foundation

/// One serialization owner for delivered material. Canonical storage is a
/// JSON object `{ body: string[], carry: string[] }`; model-facing values are
/// `# ` comment prose plus `[[returned]]` data fields.
[<RequireQualifiedAccess>]
module ReturnedMaterial =
    val encode: body: string list -> carry: string list -> string
    val body: encoded: string -> string list
    val carry: encoded: string -> string list
    val fullText: encoded: string -> string
    val validate: encoded: string -> Result<unit, string>
    val entries: encoded: string -> obj array
    val ofEntries: entries: obj -> string
    val carryData: encoded: string -> LlmFacing.DataBlock list
    val render: encoded: string -> string
    val renderBody: encoded: string -> string
