namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// Semantic owner surface for the OpenCode tool-host codec. It keeps Host
/// argument records, schema wrappers, callbacks and TOML value unions opaque.
module ToolHostSurface =

    type private ArgumentsHandle(arguments: HostToolArguments) =
        member _.Value = arguments

    type private ContextHandle(context: HostToolContext) =
        member _.Value = context

    let private argumentsOf value =
        unbox<ArgumentsHandle> value |> fun handle -> handle.Value

    let private contextOf value =
        unbox<ContextHandle> value |> fun handle -> handle.Value

    let makeArguments (raw: obj) : obj =
        ArgumentsHandle(HostToolArguments raw) :> obj

    let argumentText (args: obj) (name: string) = (argumentsOf args).Text name

    let argumentOptionalText (args: obj) (name: string) =
        (argumentsOf args).OptionalText name |> Option.toObj

    let argumentOptionalTexts (args: obj) (name: string) =
        (argumentsOf args).OptionalTexts name |> Option.map List.toArray |> Option.toObj

    let argumentOptionalNumber (args: obj) (name: string) =
        (argumentsOf args).OptionalNumber name |> Option.map box |> Option.toObj

    let argumentOptionalNonNegativeInteger (args: obj) (name: string) =
        match (argumentsOf args).OptionalNonNegativeInteger name with
        | Ok(Some value) -> box {| ok = true; value = value |}
        | Ok None -> box {| ok = true; value = null |}
        | Error() -> box {| ok = false |}

    [<Emit("(($0.value ?? $0.fields[0]).value ?? ($0.value ?? $0.fields[0]))")>]
    let private schemaValue (schema: obj) : obj = jsNative

    let private factory (toolModule: obj) = ToolHostCodec.factory toolModule

    let schemaString toolModule =
        ToolHostCodec.stringSchema (factory toolModule) |> schemaValue

    let schemaStringDescribed toolModule description =
        ToolHostCodec.stringSchemaDescribed description (factory toolModule)
        |> schemaValue

    let schemaNumber toolModule =
        ToolHostCodec.numberSchema (factory toolModule) |> schemaValue

    let schemaNumberDescribed toolModule description =
        ToolHostCodec.numberSchemaDescribed description (factory toolModule)
        |> schemaValue

    let schemaBool toolModule =
        ToolHostCodec.boolSchema (factory toolModule) |> schemaValue

    let schemaBoolDescribed toolModule description =
        ToolHostCodec.boolSchemaDescribed description (factory toolModule)
        |> schemaValue

    let schemaEnum toolModule values =
        ToolHostCodec.enumSchema (List.ofArray values) (factory toolModule)
        |> schemaValue

    let schemaEnumDescribed toolModule values description =
        ToolHostCodec.enumSchemaDescribed (List.ofArray values) description (factory toolModule)
        |> schemaValue

    let schemaOptionalEnum toolModule values =
        ToolHostCodec.optionalEnumSchema (List.ofArray values) (factory toolModule)
        |> schemaValue

    let schemaOptionalEnumDescribed toolModule values description =
        ToolHostCodec.optionalEnumSchemaDescribed (List.ofArray values) description (factory toolModule)
        |> schemaValue

    let schemaManagedOrHandle toolModule values =
        ToolHostCodec.managedOrHandleSchema (List.ofArray values) (factory toolModule)
        |> schemaValue

    let schemaOptionalString toolModule =
        ToolHostCodec.optionalStringSchema (factory toolModule) |> schemaValue

    let schemaOptionalStringDescribed toolModule description =
        ToolHostCodec.optionalStringSchemaDescribed description (factory toolModule)
        |> schemaValue

    let schemaOptionalNumber toolModule =
        ToolHostCodec.optionalNumberSchema (factory toolModule) |> schemaValue

    let schemaOptionalNonNegativeIntegerDescribed toolModule description =
        ToolHostCodec.optionalNonNegativeIntegerSchemaDescribed description (factory toolModule)
        |> schemaValue

    let schemaOptionalStringArray toolModule =
        ToolHostCodec.optionalStringArraySchema (factory toolModule) |> schemaValue

    let registryNames (toolModule: obj) (names: string array) : obj =
        let specs =
            names
            |> Array.toList
            |> List.map (fun name ->
                { Name = name
                  Description = name
                  Arguments = []
                  Admission = ToolAdmission.OfficeRole(fun _ _ -> true)
                  Execute = fun _ _ -> Task.FromResult name })

        let value = ToolHostCodec.registry (factory toolModule) specs

        box
            {| names = names
               one = value?one
               two = value?two |}

    let hide (registry: obj) (name: string) (callback: obj) =
        ToolHostCodec.hide registry name callback
        registry

    let contextDecode (raw: obj) : obj =
        ContextHandle(ToolHostCodec.decodeContext raw) :> obj

    let contextView (context: obj) : obj =
        let value = contextOf context

        box
            {| sessionId = value.SessionId
               agent = value.Agent |> Option.toObj
               toolCallId = value.ToolCallId |> Option.map ToolCallId.value |> Option.toObj
               providerRunId = value.ProviderRunId |> Option.map ProviderRunIdentity.value |> Option.toObj
               promptText = value.PromptText |> Option.toObj |}

    let contextAttachAbort (context: obj) (callback: unit -> unit) : (unit -> unit) =
        (contextOf context).AttachAbort callback

    let rec private tomlValue (value: obj) : ToolHostCodec.TomlValue =
        if isNull value then
            ToolHostCodec.TomlValue.TString ""
        elif emitJsExpr value "typeof $0 === 'string'" then
            ToolHostCodec.TomlValue.TString(string value)
        elif emitJsExpr value "typeof $0 === 'boolean'" then
            ToolHostCodec.TomlValue.TBool(unbox<bool> value)
        elif emitJsExpr value "typeof $0 === 'bigint'" then
            ToolHostCodec.TomlValue.TInt64(unbox<int64> value)
        elif emitJsExpr value "typeof $0 === 'number'" then
            ToolHostCodec.TomlValue.TInt(int (unbox<float> value))
        elif emitJsExpr value "Array.isArray($0)" then
            let entries =
                unbox<obj array> value
                |> Array.choose (fun entry ->
                    if isNull entry then
                        None
                    else
                        Some(string entry?name, tomlValue entry?value))
                |> Array.toList

            ToolHostCodec.TomlValue.TTable entries
        else
            let names: string array = emitJsExpr value "Object.getOwnPropertyNames($0)"

            let fields =
                names
                |> Array.map (fun name ->
                    let field = emitJsExpr (value, name) "$0[$1]"
                    name, tomlValue field)
                |> Array.toList

            ToolHostCodec.TomlValue.TTable fields

    let tomlObject (fields: obj array) : string =
        fields
        |> Array.toList
        |> List.map (fun field -> string field?name, tomlValue field?value)
        |> ToolHostCodec.tomlObject

    let tomlObjectWithInstructions (instructions: string array) (fields: obj array) : string =
        let values =
            fields
            |> Array.toList
            |> List.map (fun field -> string field?name, tomlValue field?value)

        ToolHostCodec.tomlObjectWithInstructions (List.ofArray instructions) values

    let tomlTable (name: string) (entries: obj array array) : string =
        entries
        |> Array.map (fun row ->
            row
            |> Array.toList
            |> List.map (fun field -> string field?name, tomlValue field?value))
        |> Array.toList
        |> ToolHostCodec.tomlTable name

    let looksLikeHandleId = ToolHostCodec.looksLikeHandleId
    let digest = ToolHostCodec.digest

    /// Register one plain tool definition through the Host truncation boundary.
    /// The callback stays opaque; ToolHostCodec owns context decoding and bounds
    /// the returned text before it reaches the provider.
    let registerBounded (toolModule: obj) (name: string) (description: string) (execute: unit -> Task<string>) : obj =
        let spec =
            { Name = name
              Description = description
              Arguments = []
              Admission = ToolAdmission.OfficeRole(fun _ _ -> true)
              Execute = fun _ _ -> execute () }

        ToolHostCodec.register (factory toolModule) spec
