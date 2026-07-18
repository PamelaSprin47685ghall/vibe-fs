module Wanxiangshu.Runtime.Fallback.NudgeHandler

open Fable.Core
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.NudgeEventWriter

let cancelPendingNudge
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (reason: string)
    : JS.Promise<unit> =
    promise {
        match (runtime.GetSession sessionID).PendingNudgeLease with
        | Some nudgeLease ->
            do! appendNudgeCancelledOrFail workspaceRoot sessionID nudgeLease.NudgeID reason nudgeLease.NudgeOrdinal

            let applied =
                runtime.UpdateSessionReturning(sessionID, applyCancelNudgeLeaseReturning nudgeLease.NudgeID)

            if applied then
                runtime.TriggerStateChanged sessionID
        | None -> ()
    }
