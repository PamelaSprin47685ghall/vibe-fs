module Wanxiangshu.Runtime.Fallback.CompactionHandler

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.Fallback.CompactionTransitions
open Wanxiangshu.Runtime.SessionEventWriter

let settleActiveCompactionIfOwner
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    : JS.Promise<unit> =
    promise {
        if runtime.GetSessionOwner sessionID = SessionOwner.Compaction then
            let activeComp = runtime.GetActiveCompactionId sessionID

            match runtime.TryGetSettleInfo(sessionID, activeComp) with
            | Some(_, ordinal) ->
                do! appendCompactionSettledOrFail workspaceRoot sessionID activeComp "cancelled" ordinal
                runtime.ApplySettle(sessionID, activeComp) |> ignore
            | None -> ()
    }

let filterActionDuringCompaction
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (action: FallbackAction)
    : FallbackAction =
    if runtime.GetActiveCompactionId sessionID <> "" || runtime.IsCompacted sessionID then
        FallbackAction.DoNothing
    else
        action
