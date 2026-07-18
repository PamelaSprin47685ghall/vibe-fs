module Wanxiangshu.Hosts.Opencode.NudgeEffectPrompt

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.NudgeRuntimeState
open Wanxiangshu.Runtime.NudgeFlow
open Wanxiangshu.Runtime.NudgeModelResolver
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.ErrorClassify

module Dyn = Wanxiangshu.Runtime.Dyn

/// Parse a model value (string or { providerID, modelID, variant } object) into "provider/model" form.
let private parseModelVal (modelVal: obj) : string option =
    if Dyn.isNullish modelVal then
        None
    elif Dyn.typeIs modelVal "string" then
        let s = string modelVal
        if s = "" then None else Some s
    else
        let providerID = Dyn.str modelVal "providerID"
        let modelID = Dyn.str modelVal "modelID"
        let variant = Dyn.str modelVal "variant"
        let suffix = if variant <> "" then ":" + variant else ""

        if providerID = "" || modelID = "" then
            let idVal = Dyn.str modelVal "id"
            if idVal <> "" then Some(idVal + suffix) else None
        else
            Some(sprintf "%s/%s%s" providerID modelID suffix)

/// Derive a stable turn anchor from message info and array index.
let private getTurnId (info: obj) (idx: int) : string =
    let time = Dyn.get info "time"
    let completed = Dyn.str time "completed"

    if completed <> "" then
        completed
    else
        let msgId = Dyn.str info "id"

        if msgId <> "" then
            msgId
        else
            sprintf "nudge-fallback-anchor-%d" idx

/// Convert a NudgeSnapshotState fold result into a SessionSnapshot for the nudge decision engine.
let private buildSnapshotResult (snap: NudgeSnapshotState) : SessionSnapshot =
    let anchor = nudgeAnchorKey snap.turnId snap.lastAssistantText

    let blockStatus =
        if isBlocked { PendingNudge = snap.pendingNudge; LastDispatchedAnchor = snap.lastDispatchedAnchor } anchor
        then NudgeBlockStatus.Blocked
        else NudgeBlockStatus.Allowed

    sessionSnapshotFromFold snap RunnerPresence.Absent blockStatus
