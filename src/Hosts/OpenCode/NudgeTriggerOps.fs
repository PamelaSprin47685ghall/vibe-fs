module Wanxiangshu.Hosts.Opencode.NudgeTriggerOps

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Runtime.NudgeEventWriter
open Wanxiangshu.Runtime.NudgeLease
open Wanxiangshu.Hosts.Opencode.NudgeEffect
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection

/// Read the current owner; in test runs, fall back to the last observed
/// role on the host session (assistant / toolResult -> Human).
let resolveOwner
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionIDStr: string)
    (isTest: bool)
    : JS.Promise<SessionOwner> =
    promise {
        let current = (fallbackRuntime.GetSession sessionIDStr).Owner

        if current <> SessionOwner.NoOwner then
            return current
        elif not isTest then
            let directory = pluginDirectoryFromCtx ctx

            do!
                appendNudgeOwnerUnknownOrFail
                    directory
                    sessionIDStr
                    "No owner inferred from runtime state or host messages"
                |> Promise.catch (fun ex ->
                    JS.console.error (
                        box
                            {| feature = "nudge"
                               session = sessionIDStr
                               error = "Failed to append nudge_owner_unknown event: " + ex.Message |}
                    )

                    ())

            return SessionOwner.NoOwner
        else
            match getClientFromPluginCtx ctx with
            | Error _ -> return SessionOwner.NoOwner
            | Ok client ->
                let arg = box {| path = box {| id = sessionIDStr |} |}
                let! resp = invokeClient client "messages" arg
                let data = Dyn.get resp "data"

                if not (Dyn.isNullish data) && Dyn.isArray data then
                    let messagesArr = data :?> obj array

                    if messagesArr.Length > 0 then
                        let lastMsg = messagesArr.[messagesArr.Length - 1]
                        let info = Dyn.get lastMsg "info"
                        let role = Dyn.str info "role"

                        if role = "assistant" || role = "toolResult" then
                            return SessionOwner.Human
                        else
                            return SessionOwner.NoOwner
                    else
                        return SessionOwner.NoOwner
                else
                    return SessionOwner.NoOwner
    }

/// Map (owner, isForce) to the TerminalOrigin that explains why the
/// session reached a terminal state.
let resolveOrigin
    (fallbackRuntime: FallbackRuntimeStore)
    (owner: SessionOwner)
    (isForce: bool)
    (sessionIDStr: string)
    : TerminalOrigin =
    match owner with
    | SessionOwner.Human ->
        if isForce then
            TerminalOrigin.HumanTurnAborted
        else
            TerminalOrigin.HumanTurnCompleted
    | SessionOwner.NoOwner -> TerminalOrigin.Unknown
    | SessionOwner.Fallback -> TerminalOrigin.FallbackContinuationCompleted
    | SessionOwner.Compaction ->
        let session = fallbackRuntime.GetSession sessionIDStr
        let isCompacted = session.CompactionCompacted

        let continuationObserved = session.CompactionContinuationObserved

        if isCompacted && continuationObserved then
            TerminalOrigin.CompactionContinuationCompleted
        else
            TerminalOrigin.Unknown
    | SessionOwner.Nudge -> TerminalOrigin.NudgeCompleted
    | SessionOwner.Title -> TerminalOrigin.TitleCompleted

/// True when the session still owns a non-terminal fallback continuation lease.
/// The snapshot is read once so a stale natural-stop event cannot clear the owner
/// between lease creation and dispatch.
let hasActiveFallbackContinuation (fallbackRuntime: FallbackRuntimeStore) (sessionIDStr: string) : bool =
    let session = fallbackRuntime.GetSession sessionIDStr

    session.Owner = SessionOwner.Fallback
    && (match session.PendingLease with
        | Some lease ->
            lease.Owner = SessionOwner.Fallback
            && lease.Status <> LeaseStatus.Settled
            && lease.Status <> LeaseStatus.Cancelled
        | None -> false)

/// Clear the owner slot for Fallback / Title ownership.
let clearOwnerSlot (fallbackRuntime: FallbackRuntimeStore) (owner: SessionOwner) (sessionIDStr: string) : unit =
    if owner = SessionOwner.Fallback || owner = SessionOwner.Title then
        fallbackRuntime.UpdateSession(sessionIDStr, transferOwnership SessionOwner.NoOwner)

/// Finish an outstanding nudge lease, if any, and clear the owner.
let finishNudgeLease (ctx: obj) (fallbackRuntime: FallbackRuntimeStore) (sessionIDStr: string) : JS.Promise<unit> =
    promise {
        match (fallbackRuntime.GetSession sessionIDStr).PendingNudgeLease with
        | Some lease ->
            let directory = pluginDirectoryFromCtx ctx

            if directory <> "" then
                do! finishNudge fallbackRuntime directory sessionIDStr lease NudgeOutcome.Settled "completed" "" ""
        | None -> fallbackRuntime.UpdateSession(sessionIDStr, transferOwnership SessionOwner.NoOwner)
    }

/// Settle a compaction run that has produced its continuation, if any.
let settleCompaction_ (ctx: obj) (fallbackRuntime: FallbackRuntimeStore) (sessionIDStr: string) : JS.Promise<unit> =
    promise {
        let activeComp = (fallbackRuntime.GetSession sessionIDStr).CompactionActiveId

        if activeComp <> "" then
            let directory = pluginDirectoryFromCtx ctx

            if directory <> "" then
                let settleInfo =
                    tryGetSettleInfo activeComp (fallbackRuntime.GetSession sessionIDStr)

                match settleInfo with
                | Some(_, ordinal) ->
                    do! appendCompactionSettledOrFail directory sessionIDStr activeComp "completed" ordinal

                    let _ =
                        fallbackRuntime.UpdateSessionReturning(sessionIDStr, applySettleReturning activeComp)

                    ()
                | None -> ()

            fallbackRuntime.Update(sessionIDStr, setCompacted false)
            fallbackRuntime.Update(sessionIDStr, setCompactionContinuationObserved false)
    }

/// Apply the post-terminal cleanup required for the current owner: free
/// the owner slot, finish the nudge lease, or settle a compaction run.
let applyPostTerminalCleanup
    (ctx: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (owner: SessionOwner)
    (sessionIDStr: string)
    : JS.Promise<unit> =
    promise {
        if owner = SessionOwner.Fallback || owner = SessionOwner.Title then
            clearOwnerSlot fallbackRuntime owner sessionIDStr
        elif owner = SessionOwner.Nudge then
            do! finishNudgeLease ctx fallbackRuntime sessionIDStr
        elif
            owner = SessionOwner.Compaction
            && (fallbackRuntime.GetSession sessionIDStr).CompactionCompacted
        then
            do! settleCompaction_ ctx fallbackRuntime sessionIDStr
    }

/// A terminal event is eligible for nudge dispatch when it is a natural
/// stop of a freshly-completed human turn.
let isNudgeEligible (origin: TerminalOrigin) (eventType: string) : bool =
    match origin with
    | TerminalOrigin.HumanTurnCompleted
    | TerminalOrigin.NudgeCompleted
    | TerminalOrigin.FallbackContinuationCompleted -> eventType <> "session.error"
    | TerminalOrigin.HumanTurnAborted
    | TerminalOrigin.CompactionSummaryCompleted
    | TerminalOrigin.CompactionContinuationCompleted
    | TerminalOrigin.TitleCompleted
    | TerminalOrigin.ToolSubturnCompleted
    | TerminalOrigin.Unknown -> false
