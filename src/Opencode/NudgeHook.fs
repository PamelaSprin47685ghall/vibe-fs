module VibeFs.Opencode.NudgeHook

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeEvents
open VibeFs.Kernel.Prompts

[<Emit("$2[$1]($0)")>]
let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = jsNative

let getSessionID (eventType: string) (props: obj) : string =
    let part = Dyn.get props "part"
    let info = Dyn.get props "info"
    let candidates =
        [ Dyn.str props "sessionID"
          Dyn.str part "sessionID"
          Dyn.str info "sessionID"
          if eventType = "session.created" || eventType = "session.updated" || eventType = "session.deleted" then
              Dyn.str info "id"
          else "" ]
    candidates |> List.tryFind (fun s -> s <> "") |> Option.defaultValue ""

let getEventAgent (props: obj) : string =
    let agent = Dyn.str props "agent"
    if agent <> "" then agent else Dyn.str (Dyn.get props "info") "agent"

let getPartsText (parts: obj) : string =
    if not (Dyn.isArray parts) then ""
    else
        (parts :?> obj array)
        |> Array.choose (fun part ->
            if Dyn.str part "type" = "text" then
                let text = Dyn.get part "text"
                if Dyn.isNullish text then None else Some (string text)
            else None)
        |> String.concat "\n"

let isCompletedAssistantMessage (info: obj) : bool =
    if Dyn.isNullish info then false
    else
        let isAssistant = Dyn.str info "role" = "assistant" || Dyn.str info "type" = "assistant"
        let hasError = not (Dyn.isNullish (Dyn.get info "error"))
        if not isAssistant || hasError then false
        else
            let finishVal = Dyn.get info "finish"
            if not (Dyn.isNullish finishVal) && Dyn.typeIs finishVal "string" then
                isTerminalAssistantFinish (string finishVal)
            else
                let timeCompleted = Dyn.get (Dyn.get info "time") "completed"
                not (Dyn.isNullish timeCompleted) && Dyn.typeIs timeCompleted "number"

let isAbortError (error: obj) : bool =
    let rec check (error: obj) =
        if Dyn.isNullish error then false
        elif Dyn.typeIs error "string" then
            Regex.IsMatch(string error, @"\babort(?:ed)?\b", RegexOptions.IgnoreCase)
        elif not (Dyn.typeIs error "object") then false
        else
            let name = Dyn.str error "name"
            if name = "AbortError" || name = "MessageAbortedError" then true
            else
                let nested = Dyn.get error "error"
                if not (Dyn.isNullish nested) && not (obj.ReferenceEquals(nested, error)) && check nested then true
                else
                    let data = Dyn.get error "data"
                    if not (Dyn.isNullish data) && Dyn.typeIs data "object" then
                        let dataMessage = Dyn.str data "message"
                        dataMessage <> "" && Regex.IsMatch(dataMessage, @"\babort(?:ed)?\b", RegexOptions.IgnoreCase)
                    else
                        let message = Dyn.str error "message"
                        message <> "" && Regex.IsMatch(message, @"\babort(?:ed)?\b", RegexOptions.IgnoreCase)
    check error

let collectSnapshot (client: obj) (sessionID: string) : JS.Promise<NudgeContext option> =
    async {
        try
            let session = Dyn.get client "session"
            let! todoResp = invoke1 (box {| path = {| id = sessionID |} |}) "todo" session |> Async.AwaitPromise
            let todosData = Dyn.get todoResp "data"
            let openTodos =
                if Dyn.isArray todosData then
                    (todosData :?> obj array)
                    |> Array.choose (fun todo ->
                        let status = Dyn.str todo "status"
                        if terminalTodoStatuses.Contains status then None else Some status)
                    |> Array.toList
                else []
            let! messagesResp = invoke1 (box {| path = {| id = sessionID |} |}) "messages" session |> Async.AwaitPromise
            let messagesData = Dyn.get messagesResp "data"
            let lastAssistantText =
                if Dyn.isArray messagesData then
                    (messagesData :?> obj array)
                    |> Array.tryFindBack (fun msg -> isCompletedAssistantMessage (Dyn.get msg "info"))
                    |> Option.map (fun msg -> getPartsText (Dyn.get msg "parts"))
                    |> Option.defaultValue ""
                else ""
            return Some
                { todos = openTodos
                  lastAssistantMessage = lastAssistantText
                  hasActiveRunner = false
                  isLoopActive = false }
        with _ -> return None
    } |> Async.StartAsPromise

type NudgeHook(ctx: obj, reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) =
    let client = Dyn.get ctx "client"
    let coordinator = Nudge.defaultCoordinator
    let stoppedSessions = HashSet<string>()

    let sendNudge sessionID promptText =
        async {
            let agent = ChildAgent.lookupChildAgent(sessionID)
            let body = createPromptBody agent promptText
            let promptArg = box {| path = box {| id = sessionID |}; body = body |}
            let session = Dyn.get client "session"
            let! result = Async.Catch (invoke1 promptArg "prompt" session |> Async.AwaitPromise)
            match result with
            | Choice1Of2 _ -> ()
            | Choice2Of2 exn ->
                if Dyn.str (box exn) "_tag" = "SessionBusyError" then
                    coordinator.clearSession(sessionID)
        }

    let nudgeIfNeeded (sessionID: string) : JS.Promise<unit> =
        async {
            if stoppedSessions.Contains(sessionID) then return ()
            else
                let! snapshotOpt = collectSnapshot client sessionID |> Async.AwaitPromise
                match snapshotOpt with
                | None -> return ()
                | Some snapshot ->
                    let context = { snapshot with isLoopActive = reviewStore.isReviewActive(sessionID) }
                    match coordinator.shouldNudge(sessionID, context) with
                    | "nudge-todo" -> do! sendNudge sessionID todoNudgePrompt
                    | "nudge-loop" -> do! sendNudge sessionID loopNudgePrompt
                    | _ -> ()
        } |> Async.StartAsPromise

    member _.handleEvent(input: obj) : JS.Promise<unit> =
        async {
            try
                let event = Dyn.get input "event"
                let eventType = Dyn.str event "type"
                let rawProps = Dyn.get event "properties"
                let props = if Dyn.isNullish rawProps then event else rawProps
                let sessionID = getSessionID eventType props
                if sessionID = "" then return ()
                else
                    match eventType with
                    | "stream-abort" ->
                        reviewStore.deactivateReview(sessionID)
                        stoppedSessions.Add(sessionID) |> ignore
                        coordinator.clearSession(sessionID)
                    | "session.delete" | "session.close" | "session.remove" | "session.deleted" ->
                        coordinator.clearSession(sessionID)
                        stoppedSessions.Remove(sessionID) |> ignore
                    | "session.idle" ->
                        do! nudgeIfNeeded(sessionID) |> Async.AwaitPromise
                    | "message.updated" ->
                        let info = Dyn.get props "info"
                        if isAbortError (Dyn.get info "error") then
                            stoppedSessions.Add(sessionID) |> ignore
                            coordinator.clearSession(sessionID)
                        elif isCompletedAssistantMessage info then
                            do! nudgeIfNeeded(sessionID) |> Async.AwaitPromise
                    | "session.next.step.ended" ->
                        let finish =
                            let direct = Dyn.str props "finish"
                            if direct <> "" then direct else Dyn.str (Dyn.get props "info") "finish"
                        if finish <> "" && isTerminalAssistantFinish finish then
                            do! nudgeIfNeeded(sessionID) |> Async.AwaitPromise
                    | "session.status" ->
                        let statusType = Dyn.str (Dyn.get props "status") "type"
                        match statusType with
                        | "idle" -> do! nudgeIfNeeded(sessionID) |> Async.AwaitPromise
                        | "busy" -> coordinator.clearSession(sessionID)
                        | "retry" -> ()
                        | _ -> ()
                    | "session.error" ->
                        if isAbortError (Dyn.get props "error") then
                            stoppedSessions.Add(sessionID) |> ignore
                            coordinator.clearSession(sessionID)
                    | "session.next.prompted" ->
                        let text =
                            let partsText = getPartsText (Dyn.get props "parts")
                            if partsText <> "" then partsText else Dyn.str props "text"
                        if not (isNudgePrompt text) then
                            coordinator.clearSession(sessionID)
                            stoppedSessions.Remove(sessionID) |> ignore
                    | _ -> ()
            with _ -> ()
        } |> Async.StartAsPromise

let createNudgeHook (ctx: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) : NudgeHook =
    NudgeHook(ctx, reviewStore)
