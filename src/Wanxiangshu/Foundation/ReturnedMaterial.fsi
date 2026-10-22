namespace Wanxiangshu.Foundation

/// One serialization owner for delivered material. Canonical storage is a
/// JSON string array; model-facing values are structured entries or TOML.
[<RequireQualifiedAccess>]
module ReturnedMaterial =
    val ofTexts: texts: string list -> string
    val texts: encoded: string -> string list
    val validate: encoded: string -> Result<unit, string>
    val entries: encoded: string -> obj array
    val ofEntries: entries: obj -> string
    val data: encoded: string -> LlmFacing.DataBlock list
    val render: encoded: string -> string
