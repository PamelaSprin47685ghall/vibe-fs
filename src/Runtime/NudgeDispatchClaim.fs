module Wanxiangshu.Runtime.NudgeDispatchClaim

open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.ProjectionCache
open Wanxiangshu.Runtime.EventLogCodec

let tryClaim
    (cache: ProjectionCache)
    (sessionId: string)
    (action: NudgeAction)
    (trimmedAnchor: string)
    (nudgeId: string)
    (nonce: string)
    (sessionGen: int)
    (cancelGen: int)
    (humanTurnId: string)
    (nudgeOrdinal: int)
    (isBlocked: NudgeDedupState -> string -> bool)
    (at: string)
    : WanEvent option =
    let canClaim =
        cache.CanClaimNudgeDispatch sessionId trimmedAnchor sessionGen cancelGen humanTurnId nudgeOrdinal isBlocked

    if canClaim then
        let payload =
            Map
                [ "action", toString action
                  "anchor", trimmedAnchor
                  "nudgeId", nudgeId
                  "nonce", nonce
                  "generation", sessionGen.ToString()
                  "cancelGeneration", cancelGen.ToString()
                  "humanTurnId", humanTurnId
                  "nudgeOrdinal", nudgeOrdinal.ToString() ]

        Some(buildEvent sessionId eventKindNudgeRequested payload at)
    else
        None
