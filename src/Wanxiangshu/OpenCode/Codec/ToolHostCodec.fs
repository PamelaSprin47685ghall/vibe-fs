namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Host.Contract
open Wanxiangshu.Foundation.Identity

module private HostArgDecode =
    let nonEmptyString (value: string) =
        if String.IsNullOrWhiteSpace value then None else Some value

    let nonNullText (item: obj) =
        if isNull item then
            None
        else
            string item |> Option.ofObj |> Option.filter (String.IsNullOrWhiteSpace >> not)

    // Fable erases unbox<obj array> to identity: a plain string would iterate its
    // CHARACTERS as items. Array-test first — a non-array is absent, never a char sequence.
    let textsFromArrayValue (value: obj) =
        if emitJsExpr value "Array.isArray($0)" then
            unbox<obj array> value |> Array.choose nonNullText |> Array.toList |> Some
        else
            None

    let tryTextsFromArrayValue (value: obj) =
        try
            textsFromArrayValue value
        with _ ->
            None

    let stringsFromArrayValue (value: obj) =
        if emitJsExpr value "Array.isArray($0) && $0.every(item => typeof item === 'string')" then
            unbox<string array> value |> Array.toList |> Some
        else
            None

    let tryStringsFromArrayValue (value: obj) =
        try
            stringsFromArrayValue value
        with _ ->
            None

    // Fable erases unbox<float> to identity, so a string would pass through where
    // .NET would throw. Type-test instead: a non-number is absent, never a wrong-typed Some.
    let numberFromValue (value: obj) =
        if emitJsExpr value "typeof $0 === 'number'" then
            Some(unbox<float> value)
        else
            None

    let tryNumberFromValue (value: obj) =
        try
            numberFromValue value
        with _ ->
            None

    let nonNegativeIntegerFromValue (value: obj) : Result<int option, unit> =
        if emitJsExpr value "typeof $0 === 'number' && Number.isInteger($0) && $0 >= 0" then
            Ok(Some(int (unbox<float> value)))
        else
            Error()

    let boolFromValue (value: obj) =
        if emitJsExpr value "typeof $0 === 'boolean'" then
            Some(unbox<bool> value)
        else
            None

    let tryBoolFromValue (value: obj) =
        try
            boolFromValue value
        with _ ->
            None

/// Opaque Host arguments. Dynamic property access is confined to this codec.
type HostToolArguments internal (raw: obj) =
    member _.Text(name: string) =
        if isNull raw || isNull raw?(name) then
            ""
        else
            unbox<string> raw?(name)

    member this.OptionalText(name: string) =
        this.Text name
        |> Option.ofObj
        |> Option.filter (String.IsNullOrWhiteSpace >> not)

    member _.OptionalTexts(name: string) =
        if isNull raw || isNull raw?(name) then
            None
        else
            HostArgDecode.tryTextsFromArrayValue (raw?(name))

    member _.Texts(name: string) =
        if isNull raw || isNull raw?(name) then
            []
        else
            HostArgDecode.tryStringsFromArrayValue (raw?(name)) |> Option.defaultValue []

    member _.OptionalNumber(name: string) =
        if isNull raw || isNull raw?(name) then
            None
        else
            HostArgDecode.tryNumberFromValue (raw?(name))

    member _.OptionalNonNegativeInteger(name: string) : Result<int option, unit> =
        if isNull raw || isNull raw?(name) then
            Ok None
        else
            HostArgDecode.nonNegativeIntegerFromValue (raw?(name))

    member _.ExactBoundedIntegers(names: string list, minimum: int, maximum: int) =
        if isNull raw then
            Error "arguments must be an object"
        else
            let actual: string array = emitJsExpr raw "Object.keys($0)"
            let expected = names |> Set.ofList

            if Set.ofArray actual <> expected then
                Error "arguments must contain exactly the required fields"
            else
                let rec loop remaining collected =
                    match remaining with
                    | [] -> Ok(List.rev collected)
                    | name :: rest ->
                        let value = raw?(name)
                        let valid: bool = emitJsExpr value "Number.isInteger($0)"

                        if not valid then
                            Error(sprintf "%s must be an integer" name)
                        else
                            let score = int (unbox<float> value)

                            if score < minimum || score > maximum then
                                Error(sprintf "%s must be between %d and %d" name minimum maximum)
                            else
                                loop rest ((name, score) :: collected)

                loop names []

    member _.OptionalBool(name: string) =
        if isNull raw || isNull raw?(name) then
            None
        else
            HostArgDecode.tryBoolFromValue (raw?(name))

/// Typed subset of the OpenCode tool invocation context used by domain tools.
///
/// No user-message field. HOST-011: the Host's `ToolContext` carries `sessionID`,
/// `messageID` and `callID`, and never a user message id — verified against Host
/// `tool/tool.ts` and the published `@opencode-ai/plugin` types. The old optional
/// field decoded a key that does not exist, so it was always `None`, and every
/// `Option.orElse` fallback beside it was unreachable code that read as a working
/// second source.
type HostToolContext =
    { SessionId: string
      Agent: string option
      ToolCallId: ToolCallId option
      ProviderRunId: ProviderRunIdentity option
      PromptText: string option
      AttachAbort: (unit -> unit) -> (unit -> unit) }

type HostToolFactory = private HostToolFactory of obj
type HostSchema = private HostSchema of obj

/// The authority a tool call is admitted under. ENF-006 keeps the two sources
/// apart in the type: an office tool is decided by the public Role the Authority
/// Root established, while an internal leaf tool (Bookkeeper's `js-bookkeeper`)
/// is decided by owner-held attachment evidence because its session is
/// HostInternal and holds no public authority profile at all.
[<RequireQualifiedAccess>]
type ToolAdmission =
    | OfficeRole of (HostToolContext -> Role -> bool)
    | PrivateAttachment of (HostToolContext -> bool)

type ToolSpec =
    { Name: string
      Description: string
      Arguments: (string * HostSchema) list
      Admission: ToolAdmission
      Execute: HostToolArguments -> HostToolContext -> Task<string> }

/// The only dynamic JS boundary used by tool definitions and invocations.
module ToolHostCodec =

    [<Emit("$0.schema.string()")>]
    let private rawStringSchema (tool: obj) : obj = jsNative

    [<Emit("$0.schema.string().describe($1)")>]
    let private rawStringSchemaDescribed (tool: obj) (description: string) : obj = jsNative

    [<Emit("$0.schema.number()")>]
    let private rawNumberSchema (tool: obj) : obj = jsNative

    [<Emit("$0.schema.number().describe($1)")>]
    let private rawNumberSchemaDescribed (tool: obj) (description: string) : obj = jsNative

    [<Emit("$0.schema.number().int().min($1).max($2).describe($3)")>]
    let private rawBoundedIntegerSchema
        (tool: obj)
        (minimum: int)
        (maximum: int)
        (description: string)
        : obj =
        jsNative

    [<Emit("$0.schema.boolean()")>]
    let private rawBooleanSchema (tool: obj) : obj = jsNative

    [<Emit("$0.schema.boolean().describe($1)")>]
    let private rawBooleanSchemaDescribed (tool: obj) (description: string) : obj = jsNative

    [<Emit("$0.schema.enum($1)")>]
    let private rawEnumSchema (tool: obj) (values: string array) : obj = jsNative

    [<Emit("$0.schema.enum($1).describe($2)")>]
    let private rawEnumSchemaDescribed (tool: obj) (values: string array) (description: string) : obj = jsNative

    [<Emit("$0.schema.enum($1).optional()")>]
    let private rawOptionalEnumSchema (tool: obj) (values: string array) : obj = jsNative

    [<Emit("$0.schema.enum($1).describe($2).optional()")>]
    let private rawOptionalEnumSchemaDescribed (tool: obj) (values: string array) (description: string) : obj = jsNative

    [<Emit("$0.schema.union([$0.schema.enum($1), $0.schema.string()])")>]
    let private rawManagedOrHandleSchema (tool: obj) (values: string array) : obj = jsNative

    [<Emit("$0.schema.string().optional()")>]
    let private rawOptionalStringSchema (tool: obj) : obj = jsNative

    [<Emit("$0.schema.string().describe($1).optional()")>]
    let private rawOptionalStringSchemaDescribed (tool: obj) (description: string) : obj = jsNative

    [<Emit("$0.schema.number().optional()")>]
    let private rawOptionalNumberSchema (tool: obj) : obj = jsNative

    [<Emit("$0.schema.number().int().nonnegative().describe($1).optional()")>]
    let private rawOptionalNonNegativeIntegerSchemaDescribed (tool: obj) (description: string) : obj = jsNative

    [<Emit("$0.schema.array($0.schema.string()).optional()")>]
    let private rawOptionalStringArraySchema (tool: obj) : obj = jsNative

    [<Emit("$0.schema.array($0.schema.string())")>]
    let private rawStringArraySchema (tool: obj) : obj = jsNative

    [<Emit("$0($1)")>]
    let private applyTool (factory: obj) (definition: obj) : obj = jsNative

    [<Emit("(args, context) => $0(args)(context)")>]
    let private uncurriedExecute (fn: obj) : obj = jsNative

    [<Emit("Object.defineProperty($0, $1, { value: $2, enumerable: false })")>]
    let private defineHidden (target: obj) (name: string) (value: obj) : unit = jsNative

    [<Emit("Math.random().toString(36).slice(2, 8)")>]
    let private newHandleIdPhysical () : string = jsNative

    let newHandleId () : string = newHandleIdPhysical ()

    let private tryUnboxString (raw: obj) (name: string) =
        try
            Some(unbox<string> raw?(name))
        with _ ->
            None

    let private contextString (raw: obj) (name: string) =
        if isNull raw || isNull raw?(name) then
            None
        else
            tryUnboxString raw name |> Option.bind HostArgDecode.nonEmptyString

    let private abortSignalFromRaw (raw: obj) =
        let abort = raw?("abort")

        if not (isNull abort) then
            abort
        elif not (isNull (raw?("abortSignal"))) then
            raw?("abortSignal")
        else
            raw?("signal")

    let private abortSignalOf (raw: obj) =
        if isNull raw then null else abortSignalFromRaw raw

    let private listenAbort (signal: obj) (callback: unit -> unit) =
        let aborted = signal?("aborted")

        if not (isNull aborted) && unbox<bool> aborted then
            callback ()
            fun () -> ()
        else
            let listener = fun (_: obj) -> callback ()
            signal?addEventListener ("abort", listener, createObj [ "once", box true ])
            fun () -> signal?removeEventListener ("abort", listener)

    let private attachAbort (raw: obj) (callback: unit -> unit) =
        let signal = abortSignalOf raw

        if isNull signal || isNull (signal?("addEventListener")) then
            fun () -> ()
        else
            listenAbort signal callback

    let private partText (part: obj) =
        if isNull part || isNull part?text then
            None
        else
            Some(unbox<string> part?text)

    let private tryPartsPrompt (raw: obj) =
        try
            unbox<obj array> raw?message?parts
            |> Array.choose partText
            |> String.concat ""
            |> Option.ofObj
            |> Option.filter (String.IsNullOrWhiteSpace >> not)
        with _ ->
            None

    let private promptText (raw: obj) =
        let fromParts =
            if isNull raw || isNull raw?message || isNull raw?message?parts then
                None
            else
                tryPartsPrompt raw

        fromParts
        |> Option.orElse (contextString raw "prompt")
        |> Option.orElse (contextString raw "input")

    let decodeContext (raw: obj) =
        let callId = contextString raw "callID"
        let messageId = contextString raw "messageID"

        let callId, messageId =
            match callId, messageId with
            | Some call, Some message -> Some call, Some message
            | _ -> None, None

        { SessionId = contextString raw "sessionID" |> Option.defaultValue ""
          Agent = contextString raw "agent"
          ToolCallId = callId |> Option.map ToolCallId.create
          ProviderRunId = messageId |> Option.map ProviderRunIdentity.create
          PromptText = promptText raw
          AttachAbort = attachAbort raw }

    let factory (toolModule: obj) = HostToolFactory(toolModule?tool)

    let stringSchema (HostToolFactory factory) = HostSchema(rawStringSchema factory)

    let stringSchemaDescribed description (HostToolFactory factory) =
        HostSchema(rawStringSchemaDescribed factory description)

    let numberSchema (HostToolFactory factory) = HostSchema(rawNumberSchema factory)

    let numberSchemaDescribed description (HostToolFactory factory) =
        HostSchema(rawNumberSchemaDescribed factory description)

    let boundedIntegerSchema minimum maximum description (HostToolFactory factory) =
        HostSchema(rawBoundedIntegerSchema factory minimum maximum description)

    let boolSchema (HostToolFactory factory) = HostSchema(rawBooleanSchema factory)

    let boolSchemaDescribed description (HostToolFactory factory) =
        HostSchema(rawBooleanSchemaDescribed factory description)

    let enumSchema values (HostToolFactory factory) =
        HostSchema(rawEnumSchema factory (List.toArray values))

    let enumSchemaDescribed values description (HostToolFactory factory) =
        HostSchema(rawEnumSchemaDescribed factory (List.toArray values) description)

    let optionalEnumSchema values (HostToolFactory factory) =
        HostSchema(rawOptionalEnumSchema factory (List.toArray values))

    let optionalEnumSchemaDescribed values description (HostToolFactory factory) =
        HostSchema(rawOptionalEnumSchemaDescribed factory (List.toArray values) description)

    let managedOrHandleSchema values (HostToolFactory factory) =
        HostSchema(rawManagedOrHandleSchema factory (List.toArray values))

    let optionalStringSchema (HostToolFactory factory) =
        HostSchema(rawOptionalStringSchema factory)

    let optionalStringSchemaDescribed description (HostToolFactory factory) =
        HostSchema(rawOptionalStringSchemaDescribed factory description)

    let optionalNumberSchema (HostToolFactory factory) =
        HostSchema(rawOptionalNumberSchema factory)

    let optionalNonNegativeIntegerSchemaDescribed description (HostToolFactory factory) =
        HostSchema(rawOptionalNonNegativeIntegerSchemaDescribed factory description)

    let optionalStringArraySchema (HostToolFactory factory) =
        HostSchema(rawOptionalStringArraySchema factory)

    let stringArraySchema (HostToolFactory factory) =
        HostSchema(rawStringArraySchema factory)

    let register (HostToolFactory factory) (spec: ToolSpec) =
        let args =
            spec.Arguments
            |> List.map (fun (name, HostSchema schema) -> name, schema)
            |> createObj

        // OpenCode Host Truncate defaults to head (keep start). Custom tools
        // pre-bound here with tail retention so decision-relevant endings —
        // Final output, errors, join work_record — survive, and so Host's
        // subsequent head pass is a no-op (under 2000 lines / 50 KiB).
        let execute (rawArgs: obj) (rawContext: obj) =
            task {
                let! result = spec.Execute (HostToolArguments rawArgs) (decodeContext rawContext)
                return box (ToolResultBound.bound result)
            }

        applyTool
            factory
            (createObj
                [ "description", box spec.Description
                  "args", box args
                  "execute", uncurriedExecute (box execute) ])

    let registry (factory: HostToolFactory) (specs: ToolSpec list) =
        specs |> List.map (fun spec -> spec.Name, register factory spec) |> createObj

    let hide (registry: obj) name callback =
        defineHidden registry name (box callback)

    /// ARCH-010 data-only TOML value algebra for tool result bodies.
    type TomlValue =
        | TString of string
        | TInt of int
        | TInt64 of int64
        | TBool of bool
        | TTable of (string * TomlValue) list

    let private scalarMember name value =
        match value with
        | TString text -> Some(LlmFacing.Data.stringMember name text)
        | TInt number -> Some(LlmFacing.Data.intMember name number)
        | TInt64 number -> Some(LlmFacing.Data.int64Member name number)
        | TBool flag -> Some(LlmFacing.Data.boolMember name flag)
        | TTable _ -> None

    let rec private blocksForField (name, value) =
        match value with
        | TString text -> [ LlmFacing.Data.stringField name text ]
        | TInt number -> [ LlmFacing.Data.intField name number ]
        | TInt64 number -> [ LlmFacing.Data.int64Field name number ]
        | TBool flag -> [ LlmFacing.Data.boolField name flag ]
        | TTable entries ->
            let local =
                entries
                |> List.choose (fun (fieldName, fieldValue) -> scalarMember fieldName fieldValue)

            let nested = entries |> List.collect nestedBlocksForField
            LlmFacing.Data.table name local :: nested

    and private nestedBlocksForField (name, value) =
        match value with
        | TTable _ -> blocksForField (name, value)
        | _ -> []

    let private objectBlocks fields = fields |> List.collect blocksForField

    let tomlObject (fields: (string * TomlValue) list) : string =
        LlmFacing.empty |> LlmFacing.withData (objectBlocks fields) |> LlmFacing.render

    let tomlObjectWithInstructions (instructions: string list) (fields: (string * TomlValue) list) : string =
        LlmFacing.instructions instructions
        |> LlmFacing.withData (objectBlocks fields)
        |> LlmFacing.render

    let private tableMember (name: string, value: TomlValue) =
        match value with
        | TString text -> LlmFacing.Data.stringMember name text
        | TInt number -> LlmFacing.Data.intMember name number
        | TInt64 number -> LlmFacing.Data.int64Member name number
        | TBool flag -> LlmFacing.Data.boolMember name flag
        | TTable _ -> invalidArg "entries" "nested table values are not valid inside a table-array row"

    let tomlTable (name: string) (entries: (string * TomlValue) list list) : string =
        let blocks =
            entries
            |> List.map (fun entry -> LlmFacing.Data.tableArray name (entry |> List.map tableMember))

        LlmFacing.empty |> LlmFacing.withData blocks |> LlmFacing.render

    let looksLikeHandleId (value: string) =
        if String.IsNullOrWhiteSpace value then
            false
        else
            let trimmed = value.Trim()

            trimmed.Length = 6
            && trimmed
               |> Seq.forall (fun c -> (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))

    let digest (text: string) =
        // DSL-MUTABLE: algorithm-scratch — FNV-1a rolling hash accumulator
        let mutable hash = 2166136261u

        for c in Text.Encoding.UTF8.GetBytes text do
            // Fable lowers uint32 multiplication to a float64 multiply, which
            // drops the low bits once the product exceeds 2^53 — every step of
            // this loop. FNV-1a needs the 32-bit wrapping multiply .NET performs.
            hash <- emitJsExpr (hash ^^^ uint32 c) "(Math.imul($0, 16777619) >>> 0)"

        sprintf "fnv1a:%08x" hash
