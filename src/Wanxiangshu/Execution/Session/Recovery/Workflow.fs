namespace Wanxiangshu.Execution.Session.Recovery

#nowarn "0035"

open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.OpenCode
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Persistence.Journal

/// Direct-CE family recovery (FLOW-001 / P0-2).
/// GREEN-4: all ports are mandatory capabilities. Missing work is a query result
/// (NoLinkedHandles / NoRelatedJobs), never a missing port.
module SessionRecoveryWorkflow =

    /// Mandatory recovery ports (GREEN-4). Composition root must inject real queries.
    type SessionRecoveryPorts =
        { Journal: AgentJournal
          Snapshot: ISessionSnapshotPort
          BloggerHost: IBloggerRuntimeHost
          RecoverPromptClaims: SessionId -> Task<SessionRecovery>
          RecoverBlogger: SessionId -> Task<SessionRecovery>
          RestoreHandles: SessionId -> Task<HandleFamilyRecovery>
          RecoverJobs: SessionId -> Task<JobFamilyRecovery> }

    let private emptyReceipt (sessionId: SessionId) (sequence: int64) =
        RecoveryReceipt.create sessionId sequence None [] []

    let private sequenceOf (journal: AgentJournal) =
        JournalRevision.value (AgentJournal.revision journal)

    let private sessionOfNode =
        function
        | RecoveryNode.WorkSession id
        | RecoveryNode.AgentChild(_, id, _)
        | RecoveryNode.Companion(_, id)
        | RecoveryNode.Blogger(_, id)
        | RecoveryNode.ManagerJob(_, id) -> id

    /// Default RecoverBlogger implementation using BloggerCrashRecovery.
    let defaultRecoverBlogger
        (journal: AgentJournal)
        (host: IBloggerRuntimeHost)
        (snapshot: ISessionSnapshotPort)
        : SessionId -> Task<SessionRecovery> =
        // DSL-MUTABLE: algorithm-scratch — per-call blogger recovery memoization cache
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
                        | BloggerCrashRecovery.WindowOutcome.ReceiptedIdle id
                        | BloggerCrashRecovery.WindowOutcome.PendingMaterial id
                        | BloggerCrashRecovery.WindowOutcome.RestoredInFlight id
                        | BloggerCrashRecovery.WindowOutcome.AlreadyLive id
                        | BloggerCrashRecovery.WindowOutcome.Unreadable(id, _) -> id = sessionId
                        | BloggerCrashRecovery.WindowOutcome.AbandonedUnsent _
                        | BloggerCrashRecovery.WindowOutcome.Superseded _
                        | BloggerCrashRecovery.WindowOutcome.Recommitted _ -> false)

                match NonEmpty.ofList blocked with
                | Some blocks -> return SessionRecovery.SessionRecovery.Blocked blocks
                | None when touched -> return SessionRecovery.SessionRecovery.Recovered(emptyReceipt sessionId sequence)
                | None -> return SessionRecovery.SessionRecovery.NoRecoveryRequired(emptyReceipt sessionId sequence)
            }

    let private claimRecoveryBlock sessionId (item: PromptRecovery.Reconciled) =
        match item.Outcome with
        | PromptRecovery.ClaimOutcome.Unreadable reason -> Some(RecoveryBlock.SnapshotUnreadable(sessionId, reason))
        | PromptRecovery.ClaimOutcome.StillPending _ ->
            Some(RecoveryBlock.PendingClaimUnknown(sessionId, item.PromptKey))
        | PromptRecovery.ClaimOutcome.Proven _ -> None

    let private provenClaimKey (item: PromptRecovery.Reconciled) =
        match item.Outcome with
        | PromptRecovery.ClaimOutcome.Proven _ -> Some item.PromptKey
        | _ -> None

    let private recoverSessionClaims sessionId sequence forSession =
        let unreadable = forSession |> List.choose (claimRecoveryBlock sessionId)

        match NonEmpty.ofList unreadable with
        | Some blocks -> SessionRecovery.SessionRecovery.Blocked blocks
        | None ->
            let keys = forSession |> List.choose provenClaimKey

            SessionRecovery.SessionRecovery.Recovered(RecoveryReceipt.create sessionId sequence None keys [])

    /// Default RecoverPromptClaims using PromptRecovery.reconcile.
    let defaultRecoverPromptClaims
        (journal: AgentJournal)
        (snapshot: ISessionSnapshotPort)
        : SessionId -> Task<SessionRecovery> =
        // DSL-MUTABLE: algorithm-scratch — per-call prompt-claim recovery memoization cache
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

                match forSession with
                | [] -> return SessionRecovery.SessionRecovery.NoRecoveryRequired(emptyReceipt sessionId sequence)
                | _ -> return recoverSessionClaims sessionId sequence forSession
            }

    let private recoverManagerJobOutcome (ports: SessionRecoveryPorts) (jobId: ManagerJobId) : Task<SessionRecovery> =
        task {
            let sequence = sequenceOf ports.Journal
            let orch = (AgentJournal.snapshot ports.Journal).AgentProjections.Orchestrator

            match OrchestratorProjection.tryFind jobId orch with
            | None ->
                return
                    sessionRecoveryOfJobFamily
                        (SessionId.create (ManagerJobId.value jobId))
                        sequence
                        JobFamilyRecovery.NoRelatedJobs
            | Some job ->
                let! family = ports.RecoverJobs job.ManagerSessionId
                return sessionRecoveryOfJobFamily job.ManagerSessionId sequence family
        }

    let private nodeSessionAndJob (node: RecoveryNode) =
        match node with
        | RecoveryNode.WorkSession id -> id, None
        | RecoveryNode.AgentChild(_, id, _) -> id, None
        | RecoveryNode.Companion(_, id) -> id, None
        | RecoveryNode.Blogger(_, id) -> id, None
        | RecoveryNode.ManagerJob(jobId, id) -> id, Some jobId

    let private recoverJobParts (ports: SessionRecoveryPorts) maybeJob =
        match maybeJob with
        | None -> Task.FromResult([])
        | Some jobId ->
            task {
                let! jobOutcome = recoverManagerJobOutcome ports jobId
                return [ jobOutcome ]
            }

    let private recoverOneNode (ports: SessionRecoveryPorts) sequence node =
        task {
            let sessionId, maybeJob = nodeSessionAndJob node
            let! claimOutcome = ports.RecoverPromptClaims sessionId
            let! bloggerOutcome = ports.RecoverBlogger sessionId
            let! handleFamily = ports.RestoreHandles sessionId
            let handleOutcome = sessionRecoveryOfHandleFamily sessionId sequence handleFamily
            let! jobParts = recoverJobParts ports maybeJob
            let merged = combine (claimOutcome :: bloggerOutcome :: handleOutcome :: jobParts)
            return sessionId, merged
        }

    let rec private recoverNodesList
        (ports: SessionRecoveryPorts)
        sequence
        (nodes: RecoveryNode list)
        (acc: Map<SessionId, SessionRecovery>)
        : Task<Map<SessionId, SessionRecovery>> =
        task {
            match nodes with
            | [] -> return acc
            | node :: rest ->
                let! sessionId, merged = recoverOneNode ports sequence node
                return! recoverNodesList ports sequence rest (Map.add sessionId merged acc)
        }

    /// Child-first direct CE for one parent family (RECOVERY-FAMILY-001/002).
    let recoverFamilyDirect (ports: SessionRecoveryPorts) (parentSession: SessionId) : Task<FamilyRecovery> =
        task {
            let sequence = sequenceOf ports.Journal

            let closure =
                RecoveryClosureProjection.discover
                    parentSession
                    (AgentJournal.snapshot ports.Journal).AgentProjections
                    sequence

            match validateClosurePure closure with
            | Error blocks -> return FamilyRecovery.FamilyBlocked blocks
            | Ok validated ->
                let ordered = (ValidatedClosure.value validated).Nodes
                let! results = recoverNodesList ports sequence ordered Map.empty
                let closed = ValidatedClosure.value validated
                let recovered = { Closure = closed; Results = results }
                return authorizeFamilyResume parentSession closed.JournalSequence recovered
        }
