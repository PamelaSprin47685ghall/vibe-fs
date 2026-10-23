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

    [<Emit("({ body: $0, carry: $1, session: $2 })")>]
    let private createContainerWithSession (body: string array) (carry: string array) (session: obj array) : obj = jsNative

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

    let encodeSession (body: string list) (session: obj array) : string =
        let sess = if isNull session then [||] else session
        JS.JSON.stringify (createContainerWithSession (body |> List.toArray) [||] sess)

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

    let hasSession (encoded: string) : bool =
        try
            let value = decoded encoded
            let s = getProperty value "session"
            not (isNull s) && isArray s
        with _ -> false

    let session (encoded: string) : obj array =
        try
            let value = decoded encoded
            let s = getProperty value "session"
            if not (isNull s) && isArray s then unbox<obj array> s else [||]
        with _ -> [||]

    let validate (encoded: string) : Result<unit, string> =
        let isStringArray (b: obj) =
            if isNull b then true
            elif not (isArray b) then false
            else
                let arr: obj array = unbox b
                Array.forall (fun item -> not (isNull item) && valueType item = "string") arr

        let isSessionArray (s: obj) =
            if isNull s || not (isArray s) then false
            else
                let arr: obj array = unbox s
                Array.forall (fun item -> not (isNull item) && valueType item = "object" && not (isArray item)) arr

        let isCarryArray (c: obj) =
            if isNull c || not (isArray c) then false
            else
                let arr: obj array = unbox c
                Array.forall (fun item -> not (isNull item) && valueType item = "string") arr

        let parsedOpt =
            try
                Some(decoded encoded)
            with _ ->
                None

        let canonical =
            match parsedOpt with
            | None -> None
            | Some value ->
                let b = getProperty value "body"
                let c = getProperty value "carry"
                let s = getProperty value "session"
                if not (isStringArray b) then None
                elif isSessionArray s then Some(encodeSession (body encoded) (session encoded))
                elif isCarryArray c then Some(encode (body encoded) (carry encoded))
                else None

        match canonical with
        | Some value when value = encoded -> Ok()
        | Some _ -> Error "returned material must use the canonical JSON object encoding"
        | None -> Error "returned material must be a canonical { body: string[], carry: string[] } or { body: string[], session: object[] } JSON object"

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

    let private serializeStringOrJson (v: obj) : string =
        if valueType v = "string" then unbox<string> v
        else JS.JSON.stringify v

    let private pickStringOrJson (primary: obj) (fallback: obj) (defaultVal: string) : string =
        if not (isNull primary) then serializeStringOrJson primary
        elif not (isNull fallback) then serializeStringOrJson fallback
        else defaultVal

    let private pickCallId (item: obj) : string =
        let idObj = getProperty item "id"
        if not (isNull idObj) then string idObj
        else
            let c = getProperty item "callID"
            if not (isNull c) then string c
            else
                let c2 = getProperty item "callId"
                if not (isNull c2) then string c2 else ""

    let private renderToolCallDataBlock (tableName: string) (item: obj) : LlmFacing.DataBlock =
        let nameObj = getProperty item "name"
        let name =
            if not (isNull nameObj) then string nameObj
            else
                let t = getProperty item "tool"
                if not (isNull t) then string t else ""
        let args = pickStringOrJson (getProperty item "arguments") (getProperty item "input") "{}"
        let callId = pickCallId item
        LlmFacing.Data.tableArray tableName [
            LlmFacing.Data.stringMember "role" "assistant"
            LlmFacing.Data.stringMember "tool" name
            LlmFacing.Data.stringMember "arguments" args
            LlmFacing.Data.stringMember "callId" callId
        ]

    let private renderToolResultDataBlock (tableName: string) (item: obj) : LlmFacing.DataBlock =
        let res = pickStringOrJson (getProperty item "result") (getProperty item "output") ""
        let callId = pickCallId item
        LlmFacing.Data.tableArray tableName [
            LlmFacing.Data.stringMember "role" "toolResult"
            LlmFacing.Data.stringMember "result" res
            LlmFacing.Data.stringMember "callId" callId
        ]

    let private renderTextDataBlock (tableName: string) (role: string) (item: obj) : LlmFacing.DataBlock =
        let textObj = getProperty item "text"
        let text =
            if not (isNull textObj) && valueType textObj = "string" then unbox<string> textObj
            else
                let c = getProperty item "content"
                if not (isNull c) && valueType c = "string" then unbox<string> c
                else ""
        LlmFacing.Data.tableArray tableName [
            LlmFacing.Data.stringMember "role" role
            LlmFacing.Data.stringMember "text" text
        ]

    let private renderTypedItemDataBlock (tableName: string) (role: string) (item: obj) : LlmFacing.DataBlock =
        let itemTypeObj = getProperty item "type"
        let itemType = if not (isNull itemTypeObj) then string itemTypeObj else "text"
        match itemType with
        | "toolCall" | "tool-call" | "tool_call" | "tool" -> renderToolCallDataBlock tableName item
        | "toolResult" | "tool-result" | "tool_result" -> renderToolResultDataBlock tableName item
        | _ -> renderTextDataBlock tableName role item

    let private renderItemDataBlock (tableName: string) (role: string) (item: obj) : LlmFacing.DataBlock =
        if isNull item then
            LlmFacing.Data.tableArray tableName [ LlmFacing.Data.stringMember "role" role ]
        else
            renderTypedItemDataBlock tableName role item

    let private renderMessageDataBlocks (tableName: string) (msg: obj) : LlmFacing.DataBlock list =
        if isNull msg then []
        else
            let roleObj = getProperty msg "role"
            let role = if not (isNull roleObj) then string roleObj else "user"
            let content = getProperty msg "content"
            let parts = getProperty msg "parts"
            let items =
                if isArray content then unbox<obj array> content
                elif isArray parts then unbox<obj array> parts
                else [||]

            if items.Length = 0 then
                let text =
                    if valueType content = "string" then unbox<string> content
                    else
                        let t = getProperty msg "text"
                        if valueType t = "string" then unbox<string> t else ""
                [ LlmFacing.Data.tableArray tableName [
                    LlmFacing.Data.stringMember "role" role
                    LlmFacing.Data.stringMember "text" text
                ] ]
            else
                items |> Array.toList |> List.map (renderItemDataBlock tableName role)

    let private messagesToDataBlocks (tableName: string) (messages: obj array) : LlmFacing.DataBlock list =
        if isNull messages || messages.Length = 0 then []
        else messages |> Array.toList |> List.collect (renderMessageDataBlocks tableName)

    let sessionData (tableName: string) (encoded: string) : LlmFacing.DataBlock list =
        messagesToDataBlocks tableName (session encoded)

    /// Carry material as the join `[[returned]]` data plane.
    let carryData (encoded: string) : LlmFacing.DataBlock list =
        if hasSession encoded then
            sessionData "session" encoded
        else
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
        if hasSession encoded then
            render encoded
        else
            body encoded @ carry encoded |> String.concat "\n\n"
