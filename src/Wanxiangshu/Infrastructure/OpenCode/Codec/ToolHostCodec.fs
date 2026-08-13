namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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
            try
                // Fable erases unbox<obj array> to identity: a plain string would
                // iterate its CHARACTERS as items. Array-test first — a non-array
                // is absent, never a char sequence.
                let value = raw?(name)

                if emitJsExpr value "Array.isArray($0)" then
                    unbox<obj array> value
                    |> Array.choose (fun item ->
                        if isNull item then
                            None
                        else
                            string item |> Option.ofObj |> Option.filter (String.IsNullOrWhiteSpace >> not))
                    |> Array.toList
                    |> Some
                else
                    None
            with _ ->
                None

    member _.OptionalNumber(name: string) =
        if isNull raw || isNull raw?(name) then
            None
        else
            try
                // Fable erases unbox<float> to identity, so a string would pass
                // through where .NET would throw. Type-test instead: a non-number
                // is absent, never a wrong-typed Some.
                let value = raw?(name)

                if emitJsExpr value "typeof $0 === 'number'" then
                    Some(unbox<float> value)
                else
                    None
            with _ ->
                None

    member _.OptionalBool(name: string) =
        if isNull raw || isNull raw?(name) then
            None
        else
            try
                let value = raw?(name)

                if emitJsExpr value "typeof $0 === 'boolean'" then
                    Some(unbox<bool> value)
                else
                    None
            with _ ->
                None

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

type ToolSpec =
    { Name: string
      Description: string
      Arguments: (string * HostSchema) list
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

    [<Emit("$0.schema.array($0.schema.string()).optional()")>]
    let private rawOptionalStringArraySchema (tool: obj) : obj = jsNative

    [<Emit("$0($1)")>]
    let private applyTool (factory: obj) (definition: obj) : obj = jsNative

    [<Emit("(args, context) => $0(args, context)")>]
    let private uncurriedExecute (fn: obj) : obj = jsNative

    [<Emit("Object.defineProperty($0, $1, { value: $2, enumerable: false })")>]
    let private defineHidden (target: obj) (name: string) (value: obj) : unit = jsNative

    [<Emit("Math.random().toString(36).slice(2, 8)")>]
    let newHandleId () : string = jsNative

    let private contextString (raw: obj) (name: string) =
        if isNull raw || isNull raw?(name) then
            None
        else
            try
                let value = unbox<string> raw?(name)
                if String.IsNullOrWhiteSpace value then None else Some value
            with _ ->
                None

    let private attachAbort (raw: obj) (callback: unit -> unit) =
        let signal =
            if isNull raw then
                null
            else
                let abort = raw?("abort")

                if not (isNull abort) then
                    abort
                else
                    let abortSignal = raw?("abortSignal")

                    if not (isNull abortSignal) then
                        abortSignal
                    else
                        raw?("signal")

        if isNull signal || isNull (signal?("addEventListener")) then
            fun () -> ()
        else
            let aborted = signal?("aborted")

            if not (isNull aborted) && unbox<bool> aborted then
                callback ()
                fun () -> ()
            else
                let listener = fun (_: obj) -> callback ()
                signal?addEventListener ("abort", listener, createObj [ "once", box true ])
                fun () -> signal?removeEventListener ("abort", listener)

    let private promptText (raw: obj) =
        let fromParts =
            if isNull raw || isNull raw?message || isNull raw?message?parts then
                None
            else
                try
                    unbox<obj array> raw?message?parts
                    |> Array.choose (fun part ->
                        if isNull part || isNull part?text then
                            None
                        else
                            Some(unbox<string> part?text))
                    |> String.concat ""
                    |> Option.ofObj
                    |> Option.filter (String.IsNullOrWhiteSpace >> not)
                with _ ->
                    None

        fromParts
        |> Option.orElse (contextString raw "prompt")
        |> Option.orElse (contextString raw "input")

    let decodeContext (raw: obj) =
        { SessionId = contextString raw "sessionID" |> Option.defaultValue ""
          Agent = contextString raw "agent"
          ToolCallId = contextString raw "callID" |> Option.map ToolCallId.create
          ProviderRunId = contextString raw "messageID" |> Option.map ProviderRunIdentity.create
          PromptText = promptText raw
          AttachAbort = attachAbort raw }

    let factory (toolModule: obj) = HostToolFactory(toolModule?tool)

    let stringSchema (HostToolFactory factory) = HostSchema(rawStringSchema factory)

    let stringSchemaDescribed description (HostToolFactory factory) =
        HostSchema(rawStringSchemaDescribed factory description)

    let numberSchema (HostToolFactory factory) = HostSchema(rawNumberSchema factory)

    let numberSchemaDescribed description (HostToolFactory factory) =
        HostSchema(rawNumberSchemaDescribed factory description)

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

    let optionalStringArraySchema (HostToolFactory factory) =
        HostSchema(rawOptionalStringArraySchema factory)

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

    let rec private renderTomlField (name: string) (value: TomlValue) =
        match value with
        | TString text -> SyntheticToml.field name (SyntheticToml.renderString text)
        | TInt number -> SyntheticToml.field name (string number)
        | TInt64 number -> SyntheticToml.field name (string number)
        | TBool flag -> SyntheticToml.field name (if flag then "true" else "false")
        | TTable entries ->
            String.concat "\n" (("[" + name + "]") :: (entries |> List.map (fun (n, v) -> renderTomlField n v)))

    let tomlObject (fields: (string * TomlValue) list) : string =
        SyntheticToml.document [] (fields |> List.map (fun (name, value) -> renderTomlField name value))

    let tomlObjectWithInstructions (instructions: string list) (fields: (string * TomlValue) list) : string =
        SyntheticToml.document instructions (fields |> List.map (fun (name, value) -> renderTomlField name value))

    let tomlTable (name: string) (entries: (string * TomlValue) list list) : string =
        let blocks =
            entries
            |> List.map (fun entry ->
                SyntheticToml.tableArrayEntry name (entry |> List.map (fun (n, v) -> renderTomlField n v)))

        SyntheticToml.document [] blocks

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
