module Wanxiangshu.Hosts.Opencode.CompactionTransform

open Wanxiangshu.Runtime.Fallback.RuntimeStore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.BacklogSession
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.SessionEventWriter

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
            | Some fr -> (fr.GetSession sessionID).SessionGeneration
            | None -> 0

        let humanTurnId =
            match fallbackRuntime with
            | Some fr -> (fr.GetSession sessionID).HumanTurnId
            | None -> ""

        let cancelGen =
            match fallbackRuntime with
            | Some fr -> (fr.GetSession sessionID).CancelGeneration
            | None -> 0

        let compactionOrdinal =
            match fallbackRuntime with
            | Some fr -> fr.UpdateSessionReturning(sessionID, incrementCompactionOrdinal)
            | None -> 0

        do! appendCompactionStartedOrFail directory sessionID compactionId currentGen humanTurnId compactionOrdinal

        match fallbackRuntime with
        | Some fr ->
            fr.UpdateSession(sessionID, transferOwnership SessionOwner.Compaction)
            fr.UpdateSession(sessionID, setActiveCompactionId compactionId compactionOrdinal humanTurnId cancelGen)
            fr.Update(sessionID, setCompacted false)
            fr.Update(sessionID, setCompactionContinuationObserved false)
            fr.UpdateSession(sessionID, setCompactionGeneration currentGen)
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
        | Some fr when (fr.GetSession sessionID).CompactionActiveId = compactionId ->
            let settleInfo = tryGetSettleInfo compactionId (fr.GetSession sessionID)

            match settleInfo with
            | Some(_, ordinal) ->
                do! appendCompactionSettledOrFail directory sessionID compactionId "failed" ordinal

                let _ = fr.UpdateSessionReturning(sessionID, applySettleReturning compactionId)
                ()
            | None -> ()

            // Clear the compaction summary transform bypass flag on error
            fr.UpdateSession(sessionID, clearCompactionSummaryTransformPending)
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
