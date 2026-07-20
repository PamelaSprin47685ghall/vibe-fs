module Wanxiangshu.Hosts.Opencode.NudgeTriggerCleanup

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeCompactionPure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Hosts.Opencode.NudgeEffect
open Wanxiangshu.Runtime.NudgeLease

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
