namespace Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
module SyntheticToml =
    val normalizeNewlines: text: string -> string
    val renderString: raw: string -> string
    val comment: text: string -> string
    val field: name: string -> renderedValue: string -> string
    val tableEntry: name: string -> fields: string list -> string
    val tableArrayEntry: name: string -> fields: string list -> string
    val renderBool: value: bool -> string
    val renderInt: value: int64 -> string
    val renderFloat: value: float -> string
    val renderKey: name: string -> string
    val encodeFs: rewritten: string list -> created: string list -> string list
    val document: instructions: string list -> body: string list -> string
    val renderStringByteCountPrefix: text: string -> length: int -> suffix: string -> int
    val byteCount: text: string -> int
