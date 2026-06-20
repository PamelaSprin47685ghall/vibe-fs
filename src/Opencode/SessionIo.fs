module VibeFs.Opencode.SessionIo

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Message
open VibeFs.Shell.ChildAgentRegistry

type WorkspaceEffect = Ro | Rw

type WikiRecordRequest =
    { title: string
      prompt: string
      result: string
      agent: string }

/// Placeholder for a subagent session that produced no assistant text. Distinct
/// from the executor's "(no output)" (shell stdout): same string, different
/// domain fact, so each stays local to its own module (REFACTOR.md §0).
let private noOutputText = "(no output)"
let private abortedPrefix = "(aborted)"

let private firstString (ctx: obj) (keys: string list) : string option =
    keys
    |> List.tryPick (fun key ->
        let v = Dyn.get ctx key
        if Dyn.isNullish v then None else Some(string v))

/// Get the abort signal from the opencode context.  The host exposes it as
/// `context.abort` (an AbortSignal), not `context.abortSignal`.
let getAbortSignal (context: obj) : obj =
    if Dyn.isNullish context then null
    else
        let abort = Dyn.get context "abort"
        if Dyn.isNullish abort then null else abort

/// Extract the tool-execution context from an opencode tool `context`.
/// sessionID is returned as null when the host did not provide one, so parent
/// resolution treats the subagent as a top-level session.
let extractToolContext (context: obj) (pluginDirectory: string) : obj =
    let directory = firstString context [ "directory"; "cwd"; "workspaceDir"; "workspace_dir"; "workingDirectory" ]
    let sessionID = firstString context [ "sessionID"; "sessionId"; "session_id" ]
    box {|
        directory =
            match directory with
            | Some s when s <> "" -> s
            | _ -> pluginDirectory
        sessionID =
            match sessionID with
            | Some s when s <> "" -> box s
            | _ -> box null
        abortSignal = getAbortSignal context
    |}

/// Dynamically invoke a method on `target`, awaiting the resulting promise.
let invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method)(arg))

/// Convert opencode's message shape `{ info: { role }, parts: [...] }` into the
/// session-entry shape expected by `readAssistantText`.
let private toEntries (messages: obj) : obj array =
    if Dyn.isNullish messages then [||]
    else
        (unbox<obj[]> messages)
        |> Array.map (fun m ->
            let info = Dyn.get m "info"
            let role = if Dyn.isNullish info then null else Dyn.get info "role"
            let parts = Dyn.get m "parts"
            let content = if Dyn.isNullish parts then [||] else unbox<obj[]> parts
            let message = box {| role = role; content = content |}
            box {| ``type`` = "message"; message = message |})

/// Pull the latest assistant text from a session's messages.
let extractSessionText (client: obj) (sessionId: string) (directory: string) : JS.Promise<string> =
    promise {
        try
            let arg =
                if directory = "" then
                    box {| path = box {| id = sessionId |} |}
                else
                    box {| path = box {| id = sessionId |}; query = box {| directory = directory |} |}
            let! result = invoke1 arg "messages" (Dyn.get client "session")
            let data = Dyn.get result "data"
            if Dyn.isNullish data then return noOutputText
            else
                match readAssistantText (toEntries data) None with
                | Some text -> return text
                | None -> return noOutputText
        with _ -> return noOutputText
    }

[<Global>]
type DOMException(message: string, name: string) =
    inherit System.Exception()

/// Prompt a session and race it against an AbortSignal. The returned promise
/// rejects with `AbortError` if the signal fires before the prompt resolves.
let promptWithAbort (client: obj) (args: obj) (signal: obj) : JS.Promise<unit> =
    promise {
        let session = Dyn.get client "session"
        if Dyn.isNullish signal then
            do! session?prompt(args)
        elif Dyn.truthy (Dyn.get signal "aborted") then
            return! Promise.reject (DOMException("Aborted", "AbortError"))
        else
            let settled = ref false
            let handlerRef = ref None
            let abortAsync : JS.Promise<string> =
                Promise.create (fun resolve _reject ->
                    let handler = fun () ->
                        if not settled.Value then
                            settled.Value <- true
                            match handlerRef.Value with
                            | Some h -> signal?removeEventListener("abort", h) |> ignore
                            | None -> ()
                            resolve "aborted"
                    handlerRef.Value <- Some handler
                    signal?addEventListener("abort", handler) |> ignore)
            let promptAsync : JS.Promise<string> =
                promise {
                    do! session?prompt(args)
                    if not settled.Value then
                        settled.Value <- true
                        match handlerRef.Value with
                        | Some h -> signal?removeEventListener("abort", h) |> ignore
                        | None -> ()
                    return "ok"
                }
            try
                let! winner = Promise.race [ promptAsync; abortAsync ]
                if winner = "aborted" then return! Promise.reject (DOMException("Aborted", "AbortError"))
            with err ->
                match translateJsError err with
                | MessageAborted -> return! Promise.reject (DOMException("Aborted", "AbortError"))
                | _ -> return! Promise.reject err
    }

/// Core subagent runner. The `cleanup` flag controls whether the child session
/// is aborted and unregistered after the prompt finishes.
let private runSubagentCore (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                            (directory: string) (sessionID: string) (context: obj)
                            (tools: obj) (cleanup: bool) (workspaceEffect: WorkspaceEffect)
                            (wikiRecorder: (WikiRecordRequest -> unit) option) : JS.Promise<string> =
    promise {
        let parentID = registry.ResolveSubsessionParentID(if sessionID = "" then None else Some sessionID)
        let session = Dyn.get client "session"
        let createBody =
            box {|
                query = box {| directory = directory |}
                body = box {|
                    parentID =
                        match parentID with
                        | Some p -> box p
                        | None -> box null
                    title = title
                |}
            |}
        let! createResult = invoke1 createBody "create" session
        let childID = Dyn.str (Dyn.get createResult "data") "id"
        if childID = "" then return "Failed to create child session"
        else
            let abortAndUnregister () =
                if cleanup then
                    let abortPromise : JS.Promise<obj> = invoke1 (box {| path = box {| id = childID |} |}) "abort" session
                    abortPromise |> ignore
                    registry.UnregisterChildAgent(childID)
            registry.RegisterChildAgent(childID, agent, parentID)
            try
                try
                    let promptBody =
                        let body = box {| agent = agent; parts = [| box {| ``type`` = "text"; text = prompt |} |] |}
                        let body = if Dyn.isNullish tools then body else Dyn.withKey body "tools" tools
                        createObj [ "path", box {| id = childID |}; "body", body ]
                    do! promptWithAbort client promptBody (getAbortSignal context)
                    let! text = extractSessionText client childID directory
                    match workspaceEffect, wikiRecorder with
                    | Rw, Some record when text <> "" -> record { title = title; prompt = prompt; result = text; agent = agent }
                    | _ -> ()
                    return if text = "" then noOutputText else text
                finally
                    abortAndUnregister ()
            with err ->
                match translateJsError err with
                | MessageAborted ->
                    abortAndUnregister ()
                    let! text = extractSessionText client childID directory
                    return if text = "" then abortedPrefix else $"{abortedPrefix} {text}"
                | _ -> return! Promise.reject err
    }

/// Run a subagent and keep the child session registered after return.
let runSubagent (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                (directory: string) (sessionID: string) (context: obj)
                (tools: obj) : JS.Promise<string> =
    runSubagentCore registry client agent title prompt directory sessionID context tools false Ro None

let runSubagentWithEffect
    (registry: ChildAgentRegistry)
    (client: obj)
    (agent: string)
    (title: string)
    (prompt: string)
    (directory: string)
    (sessionID: string)
    (context: obj)
    (tools: obj)
    (workspaceEffect: WorkspaceEffect)
    (wikiRecorder: (WikiRecordRequest -> unit) option)
    : JS.Promise<string> =
    runSubagentCore registry client agent title prompt directory sessionID context tools false workspaceEffect wikiRecorder

/// Run a subagent and clean up the child session afterwards. Used for
/// short-lived analysis subagents such as the executor summarizer.
let runSubagentWithCleanup (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                           (directory: string) (sessionID: string) (context: obj) : JS.Promise<string> =
    runSubagentCore registry client agent title prompt directory sessionID context (box null) true Ro None

/// Run a subagent with an explicit tool set and clean up afterwards.
let runSubagentWithTools
    (registry: ChildAgentRegistry)
    (client: obj) (agent: string) (title: string) (prompt: string)
    (directory: string) (sessionID: string) (context: obj)
    (tools: obj) : JS.Promise<string> =
    runSubagentCore registry client agent title prompt directory sessionID context tools true Ro None
