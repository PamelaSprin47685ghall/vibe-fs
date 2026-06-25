module VibeFs.Omp.MessagingCodec

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Shell.Dyn
module Dyn = VibeFs.Shell.Dyn

let private toObjArray (value: obj) : obj array =
    if Dyn.isNullish value || not (Dyn.isArray value) then [||]
    else unbox<obj array> value

let private textFromParts (parts: obj) : string =
    if not (Dyn.isArray parts) then ""
    else
        toObjArray parts
        |> Array.choose (fun part ->
            if Dyn.str part "type" = "text" then
                let t = Dyn.get part "text"
                if Dyn.isNullish t then None else Some (string t)
            else
                None)
        |> String.concat "\n\n"

let entries (sessionManager: obj) : obj array =
    let getEntries = Dyn.get sessionManager "getEntries"
    if Dyn.typeIs getEntries "function" then
        let raw = Dyn.call0 getEntries
        if Dyn.isArray raw then toObjArray raw else [||]
    else
        [||]

let readAssistantText (sessionManager: obj) (startIndex: int) (joiner: string) : string option =
    let chunks = ResizeArray<string>()
    let arr = entries sessionManager
    for index in startIndex .. arr.Length - 1 do
        let entry = arr.[index]
        let role =
            let m = Dyn.get entry "message"
            if not (Dyn.isNullish m) then Dyn.str m "role"
            else Dyn.str (Dyn.get entry "info") "role"
        if role = "assistant" then
            let content =
                let m = Dyn.get entry "message"
                if not (Dyn.isNullish m) then Dyn.get m "content" else Dyn.get entry "parts"
            let t = textFromParts content
            if t <> "" then chunks.Add t
    if chunks.Count = 0 then None else Some (String.concat joiner chunks)

let lastAssistantMessage (sessionManager: obj) : string =
    readAssistantText sessionManager 0 "\n\n" |> Option.defaultValue ""

let private flattenTodoTasks (phases: obj array) : string list =
    phases
    |> Array.collect (fun phase ->
        let tasks = Dyn.get phase "tasks"
        if not (Dyn.isArray tasks) then [||]
        else
            toObjArray tasks
            |> Array.choose (fun task ->
                let status = Dyn.str task "status"
                if status = "" then None else Some status))
    |> Array.toList

let openTodoStatuses (sessionManager: obj) : string list =
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
            if Dyn.isArray content then flattenTodoTasks (toObjArray content)
            elif not (Dyn.isNullish content) then flattenTodoTasks [| content |]
            else []
    statuses
    |> List.filter (fun s ->
        let lower = s.ToLowerInvariant()
        lower <> "completed" && lower <> "cancelled" && lower <> "abandoned")