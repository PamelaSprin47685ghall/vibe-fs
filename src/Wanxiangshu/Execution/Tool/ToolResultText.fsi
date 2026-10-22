namespace Wanxiangshu.Execution.Tool

/// ARCH-010 data-only TOML value algebra for tool result bodies. The product
/// owns the result text; a host adapter only bounds and returns it.
module ToolResultText =
    type TomlValue =
        | TString of string
        | TInt of int
        | TInt64 of int64
        | TBool of bool
        | TTable of (string * TomlValue) list

    val tomlObject: fields: (string * TomlValue) list -> string
    val tomlObjectWithInstructions: instructions: string list -> fields: (string * TomlValue) list -> string
    val tomlTable: name: string -> entries: (string * TomlValue) list list -> string
