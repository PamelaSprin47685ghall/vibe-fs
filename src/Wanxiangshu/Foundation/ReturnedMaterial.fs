namespace Wanxiangshu.Foundation

/// Delivered material crosses persistence and wait cells as one canonical
/// JSON object separating main prose (body) from structured carry material.
/// Decode only here; render TOML only at a presentation boundary, so rendered
/// TOML can never become another quoted data field.
///
/// AXIOM 7: prose -> `# ` comment header (whole prose verbatim); carry and
/// other material -> data field body; one blank line between header and body;
/// parseable TOML; report body never triple-quoted. The body/carry split is
/// structural here and is never inferred from the text content.
[<RequireQualifiedAccess>]
module ReturnedMaterial =
    open Fable.Core

    [<AllowNullLiteral>]
    type private TextEntry =
        abstract text: obj

    [<Emit("Array.isArray($0)")>]
    let private isArray (value: obj) : bool = jsNative

    [<Emit("typeof $0")>]
    let private valueType (value: obj) : string = jsNative

    [<Emit("$0 === null")>]
    let private isNull (value: obj) : bool = jsNative

    [<Emit("({ body: $0, carry: $1 })")>]
    let private createContainer (body: string array) (carry: string array) : obj = jsNative

    [<Emit("$0[$1]")>]
    let private getProperty (target: obj) (name: string) : obj = jsNative

    let private stringListOf (label: string) (value: obj) : string list =
        if not (isArray value) then
            invalidArg "encoded" (sprintf "returned material %s must be a JSON string array" label)

        let values: obj array = unbox value

        if values |> Array.exists (fun item -> valueType item <> "string") then
            invalidArg "encoded" (sprintf "returned material %s must contain only strings" label)

        values |> Array.map unbox<string> |> Array.toList

    /// Canonical storage: a JSON object `{ "body": [...], "carry": [...] }`.
    /// `body` is the main prose (report/instructions); `carry` is structured
    /// material. Both are string lists. The distinction is explicit and never
    /// read back from what the text looks like.
    let encode (body: string list) (carry: string list) : string =
        JS.JSON.stringify (createContainer (body |> List.toArray) (carry |> List.toArray))

    let private decoded (encoded: string) : obj =
        let value: obj = JS.JSON.parse encoded

        if isNull value || valueType value <> "object" || isArray value then
            invalidArg "encoded" "returned material must be a JSON object"

        value

    let body (encoded: string) : string list =
        let value = decoded encoded
        let b = getProperty value "body"
        if isNull b then [] else stringListOf "body" b

    let carry (encoded: string) : string list =
        let value = decoded encoded
        let c = getProperty value "carry"
        if isNull c then [] else stringListOf "carry" c

    let validate (encoded: string) : Result<unit, string> =
        let isStringArray (b: obj) =
            if isNull b then
                true
            elif not (isArray b) then
                false
            else
                let arr: obj array = unbox b
                Array.forall (fun item -> not (isNull item) && valueType item = "string") arr

        let isCarryArray (c: obj) =
            if isNull c || not (isArray c) then
                false
            else
                let arr: obj array = unbox c
                Array.forall (fun item -> not (isNull item) && valueType item = "string") arr

        let parsedOpt =
            try
                Some(decoded encoded)
            with _ ->
                None

        let canonicalValue value =
            let b = getProperty value "body"
            let c = getProperty value "carry"

            if not (isStringArray b) then
                None
            elif isCarryArray c then
                Some(encode (body encoded) (carry encoded))
            else
                None

        let canonical = parsedOpt |> Option.bind canonicalValue

        match canonical with
        | Some value when value = encoded -> Ok()
        | Some _ -> Error "returned material must use the canonical JSON object encoding"
        | None -> Error "returned material must be a canonical { body: string[], carry: string[] } JSON object"

    let entries (encoded: string) : obj array =
        carry encoded |> List.map (fun text -> box {| text = text |}) |> List.toArray

    let private entryText (entry: obj) : string =
        if isNull entry || valueType entry <> "object" then
            invalidArg "entries" "each returned entry must have a text string"

        let text = (unbox<TextEntry> entry).text

        if valueType text <> "string" then
            invalidArg "entries" "each returned entry must have a text string"

        unbox<string> text

    let ofEntries (entries: obj) : string =
        if not (isArray entries) then
            invalidArg "entries" "returned must be an array of text entries"

        (unbox<obj array> entries)
        |> Array.map entryText
        |> Array.toList
        |> fun carry -> encode [] carry

    /// Carry material as the join `[[returned]]` data plane.
    let carryData (encoded: string) : LlmFacing.DataBlock list =
        carry encoded |> LlmFacing.Data.textTables "returned"

    /// The whole delivered view: body prose as a `# ` comment header, carry
    /// material as `[[returned]]` data after the one blank line.
    let render (encoded: string) : string =
        LlmFacing.instructions (body encoded)
        |> LlmFacing.withData (carryData encoded)
        |> LlmFacing.render

    /// The main prose as a `# ` comment header only.
    let renderBody (encoded: string) : string =
        LlmFacing.instructions (body encoded) |> LlmFacing.render

    /// Body and carry as raw text blocks (not rendered `# ` bytes), joined for
    /// consumers that present the whole delivered content verbatim.
    let fullText (encoded: string) : string =
        body encoded @ carry encoded |> String.concat "\n\n"
