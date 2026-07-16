module Wanxiangshu.Hosts.Opencode.CompactionTransform

open Wanxiangshu.Runtime.Fallback.RuntimeStore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Hosts.Opencode.BacklogSession
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Hosts.Opencode.MessagingCodec
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.EventLogRuntime

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

let private invokeClient (client: obj) (method_: string) (arg: obj) : JS.Promise<obj> =
    if Dyn.isNullish client then
        Promise.lift (unbox null)
    else
        match getSessionApiFromClient client with
        | Error _ -> Promise.lift (unbox null)
        | Ok session ->
            let api: obj = Dyn.get session method_

            if Dyn.isNullish api then
                Promise.lift (unbox null)
            else
                unbox<JS.Promise<obj>> (Dyn.callMethod1 session method_ arg)

let compactionAutocontinue (input: obj) (output: obj) : JS.Promise<unit> = promise { output?enabled <- true }

let private recordCompactionStart
    (directory: string)
    (sessionID: string)
    (compactionId: string)
    (fallbackRuntime: FallbackRuntimeStore option)
    : JS.Promise<unit> =
    promise {
        let currentGen =
            match fallbackRuntime with
            | Some fr -> fr.GetSessionGeneration sessionID
            | None -> 0

        let humanTurnId =
            match fallbackRuntime with
            | Some fr -> fr.GetHumanTurnId sessionID
            | None -> ""

        let compactionOrdinal =
            match fallbackRuntime with
            | Some fr -> fr.IncrementCompactionOrdinal sessionID
            | None -> 0

        do!
            Wanxiangshu.Runtime.EventLogRuntime.appendCompactionStartedOrFail
                directory
                sessionID
                compactionId
                currentGen
                humanTurnId
                compactionOrdinal

        match fallbackRuntime with
        | Some fr ->
            fr.SetSessionOwner sessionID SessionOwner.Compaction
            fr.SetActiveCompactionId(sessionID, compactionId, compactionOrdinal)
            fr.SetCompacted(sessionID, false)
            fr.SetCompactionContinuationObserved(sessionID, false)
            fr.SetCompactionGeneration(sessionID, currentGen)
        | None -> ()
    }

let private performCompaction
    (directory: string)
    (sessionID: string)
    (client: obj)
    (backlogSession: BacklogSession)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        let arg = box {| path = box {| id = sessionID |} |}
        let! resp = invokeClient client "messages" arg
        let data = Dyn.get resp "data"

        let messagesArr =
            if not (Dyn.isNullish data) && Dyn.isArray data then
                data :?> obj array
            else
                [||]

        if messagesArr.Length > 0 then
            let messagesList = MessagingCodec.decodeMessages messagesArr
            let cleaned = messagesList
            let backlog = backlogSession.GetOrRebuildBacklog(sessionID, cleaned)
            let guidGen () = string (runtimeScope.RandomGen())

            let result =
                Wanxiangshu.Runtime.BacklogProjectionBuild.compactingTransform cleaned backlog guidGen

            let wrappedText =
                match result with
                | m :: _ ->
                    match m.parts with
                    | TextPart t :: _ -> t
                    | _ -> ""
                | [] -> ""

            let currentContext =
                let c = Dyn.get output "context"

                if not (Dyn.isNullish c) && Dyn.isArray c then
                    c :?> string array
                else
                    [||]

            output?context <- Array.append currentContext [| wrappedText |]
    }

let private handleCompactionError
    (directory: string)
    (sessionID: string)
    (compactionId: string)
    (fallbackRuntime: FallbackRuntimeStore option)
    (ex: System.Exception)
    : JS.Promise<unit> =
    promise {
        match fallbackRuntime with
        | Some fr when fr.GetActiveCompactionId sessionID = compactionId ->
            let settleInfo = fr.TryGetSettleInfo(sessionID, compactionId)

            match settleInfo with
            | Some(_, ordinal) ->
                do!
                    Wanxiangshu.Runtime.EventLogRuntime.appendCompactionSettledOrFail
                        directory
                        sessionID
                        compactionId
                        "failed"
                        ordinal

                let _ = fr.ApplySettle(sessionID, compactionId)
                ()
            | None -> ()
        | _ -> ()

        return! Promise.reject ex
    }

let compactingTransform
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (client: obj)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        runtimeScope.TriggerInit(directory)
        do! runtimeScope.WaitInit()

        let sessionID = Dyn.str input "sessionID"

        if sessionID <> "" then
            let fallbackRuntime =
                match runtimeScope.TryFindKey("fallbackRuntime") with
                | Some obj -> Some(unbox<FallbackRuntimeStore> obj)
                | None -> None

            let compactionId = "compact-" + System.Guid.NewGuid().ToString("N")

            try
                do! recordCompactionStart directory sessionID compactionId fallbackRuntime
                do! performCompaction directory sessionID client backlogSession runtimeScope output
            with ex ->
                do! handleCompactionError directory sessionID compactionId fallbackRuntime ex
    }
