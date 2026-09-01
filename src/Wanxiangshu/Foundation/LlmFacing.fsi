namespace Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
module LlmFacing =
    type DataBlock = private DataBlock of string

    type Document =
        private
            { Instructions: string list
              Data: DataBlock list }

    val empty: Document
    val instructions: texts: string list -> Document
    val instruction: text: string -> Document
    val withInstructions: texts: string list -> document: Document -> Document
    val withInstruction: text: string -> document: Document -> Document
    val withData: blocks: DataBlock list -> document: Document -> Document
    val combine: documents: Document list -> Document
    val render: document: Document -> string
    val renderInstruction: text: string -> string
    val renderInstructions: texts: string list -> string
    val normalizeNewlines: (string -> string)
    val byteCount: (string -> int)
    val stringValueByteCount: text: string -> int
    val stringValuePrefixByteCount: text: string -> length: int -> suffix: string -> int

    [<RequireQualifiedAccess>]
    module Data =
        type Value =
            | Null
            | Bool of bool
            | Integer of int64
            | Float of float
            | String of string
            | Array of Value list
            | Object of (string * Value) list

        type Field = private Field of string

        val stringField: name: string -> value: string -> DataBlock
        val intField: name: string -> value: int -> DataBlock
        val int64Field: name: string -> value: int64 -> DataBlock
        val floatField: name: string -> value: float -> DataBlock
        val boolField: name: string -> value: bool -> DataBlock
        val stringMember: name: string -> value: string -> Field
        val intMember: name: string -> value: int -> Field
        val int64Member: name: string -> value: int64 -> Field
        val floatMember: name: string -> value: float -> Field
        val boolMember: name: string -> value: bool -> Field
        val table: name: string -> fields: Field list -> DataBlock
        val tableArray: name: string -> fields: Field list -> DataBlock
        val structuredValue: value: Value -> DataBlock list
        val fileEffects: rewritten: string list -> created: string list -> DataBlock list
