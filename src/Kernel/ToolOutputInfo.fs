module Wanxiangshu.Kernel.ToolOutputInfo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Kernel.ToolOutputInfoTypes

let seeBelow = ToolOutputInfoTypes.seeBelow
let seeBelowTruncated = ToolOutputInfoTypes.seeBelowTruncated
let noChangeSincePreviousReadWrite = ToolOutputInfoTypes.noChangeSincePreviousReadWrite

type ToolOutputBodyRef = ToolOutputInfoTypes.ToolOutputBodyRef
type InfoItem = ToolOutputInfoTypes.InfoItem
type ToolOutputMessage = ToolOutputInfoTypes.ToolOutputMessage

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

let private itemKey = function
    | InfoItem.Hint _ -> "hint"
    | InfoItem.Syntax _ -> "syntax"
    | InfoItem.Iterator _ -> "iterator"
    | InfoItem.Status _ -> "status"
    | InfoItem.ExitCode _ -> "exit_code"
    | InfoItem.Signal _ -> "signal"
    | InfoItem.TimeoutMs _ -> "timeout_ms"
    | InfoItem.BodyRef _ -> "tool_output"

let private itemValue = function
    | InfoItem.Hint h -> box h
    | InfoItem.Syntax s -> box s
    | InfoItem.Iterator i -> box i
    | InfoItem.Status s -> box s
    | InfoItem.ExitCode n -> box n
    | InfoItem.Signal s -> box s
    | InfoItem.TimeoutMs n -> box n
    | InfoItem.BodyRef r -> box (bodyRefValue r)

/// Order-preserving group by key: single value as scalar, multiple as array.
let private flatFields (items: InfoItem list) : FrontMatterField list =
    let rec loop acc seen items =
        match items with
        | [] -> List.rev acc
        | item :: rest ->
            let k = itemKey item
            if List.contains k seen then loop acc seen rest
            else
                let grp = items |> List.filter (fun x -> itemKey x = k) |> List.map itemValue
                let field =
                    match grp with
                    | [ v ] -> (k, v)
                    | vs -> (k, box (vs |> List.toArray))
                loop (field :: acc) (k :: seen) rest
    loop [] [] items

let render (msg: ToolOutputMessage) : string =
    if msg.info.IsEmpty && msg.body = "" then ""
    elif msg.info.IsEmpty then msg.body
    else
        let fence = frontMatter (flatFields (orderInfoForRender msg.info))
        match msg.body with
        | "" -> fence
        | body -> fence + "\n" + body

open Wanxiangshu.Kernel.ToolOutputInfoParse

let tryParse (text: string) : ToolOutputMessage option =
    if isNull text || text = "" then None
    else
        let lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
        if lines.Length < 2 || lines.[0] <> "---" then None
        else
            let parsed = parseFrontMatter text
            if isNull parsed then None
            else
                let body = bodyAfterFrontMatter text
                let info = parseInfoItems parsed
                Some { info = info; body = body }

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