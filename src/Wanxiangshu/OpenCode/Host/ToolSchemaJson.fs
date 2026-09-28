namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

/// Rendering of a Host tool's Effect argument schema into the provider-visible
/// JSON schema, mirroring opencode 1.18.32 `packages/opencode/src/tool/json-schema.ts`
/// (`fromSchema`): `Schema.toJsonSchemaDocument(schema, { additionalProperties: true })`,
/// `$schema`/`$defs` assembly, `normalize`, `inlineLocalReferences`,
/// `dropDefinitionsIfResolved`. Keeping this conversion in the plugin is what
/// lets a decorated tool be published through `output.jsonSchema` while the
/// Effect schema the Host decodes arguments with stays untouched.
module ToolSchemaJson =

    [<Literal>]
    let private metaSchema = "https://json-schema.org/draft/2020-12/schema"

    // DSL-MUTABLE: single-flight — Effect Schema renderer resolved in Load Phase
    let mutable private renderer: obj = null
    // DSL-MUTABLE: single-flight — held Load Phase failure, raised only when a schema must be rendered
    let mutable private rendererFailure: string = null

    [<Emit("import('effect')")>]
    let private importEffect () : Task<obj> = jsNative

    [<Emit("$0 == null ? undefined : $0[$1]")>]
    let private field (carrier: obj) (name: string) : obj = jsNative

    [<Emit("$0[$1] === undefined")>]
    let private isAbsent (carrier: obj) (name: string) : bool = jsNative

    [<Emit("typeof $0 === 'object' && $0 !== null && !Array.isArray($0)")>]
    let private isRecord (value: obj) : bool = jsNative

    [<Emit("Array.isArray($0)")>]
    let private isArray (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'boolean'")>]
    let private isBoolean (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'function'")>]
    let private isFunction (value: obj) : bool = jsNative

    [<Emit("Object.entries($0)")>]
    let private entries (value: obj) : obj array = jsNative

    [<Emit("Object.fromEntries($0)")>]
    let private fromEntries (pairs: obj array) : obj = jsNative

    [<Emit("delete $0[$1]")>]
    let private deleteKey (target: obj) (key: string) : unit = jsNative

    [<Emit("({ ...$0, ...$1 })")>]
    let private merge (left: obj) (right: obj) : obj = jsNative

    [<Emit("Object.assign({}, ...$0)")>]
    let private mergeAll (values: obj array) : obj = jsNative

    [<Emit("(() => { const { [$1]: omitted, ...rest } = $0; return rest; })()")>]
    let private withoutKey (value: obj) (key: string) : obj = jsNative

    [<Emit("$0.toJsonSchemaDocument($1, { additionalProperties: true })")>]
    let private toJsonSchemaDocument (renderer: obj) (schema: obj) : obj = jsNative

    [<Emit("({ $schema: $1, ...$0 })")>]
    let private withMetaSchema (value: obj) (meta: string) : obj = jsNative

    [<Emit("$1 != null && Object.keys($1).length > 0 ? { ...$0, $defs: $1 } : $0")>]
    let private withDefinitions (value: obj) (definitions: obj) : obj = jsNative

    [<Emit("({ minimum: Number.MIN_SAFE_INTEGER, ...$0, maximum: Number.MAX_SAFE_INTEGER })")>]
    let private withIntegerBounds (value: obj) : obj = jsNative

    [<Emit("new WeakMap()")>]
    let private newWeakMap () : obj = jsNative

    [<Emit("$0.get($1)")>]
    let private weakGet (map: obj) (key: obj) : obj = jsNative

    [<Emit("$0.set($1, $2)")>]
    let private weakSet (map: obj) (key: obj) (value: obj) : unit = jsNative

    /// Rendered schemas are cached per schema object, exactly as the Host caches
    /// its own conversion.
    let private cache = newWeakMap ()

    let private entryKey (entry: obj) : string =
        let pair = unbox<obj array> entry
        string pair[0]

    let private entryValue (entry: obj) : obj =
        let pair = unbox<obj array> entry
        pair[1]

    let private typeNameOf (value: obj) : string =
        if isNull (field value "type") then
            ""
        else
            string (field value "type")

    let private isNullTypeMember (value: obj) : bool =
        isRecord value && typeNameOf value = "null"

    let private isNumberMember (value: obj) : bool =
        isRecord value && typeNameOf value = "number"

    let private isObjectTypeMember (value: obj) : bool =
        isRecord value && typeNameOf value = "object" && isAbsent value "properties"

    let private isArrayTypeMember (value: obj) : bool =
        isRecord value && typeNameOf value = "array" && isAbsent value "items"

    let private isEmptyStructUnion (members: obj array) : bool =
        members.Length = 2
        && (members |> Array.exists isObjectTypeMember)
        && (members |> Array.exists isArrayTypeMember)

    let private isNonFiniteNumber (value: obj) : bool =
        isString value
        && (let text = string value in text = "NaN" || text = "Infinity" || text = "-Infinity")

    let private isNonFiniteEnumMember (value: obj) : bool =
        isRecord value
        && (match field value "enum" with
            | members when isArray members -> unbox<obj array> members |> Array.forall isNonFiniteNumber
            | _ -> false)

    let private requiredNames (schema: obj) : Set<string> option =
        match field schema "required" with
        | required when isArray required ->
            unbox<obj array> required
            |> Array.choose (fun item -> if isString item then Some(string item) else None)
            |> Set.ofArray
            |> Some
        | _ -> None

    let private canFlattenAllOf (members: obj array) (parent: obj) : bool =
        let mutable pushed =
            entries parent
            |> Array.map entryKey
            |> Array.filter (fun key -> key <> "allOf")
            |> Set.ofArray

        members
        |> Array.forall (fun member' ->
            entries member'
            |> Array.forall (fun entry ->
                let key = entryKey entry

                if Set.contains key pushed then
                    false
                else
                    pushed <- Set.add key pushed
                    true))

    let rec private normalizeWith (stripNull: bool) (value: obj) : obj =
        if isArray value then
            unbox<obj array> value |> Array.map (normalizeWith false) |> box
        elif not (isRecord value) then
            value
        else
            let schema = buildSchema value

            if field schema "additionalProperties" = box true then
                deleteKey schema "additionalProperties"

            stripNullMembers stripNull schema

    and private buildSchema (value: obj) : obj =
        let required = requiredNames value

        entries value
        |> Array.map (fun entry ->
            let key = entryKey entry
            let item = entryValue entry

            if key = "properties" && isRecord item then
                let properties =
                    entries item
                    |> Array.map (fun property ->
                        let name = entryKey property

                        let strip =
                            match required with
                            | Some names -> not (Set.contains name names)
                            | None -> true

                        box [| box name; normalizeWith strip (entryValue property) |])

                box [| box key; fromEntries properties |]
            else
                box [| box key; normalizeWith false item |])
        |> fromEntries

    and private stripNullMembers (stripNull: bool) (schema: obj) : obj =
        let anyOf = field schema "anyOf"

        let withoutNull =
            if stripNull && isArray anyOf then
                let members = unbox<obj array> anyOf
                let kept = members |> Array.filter (fun item -> not (isNullTypeMember item))
                if kept.Length <> members.Length then Some kept else None
            else
                None

        match withoutNull with
        | Some kept -> normalizeWith false (merge schema (createObj [ "anyOf" ==> box kept ]))
        | None -> normalizeAnyOf schema

    and private normalizeAnyOf (schema: obj) : obj =
        let anyOf = field schema "anyOf"

        if not (isArray anyOf) then
            normalizeAllOf schema
        else
            let members = unbox<obj array> anyOf
            let number = members |> Array.tryFind isNumberMember
            let nonFinite = members |> Array.filter isNonFiniteEnumMember
            let rest = withoutKey schema "anyOf"

            if number.IsSome && nonFinite.Length = members.Length - 1 then
                normalizeWith false (merge number.Value rest)
            elif isEmptyStructUnion members then
                normalizeWith false (merge (createObj [ "type" ==> box "object"; "properties" ==> createObj [] ]) rest)
            elif members.Length = 1 && isRecord members[0] then
                normalizeWith false (merge members[0] rest)
            else
                normalizeAllOf schema

    and private normalizeAllOf (schema: obj) : obj =
        let allOf = field schema "allOf"

        if isArray allOf then
            let members = unbox<obj array> allOf

            if members |> Array.forall isRecord && canFlattenAllOf members schema then
                normalizeWith false (merge (mergeAll members) (withoutKey schema "allOf"))
            else
                normalizeIntegerBounds schema
        else
            normalizeIntegerBounds schema

    and private normalizeIntegerBounds (schema: obj) : obj =
        if typeNameOf schema = "integer" && isAbsent schema "maximum" then
            withIntegerBounds schema
        else
            schema

    let rec private inlineLocalReferences (value: obj) (definitions: obj) (seen: Set<string>) : obj =
        if isArray value then
            unbox<obj array> value
            |> Array.map (fun item -> inlineLocalReferences item definitions seen)
            |> box
        elif not (isRecord value) then
            value
        else
            let localDefinitions =
                if not (isNull definitions) then
                    definitions
                else
                    match field value "$defs" with
                    | defs when isRecord defs -> defs
                    | _ -> null

            let reference = field value "$ref"

            let inlined =
                if isString reference && not (isNull localDefinitions) then
                    match localReferenceName (string reference) with
                    | Some name when not (Set.contains name seen) ->
                        let target = field localDefinitions name

                        if isNull target then
                            None
                        else
                            let base' = if isRecord target then target else createObj []

                            Some(
                                inlineLocalReferences
                                    (merge base' (withoutKey value "$ref"))
                                    localDefinitions
                                    (Set.add name seen)
                            )
                    | _ -> None
                else
                    None

            match inlined with
            | Some resolved -> resolved
            | None ->
                entries value
                |> Array.map (fun entry ->
                    box
                        [| box (entryKey entry)
                           inlineLocalReferences (entryValue entry) localDefinitions seen |])
                |> fromEntries

    and private localReferenceName (reference: string) : string option =
        if reference.StartsWith "#/$defs/" then
            Some(reference.Substring 8)
        elif reference.StartsWith "#/definitions/" then
            Some(reference.Substring 14)
        else
            None

    let rec private scanForLocalReference (value: obj) : bool =
        if isArray value then
            unbox<obj array> value |> Array.exists scanForLocalReference
        elif not (isRecord value) then
            false
        else
            let reference = field value "$ref"

            let isLocalReference =
                isString reference
                && (let text = string reference in text.StartsWith "#/$defs/" || text.StartsWith "#/definitions/")

            isLocalReference
            || (entries value
                |> Array.exists (fun entry -> scanForLocalReference (entryValue entry)))

    let private dropDefinitionsIfResolved (value: obj) : obj =
        if not (isRecord value) || scanForLocalReference value then
            value
        else
            withoutKey (withoutKey value "$defs") "definitions"

    let private isJsonSchema (value: obj) : bool = isRecord value || isBoolean value

    let initialize () : Task =
        task {
            try
                let! moduleObj = importEffect ()
                let schema = if isNull moduleObj then null else field moduleObj "Schema"

                if isNull schema || not (isFunction (field schema "toJsonSchemaDocument")) then
                    rendererFailure <- "the `effect` package does not expose Schema.toJsonSchemaDocument"
                else
                    renderer <- schema
            with ex ->
                rendererFailure <- string ex
        }
        :> Task

    let private render (parameters: obj) : obj =
        if isNull renderer then
            invalidOp (
                sprintf
                    "wanxiangshu tool-definition: the Effect schema renderer is unavailable (%s); the readonly delegation protocol cannot decorate this tool schema"
                    (if isNull rendererFailure then
                         "ToolSchemaJson.initialize was never run"
                     else
                         rendererFailure)
            )

        let document = toJsonSchemaDocument renderer parameters

        let rendered =
            withMetaSchema (withDefinitions (field document "schema") (field document "definitions")) metaSchema
            |> normalizeWith false
            |> fun value -> inlineLocalReferences value null Set.empty
            |> dropDefinitionsIfResolved

        if not (isJsonSchema rendered) then
            invalidOp "wanxiangshu tool-definition: rendering the tool's Effect schema produced a non-schema value"

        rendered

    let providerSchema (parameters: obj) : obj =
        let cached = weakGet cache parameters

        if not (isNull cached) then
            cached
        else
            let rendered = render parameters
            weakSet cache parameters rendered
            rendered
