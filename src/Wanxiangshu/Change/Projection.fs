namespace Wanxiangshu.Change

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Work

[<RequireQualifiedAccess>]
type TerminalOutcome =
    | Published of
        {| CandidateCommit: CommitHash
           ResultingTargetHead: CommitHash |}
    | Failed of reason: string
    | Abandoned

type ManagerJobProjection =
    { ManagerJobId: ManagerJobId
      ManagerSessionId: SessionId
      ManagerAgent: string
      Byname: string
      WorktreeIdentity: WorktreeIdentity
      WorktreePath: WorktreePath
      TargetRef: TargetRef
      TargetBranchFrozen: string
      CandidateReady: {| CandidateCommit: CommitHash |} option
      ConflictDetected:
          {| CandidateCommit: CommitHash
             TargetHeadSnapshot: CommitHash
             ConflictFiles: string list
             DiagnosticsDigest: string |} option
      RebasedCandidateReady:
          {| RebasedCommit: CommitHash
             TargetHeadSnapshot: CommitHash |} option
      PublishClaimed:
          {| TargetRef: TargetRef
             RebasedCommit: CommitHash
             ExpectedHead: CommitHash
             AuthorityRevision: AuthorityRevision |} option
      Terminal: TerminalOutcome option }

[<RequireQualifiedAccess>]
type WorktreeEffectStatus =
    | Requested of
        {| ManagerJobId: ManagerJobId
           WorktreePath: WorktreePath |}
    | Created of
        {| ManagerJobId: ManagerJobId
           WorktreePath: WorktreePath |}

/// Complete evidence presented to the pure worktree reconciliation boundary.
/// Physical evidence is representable only for the ambiguous Requested state.
[<RequireQualifiedAccess>]
type WorktreeReconciliationObservation =
    | NoDurableEffect
    | RequestedConflict of recordedJobId: ManagerJobId * recordedPath: WorktreePath
    | RequestedAmbiguity of
        recordedJobId: ManagerJobId *
        recordedPath: WorktreePath *
        physical: Result<(WorktreePath * WorktreeIdentity option) list, string>
    | CreatedReceipt of recordedJobId: ManagerJobId * recordedPath: WorktreePath

[<RequireQualifiedAccess>]
type WorktreeReconciliationFailure =
    | DurableOwnershipConflict
    | WorktreeQueryFailed of string
    | PhysicalIdentityPathConflict

[<RequireQualifiedAccess>]
type WorktreeReconciliationDecision =
    | RequestThenCreate
    | CreateAfterProvenMissing
    | AdoptThenRecordCreated
    | AdoptCreated
    | Reject of WorktreeReconciliationFailure

type OrchestratorProjection =
    { Jobs: Map<ManagerJobId, ManagerJobProjection>
      WorktreeEffects: Map<WorktreeIdentity, WorktreeEffectStatus> }

[<RequireQualifiedAccess>]
type RebasedCandidateReality =
    | HeadUnreadable
    | PublishReady
    | NeedsRebase

[<RequireQualifiedAccess>]
type PublishClaimReality =
    | HeadUnreadable
    | AlreadyFastForwarded
    | PublishReady
    | ClaimExpired

/// wanxiangli/integration binding (CUT-23 ②); see Projection.fsi.
module ChangeIntegrationLaw =
    [<Fable.Core.Import("IntegrationLaw", "wanxiangli/integration")>]
    let private lawClass: obj = Fable.Core.Util.jsNative

    [<Fable.Core.Emit("new $0()")>]
    let private construct (_cls: obj) : obj = Fable.Core.Util.jsNative

    [<Fable.Core.Emit("$0[$1]($2)")>]
    let private invoke (_law: obj) (_name: string) (_input: obj) : obj = Fable.Core.Util.jsNative

    [<Fable.Core.Emit("$0[$1]")>]
    let private field (_value: obj) (_name: string) : obj = Fable.Core.Util.jsNative

    let private law: obj = construct lawClass

    let private ask (name: string) (pairs: (string * obj) list) : obj =
        invoke law name (Fable.Core.JsInterop.createObj pairs)

    let private code (name: string) (answer: obj) : string =
        let value = field answer name

        match value with
        | :? string as text -> text
        | _ -> invalidOp ("wanxiangli/integration returned no " + name)

    let internal text (value: string) : obj = box value

    let internal rebasedWire
        (rebased:
            {| RebasedCommit: CommitHash
               TargetHeadSnapshot: CommitHash |} option)
        : obj =
        match rebased with
        | None -> null
        | Some ready ->
            Fable.Core.JsInterop.createObj
                [ "rebasedCommit", text (CommitHash.value ready.RebasedCommit)
                  "targetHeadSnapshot", text (CommitHash.value ready.TargetHeadSnapshot) ]

    let resume (record: ManagerJobProjection option) : string =
        let wire =
            match record with
            | None -> null
            | Some job ->
                Fable.Core.JsInterop.createObj
                    [ "terminal", box job.Terminal.IsSome; "claimed", box job.PublishClaimed.IsSome ]

        ask "resume" [ "record", wire ] |> code "step"

    let reenterClaim
        (jobTargetRef: TargetRef)
        (claimTargetRef: TargetRef)
        (claimRebased: CommitHash)
        (claimExpected: CommitHash)
        (record: ManagerJobProjection option)
        : string =
        let wire =
            match record with
            | None -> null
            | Some job -> Fable.Core.JsInterop.createObj [ "rebased", rebasedWire job.RebasedCandidateReady ]

        let answer =
            ask
                "reenterClaim"
                [ "jobTargetRef", text (TargetRef.value jobTargetRef)
                  "claim",
                  Fable.Core.JsInterop.createObj
                      [ "targetRef", text (TargetRef.value claimTargetRef)
                        "rebasedCommit", text (CommitHash.value claimRebased)
                        "expectedHead", text (CommitHash.value claimExpected) ]
                  "record", wire ]

        match code "step" answer with
        | "Refuse" -> code "reason" answer
        | step -> step

    let admitCandidate candidate target rebased : string =
        let answer =
            ask
                "admitCandidate"
                [ "candidate", text (CommitHash.value candidate)
                  "target", text (CommitHash.value target)
                  "rebased", rebasedWire rebased ]

        match code "step" answer with
        | "Rebase" -> code "reason" answer
        | step -> step

    let certify candidate expectedHead rebased : string =
        let answer =
            ask
                "certify"
                [ "candidate", text (CommitHash.value candidate)
                  "expectedHead", text (CommitHash.value expectedHead)
                  "rebased", rebasedWire rebased ]

        if unbox<bool> (field answer "ok") then "Ok" else code "code" answer

    let rebaseFailed (conflictFiles: string list) : string =
        ask "rebaseFailed" [ "conflictFiles", box (List.toArray conflictFiles) ] |> code "step"

    let checkPublication jobId jobTargetRef evidenceJobId evidenceTargetRef evidenceExpectedHead current : string =
        let answer =
            ask
                "checkPublication"
                [ "jobId", text (ManagerJobId.value jobId)
                  "jobTargetRef", text (TargetRef.value jobTargetRef)
                  "evidence",
                  Fable.Core.JsInterop.createObj
                      [ "jobId", text (ManagerJobId.value evidenceJobId)
                        "targetRef", text (TargetRef.value evidenceTargetRef)
                        "expectedHead", text (CommitHash.value evidenceExpectedHead) ]
                  "current", text (CommitHash.value current) ]

        if unbox<bool> (field answer "ok") then "Ok" else code "code" answer

    let ffMerge (merge: Result<CommitHash, string>) (candidate: CommitHash) (targetMovedError: string) : string =
        let wire =
            match merge with
            | Ok landed -> Fable.Core.JsInterop.createObj [ "ok", box true; "landed", text (CommitHash.value landed) ]
            | Error error -> Fable.Core.JsInterop.createObj [ "ok", box false; "error", text error ]

        ask
            "ffMerge"
            [ "merge", wire
              "candidate", text (CommitHash.value candidate)
              "targetMovedError", text targetMovedError ]
        |> code "outcome"

    let internal reconcileWorktree (pairs: (string * obj) list) : obj = ask "reconcileWorktree" pairs
    let internal reality (name: string) (pairs: (string * obj) list) : string = ask name pairs |> code "reality"
    let internal answerField (answer: obj) (name: string) : string = code name answer

module OrchestratorProjection =

    let empty =
        { Jobs = Map.empty
          WorktreeEffects = Map.empty }

    let tryFind (jobId: ManagerJobId) (projection: OrchestratorProjection) = Map.tryFind jobId projection.Jobs

    let tryFindByByname (byname: string) (projection: OrchestratorProjection) =
        if System.String.IsNullOrWhiteSpace byname then
            None
        else
            let wanted = byname.Trim()

            let matchesWanted (job: ManagerJobProjection) =
                System.String.Equals(job.Byname, wanted, System.StringComparison.OrdinalIgnoreCase)

            projection.Jobs
            |> Map.tryPick (fun _ job -> if matchesWanted job then Some job else None)

    let tryWorktreeEffect (identity: WorktreeIdentity) (projection: OrchestratorProjection) =
        Map.tryFind identity projection.WorktreeEffects

    /// PERSIST-009: decide from durable intent plus, only when Requested is
    /// ambiguous, one complete physical worktree observation.
    let decideWorktreeReconciliation
        (jobId: ManagerJobId)
        (identity: WorktreeIdentity)
        (path: WorktreePath)
        (observation: WorktreeReconciliationObservation)
        : WorktreeReconciliationDecision =
        // PERSIST-009 is wanxiangli/integration `reconcileWorktree` (CUT-23 ②).
        let text = ChangeIntegrationLaw.text

        let owner kind (recordedJobId: ManagerJobId) (recordedPath: WorktreePath) extra =
            Fable.Core.JsInterop.createObj (
                [ "kind", text kind
                  "jobId", text (ManagerJobId.value recordedJobId)
                  "path", text (WorktreePath.value recordedPath) ]
                @ extra
            )

        let evidence =
            match observation with
            | WorktreeReconciliationObservation.NoDurableEffect ->
                Fable.Core.JsInterop.createObj [ "kind", text "NoDurableEffect" ]
            | WorktreeReconciliationObservation.CreatedReceipt(recordedJobId, recordedPath) ->
                owner "CreatedReceipt" recordedJobId recordedPath []
            | WorktreeReconciliationObservation.RequestedConflict(recordedJobId, recordedPath) ->
                owner "RequestedConflict" recordedJobId recordedPath []
            | WorktreeReconciliationObservation.RequestedAmbiguity(recordedJobId, recordedPath, Error error) ->
                owner "RequestedQueryFailure" recordedJobId recordedPath [ "error", text error ]
            | WorktreeReconciliationObservation.RequestedAmbiguity(recordedJobId, recordedPath, Ok entries) ->
                let wire =
                    entries
                    |> List.map (fun (observedPath, observedIdentity) ->
                        Fable.Core.JsInterop.createObj
                            [ "path", text (WorktreePath.value observedPath)
                              "identity",
                              (match observedIdentity with
                               | Some value -> text (WorktreeIdentity.value value)
                               | None -> null) ])
                    |> List.toArray

                owner "RequestedEntries" recordedJobId recordedPath [ "entries", box wire ]

        let answer =
            ChangeIntegrationLaw.reconcileWorktree
                [ "jobId", text (ManagerJobId.value jobId)
                  "identity", text (WorktreeIdentity.value identity)
                  "path", text (WorktreePath.value path)
                  "evidence", evidence ]

        match ChangeIntegrationLaw.answerField answer "decision" with
        | "RequestThenCreate" -> WorktreeReconciliationDecision.RequestThenCreate
        | "CreateAfterProvenMissing" -> WorktreeReconciliationDecision.CreateAfterProvenMissing
        | "AdoptThenRecordCreated" -> WorktreeReconciliationDecision.AdoptThenRecordCreated
        | "AdoptCreated" -> WorktreeReconciliationDecision.AdoptCreated
        | "Reject" ->
            match ChangeIntegrationLaw.answerField answer "reason" with
            | "DurableOwnershipConflict" ->
                WorktreeReconciliationDecision.Reject WorktreeReconciliationFailure.DurableOwnershipConflict
            | "PhysicalIdentityPathConflict" ->
                WorktreeReconciliationDecision.Reject WorktreeReconciliationFailure.PhysicalIdentityPathConflict
            | "WorktreeQueryFailed" ->
                WorktreeReconciliationDecision.Reject(
                    WorktreeReconciliationFailure.WorktreeQueryFailed(ChangeIntegrationLaw.answerField answer "error")
                )
            | other -> invalidOp ("wanxiangli/integration returned an unknown rejection: " + other)
        | other -> invalidOp ("wanxiangli/integration returned an unknown decision: " + other)

    let requestWorktree
        (identity: WorktreeIdentity)
        (path: WorktreePath)
        (jobId: ManagerJobId)
        (projection: OrchestratorProjection)
        =
        match Map.tryFind identity projection.WorktreeEffects with
        | Some _ -> projection
        | None ->
            { projection with
                WorktreeEffects =
                    Map.add
                        identity
                        (WorktreeEffectStatus.Requested
                            {| ManagerJobId = jobId
                               WorktreePath = path |})
                        projection.WorktreeEffects }

    let acceptWorktree
        (identity: WorktreeIdentity)
        (path: WorktreePath)
        (jobId: ManagerJobId)
        (projection: OrchestratorProjection)
        =
        let created =
            WorktreeEffectStatus.Created
                {| ManagerJobId = jobId
                   WorktreePath = path |}

        match Map.tryFind identity projection.WorktreeEffects with
        | Some(WorktreeEffectStatus.Created _) -> projection
        | _ ->
            { projection with
                WorktreeEffects = Map.add identity created projection.WorktreeEffects }

    let tryFindByManagerSession (managerSessionId: SessionId) (projection: OrchestratorProjection) =
        projection.Jobs
        |> Map.tryPick (fun _ job ->
            if job.ManagerSessionId = managerSessionId then
                Some job
            else
                None)

    let activeJobs (projection: OrchestratorProjection) =
        projection.Jobs
        |> Map.toList
        |> List.map snd
        |> List.filter (fun job -> job.Terminal.IsNone)

    let private effectiveByname (managerAgent: string) (byname: string) =
        if System.String.IsNullOrWhiteSpace byname then
            managerAgent
        else
            byname

    let createJob
        (job:
            {| ManagerJobId: ManagerJobId
               ManagerSessionId: SessionId
               ManagerAgent: string
               Byname: string
               WorktreeIdentity: WorktreeIdentity
               WorktreePath: WorktreePath
               TargetRef: TargetRef
               TargetBranchFrozen: string |})
        (projection: OrchestratorProjection)
        =
        if Map.containsKey job.ManagerJobId projection.Jobs then
            projection
        else
            { projection with
                Jobs =
                    Map.add
                        job.ManagerJobId
                        { ManagerJobId = job.ManagerJobId
                          ManagerSessionId = job.ManagerSessionId
                          ManagerAgent = job.ManagerAgent
                          Byname = effectiveByname job.ManagerAgent job.Byname
                          WorktreeIdentity = job.WorktreeIdentity
                          WorktreePath = job.WorktreePath
                          TargetRef = job.TargetRef
                          TargetBranchFrozen = job.TargetBranchFrozen
                          CandidateReady = None
                          ConflictDetected = None
                          RebasedCandidateReady = None
                          PublishClaimed = None
                          Terminal = None }
                        projection.Jobs }

    let private updateActiveJob
        (jobId: ManagerJobId)
        (update: ManagerJobProjection -> ManagerJobProjection)
        (projection: OrchestratorProjection)
        =
        match Map.tryFind jobId projection.Jobs with
        | Some job when job.Terminal.IsNone ->
            { projection with
                Jobs = Map.add jobId (update job) projection.Jobs }
        | _ -> projection

    let recordCandidateReady
        (jobId: ManagerJobId)
        (payload: {| CandidateCommit: CommitHash |})
        (projection: OrchestratorProjection)
        =
        updateActiveJob
            jobId
            (fun job ->
                { job with
                    CandidateReady = Some payload })
            projection

    let recordConflictDetected
        (jobId: ManagerJobId)
        (payload:
            {| CandidateCommit: CommitHash
               TargetHeadSnapshot: CommitHash
               ConflictFiles: string list
               DiagnosticsDigest: string |})
        (projection: OrchestratorProjection)
        =
        updateActiveJob
            jobId
            (fun job ->
                { job with
                    ConflictDetected = Some payload })
            projection

    let recordRebasedCandidateReady
        (jobId: ManagerJobId)
        (payload:
            {| RebasedCommit: CommitHash
               TargetHeadSnapshot: CommitHash |})
        (projection: OrchestratorProjection)
        =
        // Latest-event view mirroring Fold: exact replay is idempotent and a
        // later differing record supersedes. Terminal guard stays in
        // updateActiveJob so Published still settles the effect.
        updateActiveJob
            jobId
            (fun job ->
                { job with
                    RebasedCandidateReady = Some payload })
            projection

    let recordPublishClaimed
        (jobId: ManagerJobId)
        (payload:
            {| TargetRef: TargetRef
               RebasedCommit: CommitHash
               ExpectedHead: CommitHash
               AuthorityRevision: AuthorityRevision |})
        (projection: OrchestratorProjection)
        =
        // Latest-event view mirroring Fold: a retried publish with fresh
        // evidence supersedes the CAS-missed claim. Terminal guard stays in
        // updateActiveJob so Published still blocks replay.
        updateActiveJob
            jobId
            (fun job ->
                { job with
                    PublishClaimed = Some payload })
            projection

    let recordTerminal (jobId: ManagerJobId) (terminal: TerminalOutcome) (projection: OrchestratorProjection) =
        updateActiveJob jobId (fun job -> { job with Terminal = Some terminal }) projection

    let private headWire (currentHead: CommitHash option) : obj =
        match currentHead with
        | None -> null
        | Some head -> ChangeIntegrationLaw.text (CommitHash.value head)

    /// ORCH-007, owned by wanxiangli/integration `rebasedCandidate` (CUT-23 ②).
    let classifyRebasedCandidate
        (currentHead: CommitHash option)
        (rebasedCommit: CommitHash)
        (targetHeadSnapshot: CommitHash)
        : RebasedCandidateReality =
        match
            ChangeIntegrationLaw.reality
                "rebasedCandidate"
                [ "head", headWire currentHead
                  "rebasedCommit", ChangeIntegrationLaw.text (CommitHash.value rebasedCommit)
                  "targetHeadSnapshot", ChangeIntegrationLaw.text (CommitHash.value targetHeadSnapshot) ]
        with
        | "HeadUnreadable" -> RebasedCandidateReality.HeadUnreadable
        | "PublishReady" -> RebasedCandidateReality.PublishReady
        | "NeedsRebase" -> RebasedCandidateReality.NeedsRebase
        | other -> invalidOp ("wanxiangli/integration returned an unknown rebased reality: " + other)

    /// ORCH-007, owned by wanxiangli/integration `publishClaim` (CUT-23 ②):
    /// already-published first, then unchanged target, then expired.
    let classifyPublishClaim
        (currentHead: CommitHash option)
        (rebasedCommit: CommitHash)
        (expectedHead: CommitHash)
        : PublishClaimReality =
        match
            ChangeIntegrationLaw.reality
                "publishClaim"
                [ "head", headWire currentHead
                  "rebasedCommit", ChangeIntegrationLaw.text (CommitHash.value rebasedCommit)
                  "expectedHead", ChangeIntegrationLaw.text (CommitHash.value expectedHead) ]
        with
        | "HeadUnreadable" -> PublishClaimReality.HeadUnreadable
        | "AlreadyFastForwarded" -> PublishClaimReality.AlreadyFastForwarded
        | "PublishReady" -> PublishClaimReality.PublishReady
        | "ClaimExpired" -> PublishClaimReality.ClaimExpired
        | other -> invalidOp ("wanxiangli/integration returned an unknown claim reality: " + other)
