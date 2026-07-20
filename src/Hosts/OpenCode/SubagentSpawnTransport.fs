module Wanxiangshu.Hosts.Opencode.SubagentSpawnTransport

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.ChildAgentRegistry

open Wanxiangshu.Kernel.ToolResult

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeSessionSpawnCodec
open Wanxiangshu.Hosts.Opencode.SubagentTypes
open Wanxiangshu.Hosts.Opencode.SubagentSpawnInput
open Wanxiangshu.Hosts.Opencode.SubagentSpawnCleanup

/// S-08 style gate: physical session.abort only when the caller still owns
/// this prompt epoch. Stale aborts after close / epoch bump skip host abort.
type PromptAbortGate =
    { mutable Live: bool
      mutable Epoch: int }

let createPromptAbortGate () : PromptAbortGate = { Live = true; Epoch = 0 }

let bumpPromptAbortEpoch (gate: PromptAbortGate) : int =
    gate.Epoch <- gate.Epoch + 1
    gate.Epoch

let closePromptAbortGate (gate: PromptAbortGate) : unit = gate.Live <- false

let private ownsEpoch (gate: PromptAbortGate option) (epoch: int) : bool =
    match gate with
    | None -> true
    | Some g -> g.Live && g.Epoch = epoch

let private hostAbortIfOwned (session: obj) (childID: string) (gate: PromptAbortGate option) (epoch: int) =
    promise {
        if ownsEpoch gate epoch then
            do! physicalAbort session childID
    }

let promptWithAbortOwned
    (client: obj)
    (args: obj)
    (signal: obj)
    (gate: PromptAbortGate option)
    : JS.Promise<unit> =
    promise {
        match getSessionApiFromClient client with
        | Error err -> return! Promise.reject (exn (wireEncodeToolError "OpencodeClient" err))
        | Ok session ->
            let childID = Dyn.str (Dyn.get args "path") "id"
            let epoch = match gate with | Some g -> g.Epoch | None -> 0

            if Dyn.isNullish signal then
                do! session?prompt (args)
            elif Dyn.truthy (Dyn.get signal "aborted") then
                do! hostAbortIfOwned session childID gate epoch
                return! Promise.reject (DOMException("Aborted", "AbortError"))
            else
                let settled = ref false
                let handlerRef = ref None

                let abortAsync: JS.Promise<string> =
                    Promise.create (fun resolve _reject ->
                        let handler =
                            fun () ->
                                if not settled.Value then
                                    settled.Value <- true

                                    match handlerRef.Value with
                                    | Some h -> signal?removeEventListener ("abort", h) |> ignore
                                    | None -> ()

                                    resolve "aborted"

                        handlerRef.Value <- Some handler
                        signal?addEventListener ("abort", handler) |> ignore)

                let promptAsync: JS.Promise<string> =
                    promise {
                        do! session?prompt (args)

                        if not settled.Value then
                            settled.Value <- true

                            match handlerRef.Value with
                            | Some h -> signal?removeEventListener ("abort", h) |> ignore
                            | None -> ()

                        return "ok"
                    }

                let! winner = Promise.race [ promptAsync; abortAsync ]

                if winner = "aborted" then
                    do! hostAbortIfOwned session childID gate epoch
                    return! Promise.reject (DOMException("Aborted", "AbortError"))
    }

let promptWithAbort (client: obj) (args: obj) (signal: obj) : JS.Promise<unit> =
    promptWithAbortOwned client args signal None

let startSubagentSession
    (registry: ChildAgentRegistry)
    (client: obj)
    (options: SubagentLaunchOptions)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        match getSessionApiFromClient client with
        | Error err -> return Error err
        | Ok session ->
            let parentID =
                registry.ResolveSubsessionParentID(
                    if options.sessionID = "" then
                        None
                    else
                        Some options.sessionID
                )

            let createBody =
                box
                    {| query = box {| directory = options.directory |}
                       body =
                        box
                            {| parentID =
                                (match parentID with
                                 | Some p -> box p
                                 | None -> box null)
                               title = options.title |} |}

            let! createResult = invoke1 createBody "create" session

            match decodeChildSessionIdFromCreateResult createResult with
            | Error err -> return Error err
            | Ok childID ->
                registry.RegisterChildAgent(childID, options.agent, parentID)
                return Ok childID
    }
