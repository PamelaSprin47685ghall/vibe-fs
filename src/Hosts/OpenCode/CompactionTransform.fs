module Wanxiangshu.Hosts.Opencode.CompactionTransform

open Wanxiangshu.Runtime.Fallback.RuntimeStore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Hosts.Opencode.BacklogSession
open Wanxiangshu.Runtime.Fallback.HumanTurnTransitions
open Wanxiangshu.Runtime.Fallback.OrdinalTransitions
open Wanxiangshu.Runtime.Fallback.CompactionTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn

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
            // Arm the compaction summary transform bypass flag
            fr.UpdateSession(
                sessionID,
                fun s ->
                    { s with
                        CompactionSummaryTransformPending = true }
            )
        | None -> ()
    }

let private injectCompactionContext (backlogSession: BacklogSession) (sessionID: string) (output: obj) : unit =
    let backlog = backlogSession.GetOrRebuildBacklog(sessionID, [])

    let text =
        Wanxiangshu.Runtime.BacklogProjectionBuild.buildCompactionContextText backlog

    let currentContext =
        let c = Dyn.get output "context"

        if not (Dyn.isNullish c) && Dyn.isArray c then
            c :?> string array
        else
            [||]

    output?context <- Array.append currentContext [| text |]

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

            // Clear the compaction summary transform bypass flag on error
            fr.ClearCompactionSummaryTransformPending(sessionID)
        | _ -> ()

        return! Promise.reject ex
    }

let compactingTransform
    (directory: string)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
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
                injectCompactionContext backlogSession sessionID output
            with ex ->
                do! handleCompactionError directory sessionID compactionId fallbackRuntime ex
    }
