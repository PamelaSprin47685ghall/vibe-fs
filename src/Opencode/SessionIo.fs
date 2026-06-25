module VibeFs.Opencode.SessionIo

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.ToolResult
open VibeFs.Shell.ErrorClassify
open VibeFs.Opencode.MessagingCodec
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.DelegatedAiSettings
open VibeFs.Shell
open VibeFs.Shell.Dyn
open VibeFs.Shell.ToolContextCodec
open VibeFs.Shell.OpencodeContextCodec
open VibeFs.Shell.OpencodeSessionPromptCodec
open VibeFs.Shell.OpencodeSessionSpawnCodec
open VibeFs.Shell.OpencodeClientCodec
open VibeFs.Shell.SessionIoSpawn
open VibeFs.Shell.SubagentToolExecute

[<Global>]
type DOMException(message: string, name: string) =
    inherit System.Exception()

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

/// Get the abort signal from the opencode context.  The host exposes it as
/// `context.abort` (an AbortSignal), not `context.abortSignal`.
let getAbortSignal (context: obj) : obj = getAbortSignalFromContext context

/// Extract the tool-execution context from an opencode tool `context`.
/// sessionID is returned as null when the host did not provide one, so parent
/// resolution treats the subagent as a top-level session.
let extractToolContext (context: obj) (pluginDirectory: string) : obj =
    let execution = decodeOpencodeToolContext context pluginDirectory
    box {|
        directory = execution.Directory
        sessionID =
            if execution.SessionId = "" then box null
            else box execution.SessionId
        abortSignal = getAbortSignal context
    |}

/// Dynamically invoke a method on `target`, awaiting the resulting promise.
let invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method)(arg))

let private buildPromptBody (options: SubagentLaunchOptions) childID : obj =
    let body = box {| agent = options.agent; parts = [| box {| ``type`` = "text"; text = options.prompt |} |] |}
    let body = if Dyn.isNullish options.tools then body else Dyn.withKey body "tools" options.tools
    let body =
        match options.aiSettings.modelString with
        | None -> body
        | Some modelString ->
            let payload = createObj [ "modelString", box modelString ]
            match tryDecodePromptModelFromPayload payload with
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
            match getSessionApiFromClient client with
            | Error _ -> return noOutputText
            | Ok session ->
                let arg =
                    if directory = "" then
                        box {| path = box {| id = sessionId |} |}
                    else
                        box {| path = box {| id = sessionId |}; query = box {| directory = directory |} |}
                let! result = invoke1 arg "messages" session
                let data = Dyn.get result "data"
                if Dyn.isNullish data then return noOutputText
                else
                    let messagesList = MessagingCodec.decodeMessages (unbox<obj[]> data)
                    match Messaging.readAssistantText messagesList 0 "\n\n" with
                    | Some text -> return text
                    | None -> return noOutputText
        with _ -> return noOutputText
    }

/// Read all text fragments from a session's message history.
let readSessionTexts (client: obj) (sessionId: string) (directory: string) : JS.Promise<string list> =
    promise {
        try
            match getSessionApiFromClient client with
            | Error _ -> return []
            | Ok session ->
                let arg =
                    if directory = "" then
                        box {| path = box {| id = sessionId |} |}
                    else
                        box {| path = box {| id = sessionId |}; query = box {| directory = directory |} |}
                let! result = invoke1 arg "messages" session
                let data = Dyn.get result "data"
                if Dyn.isNullish data then return []
                else
                    let messagesList = MessagingCodec.decodeMessages (unbox<obj[]> data)
                    return
                        messagesList
                        |> Messaging.flatten
                        |> List.map (fun fp ->
                            match fp.part with
                            | TextPart text -> text
                            | ToolPart(_, _, Some state, _) -> state.output
                            | _ -> "")
        with _ -> return []
    }

/// Prompt a session and race it against an AbortSignal. The returned promise
/// rejects with `AbortError` if the signal fires before the prompt resolves.
let promptWithAbort (client: obj) (args: obj) (signal: obj) : JS.Promise<unit> =
    promise {
        match getSessionApiFromClient client with
        | Error err -> return! Promise.reject (exn (wireEncodeToolError "OpencodeClient" err))
        | Ok session ->
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

let startSubagentSession (registry: ChildAgentRegistry) (client: obj) (options: SubagentLaunchOptions) : JS.Promise<Result<string, DomainError>> =
    promise {
        match getSessionApiFromClient client with
        | Error err -> return Error err
        | Ok session ->
            let parentID = registry.ResolveSubsessionParentID(if options.sessionID = "" then None else Some options.sessionID)
            let createBody =
                box {|
                    query = box {| directory = options.directory |}
                    body = box {| parentID = (match parentID with Some p -> box p | None -> box null); title = options.title |}
                |}
            let! createResult = invoke1 createBody "create" session
            match decodeChildSessionIdFromCreateResult createResult with
            | Error err -> return Error err
            | Ok childID ->
                registry.RegisterChildAgent(childID, options.agent, parentID)
                return Ok childID
    }

let runSubagentCoreResult (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                          (directory: string) (sessionID: string) (context: obj)
                          (tools: obj) (cleanup: bool) : JS.Promise<Result<string, DomainError>> =
    promise {
        let signal = getAbortSignal context
        let options =
            { agent = agent
              title = title
              prompt = prompt
              directory = directory
              sessionID = sessionID
              tools = tools
              aiSettings = emptySettings }
        try
            let! childResult = startSubagentSession registry client options
            match childResult with
            | Error err -> return Error err
            | Ok childID ->
                let abortAndUnregister () =
                    match getSessionApiFromClient client with
                    | Ok session ->
                        let abortPromise : JS.Promise<obj> = invoke1 (box {| path = box {| id = childID |} |}) "abort" session
                        abortPromise |> ignore
                    | Error _ -> ()
                    registry.UnregisterChildAgent(childID)
                let cleanupChildIfRequested () = if cleanup then abortAndUnregister ()
                try
                    do! promptWithAbort client (buildPromptBody options childID) signal
                    try
                        let! text = extractSessionText client childID directory
                        return Ok (formatSubagentReport noOutputText abortedPrefix text false)
                    finally
                        cleanupChildIfRequested ()
                with err ->
                    match translateJsError err with
                    | MessageAborted ->
                        abortAndUnregister ()
                        if not (Dyn.isNullish signal) && Dyn.truthy (Dyn.get signal "aborted") then
                            return Ok abortedPrefix
                        else
                            let! text = extractSessionText client childID directory
                            return Ok (formatSubagentReport noOutputText abortedPrefix text true)
                    | other -> return Error other
        with err ->
            return Error (translateJsError err)
    }

let runSubagent (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                (directory: string) (sessionID: string) (context: obj)
                (tools: obj) : JS.Promise<Result<string, DomainError>> =
    runSubagentCoreResult registry client agent title prompt directory sessionID context tools false

let runSubagentWithCleanup (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                           (directory: string) (sessionID: string) (context: obj) : JS.Promise<Result<string, DomainError>> =
    runSubagentCoreResult registry client agent title prompt directory sessionID context (box null) true