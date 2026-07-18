module Wanxiangshu.Runtime.Fallback.CompactionHandler

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.SessionEventWriter

let settleActiveCompactionIfOwner
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    : JS.Promise<unit> =
    promise {
        if runtime.GetSessionOwner sessionID = SessionOwner.Compaction then
            let activeComp = (runtime.GetSession sessionID).CompactionActiveId

            match tryGetSettleInfo activeComp (runtime.GetSession sessionID) with
            | Some(_, ordinal) ->
                do! appendCompactionSettledOrFail workspaceRoot sessionID activeComp "cancelled" ordinal

                runtime.UpdateSessionReturning(sessionID, applySettleReturning activeComp)
                |> ignore
            | None -> ()
    }

let filterActionDuringCompaction
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (action: FallbackAction)
    : FallbackAction =
    if
        (runtime.GetSession sessionID).CompactionActiveId <> ""
        || (runtime.GetSession sessionID).CompactionCompacted
    then
        FallbackAction.DoNothing
    else
        action
