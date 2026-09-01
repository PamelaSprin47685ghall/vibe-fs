namespace Wanxiangshu.Foundation

module SyntheticTomlSurface =
    val normalizeNewlines: text: string -> string
    val renderString: text: string -> string
    val comment: text: string -> string
    val field: name: string -> renderedValue: string -> string
    val tableEntry: name: string -> fields: string array -> string
    val tableArrayEntry: name: string -> fields: string array -> string
    val renderBool: value: bool -> string
    val renderInt: value: int64 -> string
    val renderKey: name: string -> string
    val renderDocument: instructions: string array -> body: string array -> string
    val byteCount: text: string -> int
