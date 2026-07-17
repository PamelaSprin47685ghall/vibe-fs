module Wanxiangshu.Runtime.Fallback.NudgeHandler

open Fable.Core
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.NudgeEventWriter

let cancelPendingNudge
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (reason: string)
    : JS.Promise<unit> =
    promise {
        match runtime.TryGetPendingNudgeLease sessionID with
        | Some nudgeLease ->
            do! appendNudgeCancelledOrFail workspaceRoot sessionID nudgeLease.NudgeID reason nudgeLease.NudgeOrdinal

            let _ = runtime.ApplyCancelNudgeLease(sessionID, nudgeLease.NudgeID)
            ()
        | None -> ()
    }
