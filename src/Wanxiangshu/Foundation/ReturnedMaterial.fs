namespace Wanxiangshu.Foundation

/// Delivered material crosses persistence and wait cells as one canonical
/// JSON string array. Decode only here; render TOML only at a presentation
/// boundary, so rendered TOML can never become another quoted data field.
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

    let ofTexts (texts: string list) : string =
        texts |> List.toArray |> JS.JSON.stringify

    let texts (encoded: string) : string list =
        let value: obj = JS.JSON.parse encoded

        if not (isArray value) then
            invalidArg "encoded" "returned material must be a JSON string array"

        let values: obj array = unbox value

        if values |> Array.exists (fun item -> valueType item <> "string") then
            invalidArg "encoded" "returned material must contain only strings"

        values |> Array.map unbox<string> |> Array.toList

    let validate (encoded: string) : Result<unit, string> =
        let canonical =
            try
                Some(ofTexts (texts encoded))
            with _ ->
                None

        match canonical with
        | Some value when value = encoded -> Ok()
        | Some _ -> Error "returned material must use the canonical JSON string array encoding"
        | None -> Error "returned material must be a canonical JSON string array"

    let entries (encoded: string) : obj array =
        texts encoded |> List.map (fun text -> box {| text = text |}) |> List.toArray

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

        (unbox<obj array> entries) |> Array.map entryText |> Array.toList |> ofTexts

    let data (encoded: string) : LlmFacing.DataBlock list =
        texts encoded |> LlmFacing.Data.textTables "returned"

    let render (encoded: string) : string =
        LlmFacing.empty |> LlmFacing.withData (data encoded) |> LlmFacing.render
