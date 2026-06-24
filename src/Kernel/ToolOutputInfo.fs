module VibeFs.Kernel.ToolOutputInfo

open VibeFs.Kernel.PromptFrontMatter

[<RequireQualifiedAccess>]
type ToolOutputBodyRef =
    | SeeBelow
    | SeeBelowTruncated
    | NoChangeSincePreviousReadWrite

let seeBelow = "/See Below/"
let seeBelowTruncated = "/See Below, Truncated/"
let noChangeSincePreviousReadWrite = "/No Change Since Previous Read/Write/"

let hintExecutorMisuse =
    "No executor for reading, searching, writing files. Use read/investigator/coder!"

let hintTodoRefresh = "Update todo list NOW and settle down your progress!"

let hintMeditator =
    "Think thrice before acting NOW and consider calling meditator tool to improve reasoning!"

let hintTodosUpdated = "Todos updated."

let hintMethodologyFollowup (methodologyId: string) =
    $"Great! Now apply {methodologyId} to actual work NOW and you MUST think over and then call methodology_{methodologyId} tool asap!"

let hintForMethodologies (methodologies: string list) : string =
    match methodologies with
    | [] -> hintTodosUpdated
    | names -> names |> List.map hintMethodologyFollowup |> String.concat " "

type InfoItem =
    | Hint of string
    | Syntax of string
    | Iterator of string
    | Status of string
    | ExitCode of int
    | Signal of string
    | TimeoutMs of int
    | BodyRef of ToolOutputBodyRef

type ToolOutputMessage = { info: InfoItem list; body: string }

let empty = { info = []; body = "" }

let private normalizedBody (s: string) =
    if isNull s then "" else s

let withBody body = { empty with body = normalizedBody body }

let appendInfo item (msg: ToolOutputMessage) : ToolOutputMessage =
    { msg with info = msg.info @ [ item ] }

let setBodyRef ref' (msg: ToolOutputMessage) : ToolOutputMessage =
    let without =
        msg.info |> List.filter (function InfoItem.BodyRef _ -> false | _ -> true)
    { msg with info = without @ [ InfoItem.BodyRef ref' ] }

let bodyRefValue = function
    | ToolOutputBodyRef.SeeBelow -> seeBelow
    | ToolOutputBodyRef.SeeBelowTruncated -> seeBelowTruncated
    | ToolOutputBodyRef.NoChangeSincePreviousReadWrite -> noChangeSincePreviousReadWrite

let private orderInfoForRender (items: InfoItem list) : InfoItem list =
    let bodyRefs, rest =
        items |> List.partition (function InfoItem.BodyRef _ -> true | _ -> false)
    rest @ bodyRefs

let private renderInfoItem = function
    | InfoItem.Hint h -> yamlListItemField "hint" h "  "
    | InfoItem.Syntax s -> yamlListItemField "syntax" s "  "
    | InfoItem.Iterator i -> yamlListItemField "iterator" i "  "
    | InfoItem.Status s -> yamlListItemField "status" s "  "
    | InfoItem.ExitCode n -> yamlListItemField "exit_code" (string n) "  "
    | InfoItem.Signal s -> yamlListItemField "signal" s "  "
    | InfoItem.TimeoutMs n -> yamlListItemField "timeout_ms" (string n) "  "
    | InfoItem.BodyRef r -> yamlListItemField "tool_output" (bodyRefValue r) "  "

let render (msg: ToolOutputMessage) : string =
    if msg.info.IsEmpty && msg.body = "" then ""
    elif msg.info.IsEmpty then msg.body
    else
        let infoBlock = yamlSeqField "info" (orderInfoForRender msg.info |> List.map renderInfoItem)
        let fence = frontMatter [ infoBlock ]
        match msg.body with
        | "" -> fence
        | body -> fence + "\n" + body

let tryParseInfoList (lines: string[]) (startIndex: int) : InfoItem list * int option =
    let rec parseItem i acc =
        if i >= lines.Length then acc, None
        elif lines.[i] = "---" then acc, Some i
        elif not (lines.[i].StartsWith("  - ")) then acc, Some i
        else
            let rest = lines.[i].Substring(4)
            let sep = rest.IndexOf(": ")
            if sep <= 0 then parseItem (i + 1) acc
            else
                let key = rest.Substring(0, sep)
                let raw = rest.Substring(sep + 2)
                let item, next =
                    match key, raw with
                    | "hint", r ->
                        match parseYamlStringValue r with
                        | Some v -> Some(InfoItem.Hint v), i + 1
                        | None when r = "|" ->
                            let v, ni = readBlockAt lines (i + 1) "    "
                            Some(InfoItem.Hint v), ni
                        | None -> Some(InfoItem.Hint (r.Trim())), i + 1
                    | "syntax", r ->
                        match parseYamlStringValue r with
                        | Some v -> Some(InfoItem.Syntax v), i + 1
                        | None when r = "|" ->
                            let v, ni = readBlockAt lines (i + 1) "    "
                            Some(InfoItem.Syntax v), ni
                        | None -> Some(InfoItem.Syntax (r.Trim())), i + 1
                    | "iterator", r -> Some(InfoItem.Iterator (parseScalarTail r)), i + 1
                    | "status", r -> Some(InfoItem.Status (parseScalarTail r)), i + 1
                    | "exit_code", r ->
                        let t = parseScalarTail r
                        match System.Int32.TryParse t with
                        | true, n -> Some(InfoItem.ExitCode n), i + 1
                        | false, _ -> None, i + 1
                    | "signal", r -> Some(InfoItem.Signal (parseScalarTail r)), i + 1
                    | "timeout_ms", r ->
                        let t = parseScalarTail r
                        match System.Int32.TryParse t with
                        | true, n -> Some(InfoItem.TimeoutMs n), i + 1
                        | false, _ -> None, i + 1
                    | "tool_output", r ->
                        let t = parseScalarTail r
                        let ref' =
                            if t = seeBelow then ToolOutputBodyRef.SeeBelow
                            elif t = seeBelowTruncated then ToolOutputBodyRef.SeeBelowTruncated
                            elif t = noChangeSincePreviousReadWrite then ToolOutputBodyRef.NoChangeSincePreviousReadWrite
                            else ToolOutputBodyRef.SeeBelow
                        Some(InfoItem.BodyRef ref'), i + 1
                    | _ -> None, i + 1
                match item with
                | Some it -> parseItem next (acc @ [ it ])
                | None -> parseItem (i + 1) acc
    and readBlockAt (lines: string[]) start (prefix: string) =
        let rec gather j acc =
            if j >= lines.Length then String.concat "\n" (List.rev acc), j
            elif lines.[j].StartsWith prefix then
                gather (j + 1) (lines.[j].Substring(prefix.Length) :: acc)
            else String.concat "\n" (List.rev acc), j
        gather start []
    and parseScalarTail raw =
        match parseYamlStringValue raw with
        | Some v -> v
        | None -> raw.Trim()
    if startIndex >= lines.Length then [], None
    elif lines.[startIndex] <> "info:" then [], None
    else parseItem (startIndex + 1) []

let tryParse (text: string) : ToolOutputMessage option =
    if isNull text || text = "" then None
    else
        let lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
        if lines.Length < 2 || lines.[0] <> "---" then None
        else
            let infoLineIndex =
                lines
                |> Array.mapi (fun i line -> i, line)
                |> Array.tryFind (fun (_, line) -> line = "info:")
                |> Option.map fst
            match infoLineIndex with
            | None ->
                let close =
                    lines.[1..]
                    |> Array.tryFindIndex ((=) "---")
                    |> Option.map (fun i -> i + 1)
                match close with
                | Some ci ->
                    let body =
                        if ci + 1 >= lines.Length then ""
                        else String.concat "\n" lines.[ci + 1 ..]
                    Some { info = []; body = body }
                | None -> None
            | Some iInfo ->
                let info, closeOpt = tryParseInfoList lines iInfo
                match closeOpt with
                | Some ci ->
                    let body =
                        if ci + 1 >= lines.Length then ""
                        else String.concat "\n" lines.[ci + 1 ..]
                    Some { info = info; body = body }
                | None -> None

let bodyForBookkeeper (text: string) : string =
    match tryParse text with
    | Some msg -> msg.body
    | None -> text

let hintsFromOutput (text: string) : string list =
    match tryParse text with
    | Some msg ->
        msg.info |> List.choose (function InfoItem.Hint h -> Some h | _ -> None)
    | None -> []

/// True only when front matter parses and some `hint:` item equals `expected` exactly.
let hasExactHint (text: string) (expected: string) : bool =
    hintsFromOutput text |> List.exists ((=) expected)

/// Parsed `hint:` items only; no body fallback. Prefer `hasExactHint` for contracts.
let hintTextContains (text: string) (fragment: string) : bool =
    hintsFromOutput text |> List.exists (fun h -> h.Contains fragment)

let noChangeEnvelope () =
    render { info = [ InfoItem.BodyRef ToolOutputBodyRef.NoChangeSincePreviousReadWrite ]; body = "" }

let seeBelowEnvelope body =
    render { info = [ InfoItem.BodyRef ToolOutputBodyRef.SeeBelow ]; body = normalizedBody body }

let parseOrBody (raw: string) : ToolOutputMessage =
    match tryParse raw with
    | Some msg -> msg
    | None -> { empty with body = normalizedBody raw }

let withBookkeepingHints (raw: string) : string =
    parseOrBody raw
    |> appendInfo (InfoItem.Hint hintTodoRefresh)
    |> setBodyRef ToolOutputBodyRef.SeeBelow
    |> render

let addSyntax (raw: string) (syntax: string) : string =
    if syntax = "" then raw
    else
        parseOrBody raw
        |> appendInfo (InfoItem.Syntax syntax)
        |> setBodyRef ToolOutputBodyRef.SeeBelow
        |> render

let withIterator (body: string) (iterator: string) : string =
    if iterator = "" then body
    else
        render
            { empty with
                info = [ InfoItem.Iterator iterator; InfoItem.BodyRef ToolOutputBodyRef.SeeBelow ]
                body = body }

let todoWriteOutput (methodologies: string list) (includeMeditator: bool) : string =
    let hints =
        [ InfoItem.Hint (hintForMethodologies methodologies) ]
        @ (if includeMeditator then [ InfoItem.Hint hintMeditator ] else [])
    render
        { empty with
            info = hints @ [ InfoItem.BodyRef ToolOutputBodyRef.SeeBelow ]
            body = "" }