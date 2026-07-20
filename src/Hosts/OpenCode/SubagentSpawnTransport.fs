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

let promptWithAbort (client: obj) (args: obj) (signal: obj) : JS.Promise<unit> =
    promise {
        match getSessionApiFromClient client with
        | Error err -> return! Promise.reject (exn (wireEncodeToolError "OpencodeClient" err))
        | Ok session ->
            let childID = Dyn.str (Dyn.get args "path") "id"

            if Dyn.isNullish signal then
                do! session?prompt (args)
            elif Dyn.truthy (Dyn.get signal "aborted") then
                do! physicalAbort session childID
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
                    do! physicalAbort session childID
                    return! Promise.reject (DOMException("Aborted", "AbortError"))
    }

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
