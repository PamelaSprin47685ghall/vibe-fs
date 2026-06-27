module Wanxiangshu.Opencode.SubagentSpawn

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.OpopenClientCodec
open Wanxiangshu.Shell.OpopenContextCodec
open Wanxiangshu.Shell.SessionIoSpawn
open Wanxiangshu.Shell.SubagentToolExecute

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

let abortedPrefix = "(aborted)"

let getAbortSignal (context: obj) : obj = getAbortSignalFromContext context

let invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method)(arg))

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

let runSubagentCoreResult (runtime: FallbackRuntimeState) (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
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
        let noOutputText = "(no output)"
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
                    do! SubagentIo.promptWithAbort client (SubagentIo.buildPromptBody options childID) signal
                    try
                        let! text = SubagentIo.extractSessionText client childID directory
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
                            let! text = SubagentIo.extractSessionText client childID directory
                            return Ok (formatSubagentReport noOutputText abortedPrefix text true)
                    | other ->
                        do! Promise.lift ()
                        match runtime.GetConsumed childID with
                        | Some true ->
                            let! text = SubagentIo.extractSessionText client childID directory
                            return Ok (formatSubagentReport noOutputText abortedPrefix text false)
                        | _ -> return Error other
        with err ->
            return Error (translateJsError err)
    }

let runSubagentWithCleanup (runtime: FallbackRuntimeState) (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                           (directory: string) (sessionID: string) (context: obj) : JS.Promise<Result<string, DomainError>> =
    runSubagentCoreResult runtime registry client agent title prompt directory sessionID context (box null) true

let runSubagent (runtime: FallbackRuntimeState) (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                (directory: string) (sessionID: string) (context: obj)
                (tools: obj) : JS.Promise<Result<string, DomainError>> =
    runSubagentCoreResult runtime registry client agent title prompt directory sessionID context tools false
