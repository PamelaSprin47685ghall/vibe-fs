namespace Wanxiangshu.Mission.Planning

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// Entry point for the first prompt of a plan session (planning-020): opens
/// the work, binds the fixed DevOps and starts the first incumbency at S1.
/// Idempotent for a work that already exists; fails with zero writes when the
/// store cannot be read or an append fails.
module PlanWorkOpen =

    // Test doubles mark an unreadable store with TryCurrentThrows = true; a
    // store whose current projection cannot be read must fail the entry with
    // zero writes (planning-020 failure clause).
    let private storeReadThrows (store: IEventStore) : bool =
        emitJsExpr store "$0 != null && $0.TryCurrentThrows === true"

    // The work is already opened when the unified fold has its WorkOpened: the
    // entry appends nothing (planning-020). A read failure fails the entry with
    // zero writes.
    let private workAlreadyOpened (store: IEventStore) (workId: string) : Result<bool, string> =
        if storeReadThrows store then
            Error "Plan store projection cannot be read"
        else
            try
                Ok(Wanxiangshu.Mission.Lifecycle.LifecycleKernel.tryWorkOfStore store workId |> Option.isSome)
            with ex ->
                Error(sprintf "Plan store projection read failed: %s" ex.Message)

    // Shared with PlanTools.executeAsk's single-flight binding so the entry
    // and the first ask resolve the identical DevOps identity (planning-010).
    let internal deriveDevOpsId (workId: string) : string =
        "devops-" + (HostDigest.sha256Hex ("plan-devops:" + workId)).[..15]

    // The two opening events, one append; the first failure stops the entry.
    // A retry on an already-opened work appends nothing (planning-020).
    let private commitOpening
        (store: IEventStore)
        (sessionId: string)
        (root: string)
        (observedHead: int64 option)
        : Task<Result<unit, string>> =
        task {
            match
                Wanxiangshu.Mission.Lifecycle.LifecycleKernel.openWork
                    HostDigest.sha256Hex
                    Wanxiangshu.Mission.Lifecycle.OpeningKind.UserPlan
                    None
                    sessionId
                    root
                    false
                    observedHead
            with
            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Refuse reason -> return Error reason
            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Done _ -> return Ok()
            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Write(events, _) ->
                match! Wanxiangshu.Mission.Lifecycle.LifecycleKernel.commit store sessionId events with
                | Ok(Wanxiangshu.Mission.Lifecycle.CommitOutcome.AppendDurable) -> return Ok()
                | Ok(Wanxiangshu.Mission.Lifecycle.CommitOutcome.AppendNotAttempted) ->
                    return Error "plan work was not opened"
                | Ok(Wanxiangshu.Mission.Lifecycle.CommitOutcome.CommitUnknown) ->
                    return Error "plan work commit is unknown"
                | Error reason -> return Error reason
        }

    // planning-020 entry body: an already-opened work appends nothing; a fresh
    // one appends the two opening events. The opening cursor is the XTrace head
    // observed at opening; without it the entry refuses with zero writes.
    let private openPlanWork
        (store: IEventStore)
        (root: string)
        (sessionId: string)
        (observedHead: int64 option)
        : Task<Result<unit, string>> =
        task {
            match workAlreadyOpened store sessionId with
            | Error readError -> return Error readError
            | Ok true -> return Ok()
            | Ok false -> return! commitOpening store sessionId root observedHead
        }

    let ensurePlanWorkOpened
        (store: IEventStore)
        (root: string)
        (sessionId: string)
        (observedHead: int64 option)
        : Task<Result<unit, string>> =
        if String.IsNullOrWhiteSpace sessionId then
            Task.FromResult(Error "Plan work session id cannot be empty")
        else
            openPlanWork store root sessionId observedHead
