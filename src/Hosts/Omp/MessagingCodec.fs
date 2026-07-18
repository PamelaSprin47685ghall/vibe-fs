module Wanxiangshu.Hosts.Omp.MessagingCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.MessagingDecode
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.MessagingCodecDecode

module Dyn = Wanxiangshu.Runtime.Dyn

let private toObjArray (value: obj) : obj array =
    if Dyn.isNullish value || not (Dyn.isArray value) then
        [||]
    else
        unbox<obj array> value

let private textFromParts (parts: obj) : string =
    if not (Dyn.isArray parts) then
        ""
    else
        toObjArray parts
        |> Array.choose (fun part ->
            if Dyn.str part "type" = "text" then
                let t = Dyn.get part "text"
                if Dyn.isNullish t then None else Some(string t)
            else
                None)
        |> String.concat "\n\n"

let entries (sessionManager: ISessionManager) : obj array =
    let smObj = box sessionManager

    if Dyn.typeIs (Dyn.get smObj "getEntries") "function" then
        unbox<obj array> (smObj?getEntries ())
    else
        [||]

let readAssistantText (sessionManager: ISessionManager) (startIndex: int) (joiner: string) : string option =
    let chunks = ResizeArray<string>()
    let arr = entries sessionManager

    for index in startIndex .. arr.Length - 1 do
        let entry = arr.[index]

        let role =
            let m = Dyn.get entry "message"

            if not (Dyn.isNullish m) then
                Dyn.str m "role"
            else
                Dyn.str (Dyn.get entry "info") "role"

        if role = "assistant" then
            let content =
                let m = Dyn.get entry "message"

                if not (Dyn.isNullish m) then
                    Dyn.get m "content"
                else
                    Dyn.get entry "parts"

            let t = textFromParts content

            if t <> "" then
                chunks.Add t

    if chunks.Count = 0 then
        None
    else
        Some(String.concat joiner chunks)

let lastAssistantMessage (sessionManager: ISessionManager) : string =
    readAssistantText sessionManager 0 "\n\n" |> Option.defaultValue ""

let lastAssistantTurnId (sessionManager: ISessionManager) : string =
    let arr = entries sessionManager

    match
        arr
        |> Array.tryFindIndexBack (fun entry ->
            let role =
                let m = Dyn.get entry "message"

                if not (Dyn.isNullish m) then
                    Dyn.str m "role"
                else
                    Dyn.str (Dyn.get entry "info") "role"

            role = "assistant")
    with
    | None -> ""
    | Some i -> string i

let private extractModelString (entry: obj) : string option =
    let pickFrom info =
        if Dyn.isNullish info then
            None
        else
            let modelVal = Dyn.get info "model"

            if Dyn.isNullish modelVal then
                None
            else
                let providerID = Dyn.str modelVal "providerID"
                let modelID = Dyn.str modelVal "modelID"
                let variant = Dyn.str modelVal "variant"
                let suffix = if variant <> "" then ":" + variant else ""

                if providerID = "" || modelID = "" then
                    None
                else
                    Some(sprintf "%s/%s%s" providerID modelID suffix)

    match pickFrom (Dyn.get entry "info") with
    | Some _ as m -> m
    | None ->
        let msg = Dyn.get entry "message"
        if Dyn.isNullish msg then None else pickFrom msg

let lastAssistantModel (sessionManager: ISessionManager) : string option =
    let arr = entries sessionManager

    arr
    |> Array.tryFindBack (fun entry ->
        let role =
            let m = Dyn.get entry "message"

            if not (Dyn.isNullish m) then
                Dyn.str m "role"
            else
                Dyn.str (Dyn.get entry "info") "role"

        role = "assistant")
    |> Option.bind extractModelString

let private flattenTodoTasks (phases: obj array) : string list =
    phases
    |> Array.collect (fun phase ->
        let tasks = Dyn.get phase "tasks"

        if not (Dyn.isArray tasks) then
            [||]
        else
            toObjArray tasks
            |> Array.choose (fun task ->
                let status = Dyn.str task "status"
                if status = "" then None else Some status))
    |> Array.toList

let openTodoStatuses (sessionManager: ISessionManager) : string list =
    let arr = entries sessionManager

    let statuses =
        match
            arr
            |> Array.tryFindBack (fun entry ->
                let custom = Dyn.get entry "customType"
                not (Dyn.isNullish custom) && string custom = "todo-phases")
        with
        | None -> []
        | Some entry ->
            let content = Dyn.get entry "content"

            if Dyn.isArray content then
                flattenTodoTasks (toObjArray content)
            elif not (Dyn.isNullish content) then
                flattenTodoTasks [| content |]
            else
                []

    statuses
    |> List.filter (fun s -> todoStatusOfString s |> Option.exists (fun st -> not (isTerminal st)))

let extractHistoryTexts (messages: Message<obj> list) : string list =
    messages
    |> List.collect (fun m ->
        m.parts
        |> List.map (function
            | TextPart t -> t
            | ToolPart(_, _, Some st, _) -> st.output
            | _ -> ""))

let hasActiveLoopFromHistory (sessionManager: ISessionManager) : bool =
    entries sessionManager
    |> decodeEntries ""
    |> extractHistoryTexts
    |> inferReviewTaskFromTexts
    |> Option.isSome

let activeLoopTaskFromHistory (sessionManager: ISessionManager) : string option =
    entries sessionManager
    |> decodeEntries ""
    |> extractHistoryTexts
    |> inferReviewTaskFromTexts

open Wanxiangshu.Hosts.Omp.MessagingCodecEncode

let encodeMessage = MessagingCodecEncode.encodeMessage
let encodeMessages = MessagingCodecEncode.encodeMessages
