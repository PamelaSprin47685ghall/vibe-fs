namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Production interpreter for SessionRecoveryProgram (FLOW-003).
/// Reuses PromptRecovery / BloggerCrashRecovery as instruction handlers.
module SessionRecoveryInterpreter =

    type Ports =
        { Journal: AgentJournal option
          Snapshot: ISessionSnapshotPort option
          ParkedHost: IParkedTransformHost option
          RestoreHandles: (SessionId -> Task<SessionRecovery>) option
          RecoverJob: (ManagerJobId -> Task<SessionRecovery>) option }

    let private emptyReceipt (sessionId: SessionId) (sequence: int64) =
        RecoveryReceipt.create sessionId sequence None [] []

    let private sequenceOf (journal: AgentJournal option) =
        match journal with
        | None -> 0L
        | Some j -> JournalRevision.value (AgentJournal.revision j)

    let private bloggerOutcome
        (memo: ResizeArray<BloggerCrashRecovery.WindowOutcome> option ref)
        (journal: AgentJournal option)
        (host: IParkedTransformHost option)
        (snapshot: ISessionSnapshotPort option)
        (sessionId: SessionId)
        : Task<SessionRecovery> =
        task {
            let sequence = sequenceOf journal

            match host with
            | None -> return SessionRecovery.NoRecoveryRequired(emptyReceipt sessionId sequence)
            | Some parked ->
                let! outcomes =
                    match memo.Value with
                    | Some cached -> Task.FromResult(List.ofSeq cached)
                    | None ->
                        task {
                            let! list = BloggerCrashRecovery.reconcile journal parked snapshot
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
                        // Request-scoped outcomes: family pass already applied them.
                        | BloggerCrashRecovery.WindowOutcome.AbandonedUnsent _
                        | BloggerCrashRecovery.WindowOutcome.Recommitted _ -> false)

                match NonEmpty.ofList blocked with
                | Some blocks -> return SessionRecovery.Blocked blocks
                | None when touched -> return SessionRecovery.Recovered(emptyReceipt sessionId sequence)
                | None -> return SessionRecovery.NoRecoveryRequired(emptyReceipt sessionId sequence)
        }

    /// Interpret a family recovery program to FamilyRecovery.
    let interpretFamily (ports: Ports) (program: SessionRecoveryProgram<FamilyRecovery>) : Task<FamilyRecovery> =
        let bloggerMemo: ResizeArray<BloggerCrashRecovery.WindowOutcome> option ref =
            ref None
        // Prompt pass also once per family interpret.
        let claimsMemo: PromptRecovery.Reconciled list option ref = ref None

        let claimsOnce (sessionId: SessionId) =
            task {
                let sequence = sequenceOf ports.Journal

                let! reconciled =
                    match claimsMemo.Value with
                    | Some cached -> Task.FromResult cached
                    | None ->
                        task {
                            let! list = PromptRecovery.reconcile ports.Journal ports.Snapshot
                            claimsMemo.Value <- Some list
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

        let rec go (program: SessionRecoveryProgram<FamilyRecovery>) : Task<FamilyRecovery> =
            task {
                match program with
                | Return value -> return value
                | Block blocks -> return FamilyRecovery.FamilyBlocked blocks
                | DiscoverClosure(sessionId, next) ->
                    let sequence = sequenceOf ports.Journal

                    let closure =
                        match ports.Journal with
                        | None ->
                            { Root = sessionId
                              Nodes = [ RecoveryNode.WorkSession sessionId ]
                              Digest = SessionId.value sessionId
                              JournalSequence = sequence }
                        | Some journal ->
                            RecoveryClosureProjection.discover
                                sessionId
                                (AgentJournal.snapshot journal).AgentProjections
                                sequence

                    return! go (next closure)
                | ReadSessionSnapshot(sessionId, next) ->
                    match ports.Snapshot with
                    | None -> return! go (next ())
                    | Some snapshot ->
                        match! snapshot.GetMessages sessionId with
                        | Ok _ -> return! go (next ())
                        | Error reason ->
                            return
                                FamilyRecovery.FamilyBlocked(
                                    NonEmpty.one (RecoveryBlock.SnapshotUnreadable(sessionId, reason))
                                )
                | RecoverPromptClaims(sessionId, next) ->
                    let! outcome = claimsOnce sessionId

                    return!
                        go (
                            next
                                { SessionId = sessionId
                                  Outcome = outcome }
                        )
                | RecoverBloggerWindow(sessionId, next) ->
                    let! outcome = bloggerOutcome bloggerMemo ports.Journal ports.ParkedHost ports.Snapshot sessionId

                    return!
                        go (
                            next
                                { SessionId = sessionId
                                  Outcome = outcome }
                        )
                | RestoreLinkedHandles(sessionId, next) ->
                    let sequence = sequenceOf ports.Journal

                    let! outcome =
                        match ports.RestoreHandles with
                        | Some restore -> restore sessionId
                        | None -> Task.FromResult(SessionRecovery.NoRecoveryRequired(emptyReceipt sessionId sequence))

                    return!
                        go (
                            next
                                { SessionId = sessionId
                                  Outcome = outcome }
                        )
                | RecoverManagerJob(jobId, next) ->
                    let sequence = sequenceOf ports.Journal

                    let! outcome =
                        match ports.RecoverJob with
                        | Some recover -> recover jobId
                        | None ->
                            Task.FromResult(
                                SessionRecovery.NoRecoveryRequired(
                                    emptyReceipt (SessionId.create (ManagerJobId.value jobId)) sequence
                                )
                            )

                    return! go (next { JobId = jobId; Outcome = outcome })
                | ValidateClosure(closure, next) ->
                    match validateClosurePure closure with
                    | Error blocks -> return FamilyRecovery.FamilyBlocked blocks
                    | Ok validated -> return! go (next validated)
                | AuthorizeResume(recovered, next) ->
                    match authorizeFamilyResume recovered.Closure.Root recovered.Closure.JournalSequence recovered with
                    | FamilyRecovery.FamilyReady permit -> return! go (next permit)
                    | FamilyRecovery.FamilyBlocked blocks -> return FamilyRecovery.FamilyBlocked blocks
            }

        go program

    module Coordinator =
        let private gate = obj ()
        let private inflight = Dictionary<string, Task<FamilyRecovery>>()

        let recoverFamily (ports: Ports) (root: SessionId) : Task<FamilyRecovery> =
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
