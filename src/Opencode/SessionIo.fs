module VibeFs.Opencode.SessionIo

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Messaging
open VibeFs.Opencode.MessagingCodec
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Mux.AiSettings

type SubagentLaunchOptions =
    { agent: string
      title: string
      prompt: string
      directory: string
      sessionID: string
      tools: obj
      aiSettings: DelegatedAiSettings }

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

let private tryReadPromptModel (payload: obj) : obj option =
    let promptModel = Dyn.get payload "model"
    if not (Dyn.isNullish promptModel) then Some promptModel
    else
        let modelString = Dyn.str payload "modelString"
        if modelString = "" then None
        else
            let slash = modelString.IndexOf('/')
            if slash <= 0 || slash >= modelString.Length - 1 then None
            else Some (box {| providerID = modelString.[0..slash-1]; modelID = modelString.[slash+1..] |})

let private buildPromptBody (options: SubagentLaunchOptions) childID : obj =
    let body = box {| agent = options.agent; parts = [| box {| ``type`` = "text"; text = options.prompt |} |] |}
    let body = if Dyn.isNullish options.tools then body else Dyn.withKey body "tools" options.tools
    let body =
        match options.aiSettings.modelString with
        | None -> body
        | Some modelString ->
            match tryReadPromptModel (createObj [ "modelString", box modelString ]) with
            | Some model -> Dyn.withKey body "model" model
            | None -> body
    let body =
        match options.aiSettings.thinkingLevel with
        | Some level when level.Trim() <> "" -> Dyn.withKey body "variant" (box level)
        | _ -> body
    createObj [ "path", box {| id = childID |}; "body", body ]

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
                let messagesList = MessagingCodec.decodeMessages (unbox<obj[]> data)
                match Messaging.readAssistantText messagesList 0 "\n\n" with
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

let startSubagentSession (registry: ChildAgentRegistry) (client: obj) (options: SubagentLaunchOptions) : JS.Promise<string> =
    promise {
        let parentID = registry.ResolveSubsessionParentID(if options.sessionID = "" then None else Some options.sessionID)
        let session = Dyn.get client "session"
        let createBody =
            box {|
                query = box {| directory = options.directory |}
                body = box {| parentID = (match parentID with Some p -> box p | None -> box null); title = options.title |}
            |}
        let! createResult = invoke1 createBody "create" session
        let childID = Dyn.str (Dyn.get createResult "data") "id"
        if childID = "" then return! Promise.reject (exn "Failed to create child session")
        else
            registry.RegisterChildAgent(childID, options.agent, parentID)
            do! promptWithAbort client (buildPromptBody options childID) null
            return childID
    }

/// Core subagent runner. The `cleanup` flag controls whether the child session
/// is aborted and unregistered after the prompt finishes.
let private runSubagentCore (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                            (directory: string) (sessionID: string) (context: obj)
                            (tools: obj) (cleanup: bool) : JS.Promise<string> =
    promise {
        try
            let! childID =
                startSubagentSession registry client
                    { agent = agent
                      title = title
                      prompt = prompt
                      directory = directory
                      sessionID = sessionID
                      tools = tools
                      aiSettings = emptySettings }
            let abortAndUnregister () =
                if cleanup then
                    let session = Dyn.get client "session"
                    let abortPromise : JS.Promise<obj> = invoke1 (box {| path = box {| id = childID |} |}) "abort" session
                    abortPromise |> ignore
                    registry.UnregisterChildAgent(childID)
            try
                try
                    let! text = extractSessionText client childID directory
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
        with _ ->
            return "Failed to create child session"
    }

/// Run a subagent and keep the child session registered after return.
let runSubagent (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                (directory: string) (sessionID: string) (context: obj)
                (tools: obj) : JS.Promise<string> =
    runSubagentCore registry client agent title prompt directory sessionID context tools false

/// Run a subagent and clean up the child session afterwards. Used for
/// short-lived analysis subagents such as the executor summarizer.
let runSubagentWithCleanup (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                           (directory: string) (sessionID: string) (context: obj) : JS.Promise<string> =
    runSubagentCore registry client agent title prompt directory sessionID context (box null) true

/// Run a subagent with an explicit tool set and clean up afterwards.
let runSubagentWithTools
    (registry: ChildAgentRegistry)
    (client: obj) (agent: string) (title: string) (prompt: string)
    (directory: string) (sessionID: string) (context: obj)
    (tools: obj) : JS.Promise<string> =
    runSubagentCore registry client agent title prompt directory sessionID context tools true
