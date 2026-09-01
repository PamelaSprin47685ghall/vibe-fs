namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module ToolHostSurface =
    val makeArguments: raw: obj -> obj
    val argumentText: args: obj -> name: string -> string
    val argumentOptionalText: args: obj -> name: string -> string
    val argumentOptionalTexts: args: obj -> name: string -> string array
    val argumentOptionalNumber: args: obj -> name: string -> obj
    val argumentOptionalNonNegativeInteger: args: obj -> name: string -> obj
    val schemaString: toolModule: 'toolModule -> obj
    val schemaStringDescribed: toolModule: 'toolModule -> description: string -> obj
    val schemaNumber: toolModule: 'toolModule -> obj
    val schemaNumberDescribed: toolModule: 'toolModule -> description: string -> obj
    val schemaBool: toolModule: 'toolModule -> obj
    val schemaBoolDescribed: toolModule: 'toolModule -> description: string -> obj
    val schemaEnum: toolModule: 'toolModule -> values: string array -> obj
    val schemaEnumDescribed: toolModule: 'toolModule -> values: string array -> description: string -> obj
    val schemaOptionalEnum: toolModule: 'toolModule -> values: string array -> obj
    val schemaOptionalEnumDescribed: toolModule: 'toolModule -> values: string array -> description: string -> obj
    val schemaManagedOrHandle: toolModule: 'toolModule -> values: string array -> obj
    val schemaOptionalString: toolModule: 'toolModule -> obj
    val schemaOptionalStringDescribed: toolModule: 'toolModule -> description: string -> obj
    val schemaOptionalNumber: toolModule: 'toolModule -> obj
    val schemaOptionalNonNegativeIntegerDescribed: toolModule: 'toolModule -> description: string -> obj
    val schemaOptionalStringArray: toolModule: 'toolModule -> obj
    val registryNames: toolModule: obj -> names: string array -> obj
    val hide: registry: obj -> name: string -> callback: obj -> obj
    val contextDecode: raw: obj -> obj
    val contextView: context: obj -> obj
    val contextAttachAbort: context: obj -> callback: (unit -> unit) -> (unit -> unit)
    val tomlObject: fields: obj array -> string
    val tomlObjectWithInstructions: instructions: string array -> fields: obj array -> string
    val tomlTable: name: string -> entries: obj array array -> string
    val looksLikeHandleId: (string -> bool)
    val digest: (string -> string)

    val registerBounded:
        toolModule: obj -> name: string -> description: string -> execute: (unit -> Task<string>) -> obj
