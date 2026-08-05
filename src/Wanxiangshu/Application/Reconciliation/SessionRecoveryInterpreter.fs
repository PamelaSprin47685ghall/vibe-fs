namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
// OrchestratorProjection lives in Journal (active ManagerJob lookup).

/// Production interpreter for SessionRecoveryProgram (FLOW-003).
/// GREEN-4: all ports are mandatory capabilities. Missing work is a query result
/// (NoLinkedHandles / NoRelatedJobs), never a missing port.
module SessionRecoveryInterpreter =

    /// Mandatory recovery ports (GREEN-4). Composition root must inject real queries.
    type SessionRecoveryPorts =
        { Journal: AgentJournal
          Snapshot: ISessionSnapshotPort
          ParkedHost: IParkedTransformHost
          RecoverPromptClaims: SessionId -> Task<SessionRecovery>
          RecoverBlogger: SessionId -> Task<SessionRecovery>
          RestoreHandles: SessionId -> Task<HandleFamilyRecovery>
          RecoverJobs: SessionId -> Task<JobFamilyRecovery> }

    /// Alias kept for existing call sites (AttachFamilyRecoveryPorts).
    type Ports = SessionRecoveryPorts

    let private emptyReceipt (sessionId: SessionId) (sequence: int64) =
        RecoveryReceipt.create sessionId sequence None [] []

    let private sequenceOf (journal: AgentJournal) =
        JournalRevision.value (AgentJournal.revision journal)

    /// Default RecoverBlogger implementation using BloggerCrashRecovery.
    let defaultRecoverBlogger
        (journal: AgentJournal)
        (host: IParkedTransformHost)
        (snapshot: ISessionSnapshotPort)
        : SessionId -> Task<SessionRecovery> =
        let memo: ResizeArray<BloggerCrashRecovery.WindowOutcome> option ref = ref None

        fun (sessionId: SessionId) ->
            task {
                let sequence = sequenceOf journal

                let! outcomes =
                    match memo.Value with
                    | Some cached -> Task.FromResult(List.ofSeq cached)
                    | None ->
                        task {
                            let! list = BloggerCrashRecovery.reconcile (Some journal) host (Some snapshot)
                            memo.Value <- Some(ResizeArray list)
                            return list
                        }

                let blocked =
                    outcomes
                    |> List.choose (function
                        | BloggerCrashRecovery.WindowOutcome.Unreadable(id, reason) when id = sessionId ->
                            Some(RecoveryBlock.SnapshotUnreadable(id, reason))
                        | _ -> None)

                let touched =
                    outcomes
                    |> List.exists (function
                        | BloggerCrashRecovery.WindowOutcome.RestoredParked id
                        | BloggerCrashRecovery.WindowOutcome.PendingMaterial id
                        | BloggerCrashRecovery.WindowOutcome.RestoredInFlight id
                        | BloggerCrashRecovery.WindowOutcome.AlreadyLive id
                        | BloggerCrashRecovery.WindowOutcome.Unreadable(id, _) -> id = sessionId
                        | BloggerCrashRecovery.WindowOutcome.AbandonedUnsent _
                        | BloggerCrashRecovery.WindowOutcome.Recommitted _ -> false)

                match NonEmpty.ofList blocked with
                | Some blocks -> return SessionRecovery.Blocked blocks
                | None when touched -> return SessionRecovery.Recovered(emptyReceipt sessionId sequence)
                | None -> return SessionRecovery.NoRecoveryRequired(emptyReceipt sessionId sequence)
            }

    /// Default RecoverPromptClaims using PromptRecovery.reconcile.
    let defaultRecoverPromptClaims
        (journal: AgentJournal)
        (snapshot: ISessionSnapshotPort)
        : SessionId -> Task<SessionRecovery> =
        let memo: PromptRecovery.Reconciled list option ref = ref None

        fun (sessionId: SessionId) ->
            task {
                let sequence = sequenceOf journal

                let! reconciled =
                    match memo.Value with
                    | Some cached -> Task.FromResult cached
                    | None ->
                        task {
                            let! list = PromptRecovery.reconcile (Some journal) (Some snapshot)
                            memo.Value <- Some list
                            return list
                        }

                let forSession = reconciled |> List.filter (fun item -> item.SessionId = sessionId)

                if List.isEmpty forSession then
                    return SessionRecovery.NoRecoveryRequired(emptyReceipt sessionId sequence)
                else
                    let unreadable =
                        forSession
                        |> List.choose (fun item ->
                            match item.Outcome with
                            | PromptRecovery.ClaimOutcome.Unreadable reason ->
                                Some(RecoveryBlock.SnapshotUnreadable(sessionId, reason))
                            | PromptRecovery.ClaimOutcome.StillPending _ ->
                                Some(RecoveryBlock.PendingClaimUnknown(sessionId, item.PromptKey))
                            | PromptRecovery.ClaimOutcome.Proven _
                            | PromptRecovery.ClaimOutcome.GaveUp -> None)

                    match NonEmpty.ofList unreadable with
                    | Some blocks -> return SessionRecovery.Blocked blocks
                    | None ->
                        let keys =
                            forSession
                            |> List.choose (fun item ->
                                match item.Outcome with
                                | PromptRecovery.ClaimOutcome.Proven _ -> Some item.PromptKey
                                | _ -> None)

                        return SessionRecovery.Recovered(RecoveryReceipt.create sessionId sequence None keys [])
            }

    /// Interpret a family recovery program to FamilyRecovery.
    let interpretFamily
        (ports: SessionRecoveryPorts)
        (program: SessionRecoveryProgram<FamilyRecovery>)
        : Task<FamilyRecovery> =
        let rec go (program: SessionRecoveryProgram<FamilyRecovery>) : Task<FamilyRecovery> =
            task {
                match program with
                | Return value -> return value
                | Block blocks -> return FamilyRecovery.FamilyBlocked blocks
                | DiscoverClosure(sessionId, next) ->
                    let sequence = sequenceOf ports.Journal

                    let closure =
                        RecoveryClosureProjection.discover
                            sessionId
                            (AgentJournal.snapshot ports.Journal).AgentProjections
                            sequence

                    return! go (next closure)
                | ReadSessionSnapshot(sessionId, next) ->
                    match! ports.Snapshot.GetMessages sessionId with
                    | Ok _ -> return! go (next ())
                    | Error reason ->
                        return
                            FamilyRecovery.FamilyBlocked(
                                NonEmpty.one (RecoveryBlock.SnapshotUnreadable(sessionId, reason))
                            )
                | RecoverPromptClaims(sessionId, next) ->
                    let! outcome = ports.RecoverPromptClaims sessionId

                    return!
                        go (
                            next
                                { SessionId = sessionId
                                  Outcome = outcome }
                        )
                | RecoverBloggerWindow(sessionId, next) ->
                    let! outcome = ports.RecoverBlogger sessionId

                    return!
                        go (
                            next
                                { SessionId = sessionId
                                  Outcome = outcome }
                        )
                | RestoreLinkedHandles(sessionId, next) ->
                    let sequence = sequenceOf ports.Journal
                    let! family = ports.RestoreHandles sessionId
                    let outcome = sessionRecoveryOfHandleFamily sessionId sequence family

                    return!
                        go (
                            next
                                { SessionId = sessionId
                                  Outcome = outcome }
                        )
                | RecoverManagerJob(jobId, next) ->
                    let sequence = sequenceOf ports.Journal
                    // Job nodes carry ManagerJobId; session-scoped RecoverJobs is used from
                    // recoverFamily via manager session id on ManagerJob/Reviewer nodes.
                    // Single-job: look up durable projection through RecoverJobs(managerSession)
                    // when known; else treat as NoRelatedJobs (determined, may issue permit).
                    let orch = (AgentJournal.snapshot ports.Journal).AgentProjections.Orchestrator

                    let! outcome =
                        match OrchestratorProjection.tryFind jobId orch with
                        | None ->
                            Task.FromResult(
                                sessionRecoveryOfJobFamily
                                    (SessionId.create (ManagerJobId.value jobId))
                                    sequence
                                    JobFamilyRecovery.NoRelatedJobs
                            )
                        | Some job ->
                            task {
                                let! family = ports.RecoverJobs job.ManagerSessionId
                                return sessionRecoveryOfJobFamily job.ManagerSessionId sequence family
                            }

                    return! go (next { JobId = jobId; Outcome = outcome })
                | ValidateClosure(closure, next) ->
                    match validateClosurePure closure with
                    | Error blocks -> return FamilyRecovery.FamilyBlocked blocks
                    | Ok validated -> return! go (next validated)
                | AuthorizeResume(recovered, next) ->
                    match authorizeFamilyResume recovered.Closure.Root recovered.Closure.JournalSequence recovered with
                    | FamilyRecovery.FamilyReady permit -> return! go (next permit)
                    | FamilyRecovery.FamilyWaiting waits -> return FamilyRecovery.FamilyWaiting waits
                    | FamilyRecovery.FamilyBlocked blocks -> return FamilyRecovery.FamilyBlocked blocks
            }

        go program

    module Coordinator =
        let private gate = obj ()
        let private inflight = Dictionary<string, Task<FamilyRecovery>>()

        let recoverFamily (ports: SessionRecoveryPorts) (root: SessionId) : Task<FamilyRecovery> =
            let key = SessionId.value root

            lock gate (fun () ->
                match inflight.TryGetValue key with
                | true, existing -> existing
                | false, _ ->
                    let started =
                        task {
                            try
                                return! interpretFamily ports (recoverFamily root)
                            finally
                                lock gate (fun () -> inflight.Remove key |> ignore)
                        }

                    inflight.[key] <- started
                    started)
