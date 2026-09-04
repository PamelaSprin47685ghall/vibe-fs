namespace Wanxiangshu.Change

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process
open Wanxiangshu.Git
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Mission.Relay

/// JS-native boundary for change integration. Job projections and git/worktree
/// resources stay behind opaque handles; answers and failures are plain objects.
module ChangeSurface =

    type private ProjectionHandle(projection: OrchestratorProjection) =
        member _.Projection = projection

    type private GitHandle(port: GitPort) =
        member _.Port = port

    type private WorktreeHandle(resource: WorktreeResource) =
        member _.Resource = resource

    type private GateHandle(gate: IntegrationGate) =
        member _.Gate = gate

    [<Emit("$0==null")>]
    let private isNullish (value: obj) : bool = jsNative

    [<Emit("Promise.resolve($0)")>]
    let private asPromise (value: obj) : JS.Promise<obj> = jsNative

    [<Emit("$0($1)")>]
    let private apply1 (fn: obj) (arg: obj) : obj = jsNative

    let private property (value: obj) (name: string) : obj = emitJsExpr (value, name) "$0[$1]"

    [<Emit("undefined")>]
    let private jsUndefined: obj = jsNative

    let private optionObj (value: 'T option) : obj =
        match value with
        | Some value -> box value
        | None -> jsUndefined

    let private stringOf (value: obj) =
        if isNullish value then "" else string value

    let private field (value: obj) (names: string list) : obj =
        names
        |> List.tryPick (fun name ->
            let result = property value name
            if isNullish result then None else Some result)
        |> Option.defaultValue null

    let private stringField value names = stringOf (field value names)

    let private stringArray (value: obj) : string array =
        if isNullish value then
            [||]
        else
            unbox<obj array> value |> Array.map stringOf

    let private jobId value = ManagerJobId.create (stringOf value)
    let private sessionId value = SessionId.create (stringOf value)
    let private commit value = CommitHash.create (stringOf value)

    let private snapshotId value =
        WorkspaceSnapshotId.create (stringOf value)

    let private certificateId value =
        QualityCertificateId.create (stringOf value)

    let private worktreeIdentityValue value =
        WorktreeIdentity.create (stringOf value)

    let private worktreePathValue value = WorktreePath.create (stringOf value)
    let private target value = TargetRef.create (stringOf value)

    let private factKindAndPayload (value: obj) =
        stringField value [ "kind"; "case"; "name" ], field value [ "payload"; "value"; "data" ]

    let fact (kind: string) (payload: obj) : obj =
        box {| kind = kind; payload = payload |}

    let private recordFactValue
        (projection: OrchestratorProjection)
        (managerJobId: ManagerJobId)
        (value: obj)
        : OrchestratorProjection =
        let kind, payload = factKindAndPayload value

        match kind with
        | "CandidateReady" ->
            OrchestratorProjection.recordCandidateReady
                managerJobId
                {| CandidateCommit = commit (field payload [ "candidateCommit"; "CandidateCommit" ])
                   WorkspaceSnapshotId = snapshotId (field payload [ "workspaceSnapshotId"; "WorkspaceSnapshotId" ])
                   QualityCertificateId =
                    certificateId (field payload [ "qualityCertificateId"; "QualityCertificateId" ]) |}
                projection
        | "ConflictDetected" ->
            OrchestratorProjection.recordConflictDetected
                managerJobId
                {| CandidateCommit = commit (field payload [ "candidateCommit"; "CandidateCommit" ])
                   TargetHeadSnapshot = commit (field payload [ "targetHeadSnapshot"; "TargetHeadSnapshot" ])
                   WorkspaceSnapshotId = snapshotId (field payload [ "workspaceSnapshotId"; "WorkspaceSnapshotId" ])
                   ConflictFiles = stringArray (field payload [ "conflictFiles"; "ConflictFiles" ]) |> Array.toList
                   DiagnosticsDigest = stringField payload [ "diagnosticsDigest"; "DiagnosticsDigest" ] |}
                projection
        | "RebasedCandidateReady" ->
            OrchestratorProjection.recordRebasedCandidateReady
                managerJobId
                {| RebasedCommit = commit (field payload [ "rebasedCommit"; "RebasedCommit" ])
                   TargetHeadSnapshot = commit (field payload [ "targetHeadSnapshot"; "TargetHeadSnapshot" ])
                   WorkspaceSnapshotId = snapshotId (field payload [ "workspaceSnapshotId"; "WorkspaceSnapshotId" ]) |}
                projection
        | "PublishClaimed" ->
            OrchestratorProjection.recordPublishClaimed
                managerJobId
                {| RebasedCommit = commit (field payload [ "rebasedCommit"; "RebasedCommit" ])
                   ExpectedHead = commit (field payload [ "expectedHead"; "ExpectedHead" ]) |}
                projection
        | "Published" ->
            OrchestratorProjection.recordTerminal
                managerJobId
                (TerminalOutcome.Published
                    {| CandidateCommit = commit (field payload [ "candidateCommit"; "CandidateCommit" ])
                       ResultingTargetHead = commit (field payload [ "resultingTargetHead"; "ResultingTargetHead" ]) |})
                projection
        | "JobFailed" ->
            OrchestratorProjection.recordTerminal
                managerJobId
                (TerminalOutcome.Failed(stringField payload [ "reason"; "Reason" ]))
                projection
        | "JobAbandoned" -> OrchestratorProjection.recordTerminal managerJobId TerminalOutcome.Abandoned projection
        | unknown -> invalidArg "fact" ("unknown ManagerJob fact: " + unknown)

    let private jobObject (job: ManagerJobProjection) : obj =
        let facts =
            [ if job.CandidateReady.IsSome then
                  yield "CandidateReady"

              if job.ConflictDetected.IsSome then
                  yield "ConflictDetected"

              if job.RebasedCandidateReady.IsSome then
                  yield "RebasedCandidateReady"

              if job.PublishClaimed.IsSome then
                  yield "PublishClaimed"

              match job.Terminal with
              | Some(TerminalOutcome.Published _) -> yield "Published"
              | Some(TerminalOutcome.Failed _) -> yield "JobFailed"
              | Some TerminalOutcome.Abandoned -> yield "JobAbandoned"
              | None -> () ]
            |> List.toArray

        box
            {| jobId = ManagerJobId.value job.ManagerJobId
               managerSessionId = SessionId.value job.ManagerSessionId
               managerAgent = job.ManagerAgent
               byname = job.Byname
               worktreeIdentity = WorktreeIdentity.value job.WorktreeIdentity
               worktreePath = WorktreePath.value job.WorktreePath
               targetRef = TargetRef.value job.TargetRef
               targetBranchFrozen = job.TargetBranchFrozen
               facts = facts |}

    let private createPayload (value: obj) =
        {| ManagerJobId = jobId (field value [ "jobId"; "ManagerJobId" ])
           ManagerSessionId = sessionId (field value [ "managerSessionId"; "ManagerSessionId" ])
           ManagerAgent = stringField value [ "managerAgent"; "ManagerAgent" ]
           Byname = stringField value [ "byname"; "Byname" ]
           WorktreeIdentity = worktreeIdentityValue (field value [ "worktreeIdentity"; "WorktreeIdentity" ])
           WorktreePath = worktreePathValue (field value [ "worktreePath"; "WorktreePath" ])
           TargetRef = target (field value [ "targetRef"; "TargetRef" ])
           TargetBranchFrozen = stringField value [ "targetBranchFrozen"; "TargetBranchFrozen" ] |}

    let empty () : obj =
        ProjectionHandle OrchestratorProjection.empty :> obj

    let createJob (projection: obj) (payload: obj) : obj =
        let current = (projection :?> ProjectionHandle).Projection
        ProjectionHandle(OrchestratorProjection.createJob (createPayload payload) current) :> obj

    let recordFact (projection: obj) (job: string) (value: obj) : obj =
        let current = (projection :?> ProjectionHandle).Projection
        ProjectionHandle(recordFactValue current (jobId job) value) :> obj

    let find (projection: obj) (job: string) : obj =
        let current = (projection :?> ProjectionHandle).Projection

        match OrchestratorProjection.tryFind (jobId job) current with
        | Some value -> jobObject value
        | None -> null

    let activeJobs (projection: obj) : obj array =
        let current = (projection :?> ProjectionHandle).Projection
        OrchestratorProjection.activeJobs current |> List.map jobObject |> List.toArray

    /// ORCH-007 domain classification for a rebased candidate. Returns a
    /// physical-world classification, not a program counter.
    let classifyRebasedCandidate (head: obj) (rebasedCommit: string) (targetHeadSnapshot: string) : obj =
        let currentHead = if isNullish head then None else Some(commit head)

        let reality =
            OrchestratorProjection.classifyRebasedCandidate
                currentHead
                (commit rebasedCommit)
                (commit targetHeadSnapshot)

        match reality with
        | RebasedCandidateReality.HeadUnreadable -> box {| kind = "HeadUnreadable" |}
        | RebasedCandidateReality.PublishReady -> box {| kind = "PublishReady" |}
        | RebasedCandidateReality.NeedsRebase -> box {| kind = "NeedsRebase" |}

    /// ORCH-007 domain classification for a publish claim. Three branches in
    /// fixed order: already-published first, then unchanged target, then
    /// everything else.
    let classifyPublishClaim (head: obj) (rebasedCommit: string) (expectedHead: string) : obj =
        let currentHead = if isNullish head then None else Some(commit head)

        let reality =
            OrchestratorProjection.classifyPublishClaim currentHead (commit rebasedCommit) (commit expectedHead)

        match reality with
        | PublishClaimReality.HeadUnreadable -> box {| kind = "HeadUnreadable" |}
        | PublishClaimReality.AlreadyFastForwarded -> box {| kind = "AlreadyFastForwarded" |}
        | PublishClaimReality.PublishReady -> box {| kind = "PublishReady" |}
        | PublishClaimReality.ClaimExpired -> box {| kind = "ClaimExpired" |}

    let requestWorktree (projection: obj) (identity: string) (path: string) (job: string) : obj =
        let current = (projection :?> ProjectionHandle).Projection

        ProjectionHandle(
            OrchestratorProjection.requestWorktree
                (worktreeIdentityValue identity)
                (worktreePathValue path)
                (jobId job)
                current
        )
        :> obj

    let acceptWorktree (projection: obj) (identity: string) (path: string) (job: string) : obj =
        let current = (projection :?> ProjectionHandle).Projection

        ProjectionHandle(
            OrchestratorProjection.acceptWorktree
                (worktreeIdentityValue identity)
                (worktreePathValue path)
                (jobId job)
                current
        )
        :> obj

    let worktreeEffect (projection: obj) (identity: string) : obj =
        let current = (projection :?> ProjectionHandle).Projection

        match OrchestratorProjection.tryWorktreeEffect (worktreeIdentityValue identity) current with
        | None -> null
        | Some status ->
            match status with
            | WorktreeEffectStatus.Requested _ -> box "Requested"
            | WorktreeEffectStatus.Created _ -> box "Created"

    /// JS-native semantic surface for the pure Requested/Created reconciliation law.
    let worktreeReconciliationDecision (job: string) (identity: string) (path: string) (evidence: obj) : obj =
        let evidenceKind = stringField evidence [ "kind" ]
        let recordedJob = jobId (field evidence [ "jobId" ])
        let recordedPath = worktreePathValue (field evidence [ "path" ])

        let entries () =
            field evidence [ "entries" ]
            |> unbox<obj array>
            |> Array.map (fun entry ->
                let identityValue = field entry [ "identity" ]

                worktreePathValue (field entry [ "path" ]),
                if isNullish identityValue then
                    None
                else
                    Some(worktreeIdentityValue identityValue))
            |> Array.toList

        let observation =
            match evidenceKind with
            | "NoDurableEffect" -> WorktreeReconciliationObservation.NoDurableEffect
            | "CreatedReceipt" -> WorktreeReconciliationObservation.CreatedReceipt(recordedJob, recordedPath)
            | "RequestedConflict" -> WorktreeReconciliationObservation.RequestedConflict(recordedJob, recordedPath)
            | "RequestedEntries" ->
                WorktreeReconciliationObservation.RequestedAmbiguity(recordedJob, recordedPath, Ok(entries ()))
            | "RequestedQueryFailure" ->
                WorktreeReconciliationObservation.RequestedAmbiguity(
                    recordedJob,
                    recordedPath,
                    Error(stringField evidence [ "error" ])
                )
            | unknown -> invalidArg "evidence" ("unknown worktree reconciliation evidence: " + unknown)

        let decision =
            OrchestratorProjection.decideWorktreeReconciliation
                (ManagerJobId.create job)
                (WorktreeIdentity.create identity)
                (WorktreePath.create path)
                observation

        match decision with
        | WorktreeReconciliationDecision.RequestThenCreate -> box {| kind = "RequestThenCreate" |}
        | WorktreeReconciliationDecision.CreateAfterProvenMissing -> box {| kind = "CreateAfterProvenMissing" |}
        | WorktreeReconciliationDecision.AdoptThenRecordCreated -> box {| kind = "AdoptThenRecordCreated" |}
        | WorktreeReconciliationDecision.AdoptCreated -> box {| kind = "AdoptCreated" |}
        | WorktreeReconciliationDecision.Reject failure ->
            let reason =
                match failure with
                | WorktreeReconciliationFailure.DurableOwnershipConflict -> "DurableOwnershipConflict"
                | WorktreeReconciliationFailure.WorktreeQueryFailed _ -> "WorktreeQueryFailed"
                | WorktreeReconciliationFailure.PhysicalIdentityPathConflict -> "PhysicalIdentityPathConflict"

            box {| kind = "Reject"; reason = reason |}

    let private foldPublishClaimed (projection: OrchestratorProjection) (managerJobId: ManagerJobId) (payload: obj) =
        match
            OrchestratorProjection.tryFind managerJobId projection
            |> Option.bind (fun job -> job.RebasedCandidateReady)
        with
        | Some rebased ->
            Ok(
                OrchestratorProjection.recordPublishClaimed
                    managerJobId
                    {| RebasedCommit = rebased.RebasedCommit
                       ExpectedHead = commit (field payload [ "expectedHead"; "ExpectedHead" ]) |}
                    projection
            )
        | None -> Error "publish claimed for a job with no rebased candidate (ORCH-004)"

    let private applyEvent (projection: OrchestratorProjection) (event: obj) : Result<OrchestratorProjection, string> =
        let kind = stringField event [ "kind"; "case"; "type" ]
        let payload = field event [ "payload"; "value"; "data" ]

        match kind with
        | "ManagerJobCreated" -> Ok(OrchestratorProjection.createJob (createPayload payload) projection)
        | "PublishClaimed" -> foldPublishClaimed projection (jobId (field payload [ "jobId"; "ManagerJobId" ])) payload
        | "CandidateReady"
        | "ConflictDetected"
        | "RebasedCandidateReady"
        | "Published"
        | "JobFailed"
        | "JobAbandoned" ->
            Ok(recordFactValue projection (jobId (field payload [ "jobId"; "ManagerJobId" ])) (fact kind payload))
        | "WorktreeCreateRequested" ->
            Ok(
                OrchestratorProjection.requestWorktree
                    (worktreeIdentityValue (field payload [ "worktreeIdentity"; "WorktreeIdentity" ]))
                    (worktreePathValue (field payload [ "worktreePath"; "WorktreePath" ]))
                    (jobId (field payload [ "jobId"; "ManagerJobId" ]))
                    projection
            )
        | "WorktreeCreated" ->
            Ok(
                OrchestratorProjection.acceptWorktree
                    (worktreeIdentityValue (field payload [ "worktreeIdentity"; "WorktreeIdentity" ]))
                    (worktreePathValue (field payload [ "worktreePath"; "WorktreePath" ]))
                    (jobId (field payload [ "jobId"; "ManagerJobId" ]))
                    projection
            )
        | unknown -> Error("unknown orchestrator event: " + unknown)

    let fold (events: obj array) : obj =
        // DSL-MUTABLE: algorithm-scratch — fold accumulator
        let mutable projection = OrchestratorProjection.empty
        // DSL-MUTABLE: algorithm-scratch — first fold failure
        let mutable failure: string option = None

        for event in events do
            match failure with
            | Some _ -> ()
            | None ->
                match applyEvent projection event with
                | Ok next -> projection <- next
                | Error reason -> failure <- Some reason

        match failure with
        | Some reason -> box {| ok = false; error = reason |}
        | None ->
            box
                {| ok = true
                   value = ProjectionHandle projection |}

    let unwrapFold (result: obj) : obj =
        if stringField result [ "ok" ] = "false" then
            null
        else
            field result [ "value" ]

    [<RequireQualifiedAccess>]
    type private ProgramScenarioSignal =
        | QualityCandidate of snapshot: string
        | Retired
        | Exceptional of reason: string

    [<RequireQualifiedAccess>]
    type private ProgramScenarioRebase =
        | Ok of resultingHead: string
        | Error of reason: string

    [<RequireQualifiedAccess>]
    type private ProgramScenarioFf =
        | Ok of landedHead: string
        | TargetMoved

    type private ProgramScenario =
        { InitialHead: string
          InitialTarget: string
          InitialRebasedTarget: string option
          Signals: ProgramScenarioSignal list
          Snapshots: string list
          TargetReads: string list
          RebaseResults: ProgramScenarioRebase list
          ConflictReads: string list list
          FfResults: ProgramScenarioFf list }

    let private programScenario name =
        match name with
        | "fresh" ->
            { InitialHead = "candidate-1"
              InitialTarget = "target-1"
              InitialRebasedTarget = None
              Signals =
                [ ProgramScenarioSignal.QualityCandidate "snapshot-1"
                  ProgramScenarioSignal.QualityCandidate "snapshot-rebased-1" ]
              Snapshots = [ "snapshot-1"; "snapshot-rebased-1"; "snapshot-rebased-1" ]
              TargetReads = [ "target-1"; "target-1"; "target-1" ]
              RebaseResults = [ ProgramScenarioRebase.Ok "rebased-1" ]
              ConflictReads = [ []; [] ]
              FfResults = [ ProgramScenarioFf.Ok "rebased-1" ] }
        | "rebase-conflict" ->
            { InitialHead = "candidate-1"
              InitialTarget = "target-1"
              InitialRebasedTarget = None
              Signals =
                [ ProgramScenarioSignal.QualityCandidate "snapshot-1"
                  ProgramScenarioSignal.Exceptional "scenario-complete" ]
              Snapshots = [ "snapshot-1"; "snapshot-conflict" ]
              TargetReads = [ "target-1" ]
              RebaseResults = [ ProgramScenarioRebase.Error "rebase conflict" ]
              ConflictReads = [ []; [ "conflict.fs" ] ]
              FfResults = [] }
        | "target-moved" ->
            { InitialHead = "rebased-1"
              InitialTarget = "target-2"
              InitialRebasedTarget = Some "target-1"
              Signals =
                [ ProgramScenarioSignal.QualityCandidate "snapshot-rebased-1"
                  ProgramScenarioSignal.Exceptional "scenario-complete" ]
              Snapshots = [ "snapshot-rebased-1"; "snapshot-rebased-2" ]
              TargetReads = [ "target-2" ]
              RebaseResults = [ ProgramScenarioRebase.Ok "rebased-2" ]
              ConflictReads = [ [] ]
              FfResults = [] }
        | "cas-miss" ->
            { InitialHead = "rebased-1"
              InitialTarget = "target-1"
              InitialRebasedTarget = Some "target-1"
              Signals =
                [ ProgramScenarioSignal.QualityCandidate "snapshot-rebased-1"
                  ProgramScenarioSignal.Exceptional "scenario-complete" ]
              Snapshots = [ "snapshot-rebased-1"; "snapshot-rebased-2" ]
              TargetReads = [ "target-1"; "target-1"; "target-2" ]
              RebaseResults = [ ProgramScenarioRebase.Ok "rebased-2" ]
              ConflictReads = [ [] ]
              FfResults = [ ProgramScenarioFf.TargetMoved ] }
        | "stale-certificate" ->
            { InitialHead = "candidate-1"
              InitialTarget = "target-1"
              InitialRebasedTarget = None
              Signals =
                [ ProgramScenarioSignal.QualityCandidate "snapshot-certified"
                  ProgramScenarioSignal.Exceptional "scenario-complete" ]
              Snapshots = [ "snapshot-current" ]
              TargetReads = []
              RebaseResults = []
              ConflictReads = []
              FfResults = [] }
        | "artifact-conflict" ->
            { InitialHead = "candidate-1"
              InitialTarget = "target-1"
              InitialRebasedTarget = None
              Signals =
                [ ProgramScenarioSignal.QualityCandidate "snapshot-1"
                  ProgramScenarioSignal.Exceptional "scenario-complete" ]
              Snapshots = [ "snapshot-1" ]
              TargetReads = [ "target-1" ]
              RebaseResults = []
              ConflictReads = [ [ "conflict.fs" ] ]
              FfResults = [] }
        | "retired" ->
            { InitialHead = "candidate-1"
              InitialTarget = "target-1"
              InitialRebasedTarget = None
              Signals =
                [ ProgramScenarioSignal.Retired
                  ProgramScenarioSignal.Exceptional "scenario-complete" ]
              Snapshots = []
              TargetReads = []
              RebaseResults = []
              ConflictReads = []
              FfResults = [] }
        | unknown -> invalidArg "scenario" ("unknown Change program scenario: " + unknown)

    let private programFactName fact =
        match fact with
        | AgentFact.Orchestrator value ->
            match value with
            | OrchestratorFactCases.ManagerJobCreated _ -> "ManagerJobCreated"
            | OrchestratorFactCases.CandidateReady _ -> "CandidateReady"
            | OrchestratorFactCases.ConflictDetected _ -> "ConflictDetected"
            | OrchestratorFactCases.RebasedCandidateReady _ -> "RebasedCandidateReady"
            | OrchestratorFactCases.PublishClaimed _ -> "PublishClaimed"
            | OrchestratorFactCases.Published _ -> "Published"
            | OrchestratorFactCases.JobFailed _ -> "JobFailed"
            | OrchestratorFactCases.JobAbandoned _ -> "JobAbandoned"
            | OrchestratorFactCases.WorktreeCreateRequested _ -> "WorktreeCreateRequested"
            | OrchestratorFactCases.WorktreeCreated _ -> "WorktreeCreated"
        | _ -> "Other"

    let private scenarioBinding tag =
        { PhysicalUserMessageId = "physical-" + tag
          ProviderRunId = "provider-" + tag
          ToolCallId = "tool-" + tag
          NarrativeDigest = "narrative-" + tag
          PayloadDigest = "payload-" + tag
          RootRequestDigest = "root-" + tag
          RequirementSetDigest = "requirements-" + tag
          EvidenceFrontierDigest = "evidence-" + tag }

    let private scenarioCertificate tag snapshot =
        { Id = QualityCertificateId.create ("certificate-" + tag)
          AssessmentId = AssessmentId.create ("assessment-" + tag)
          IncumbencyId = IncumbencyId.create ("incumbency-" + tag)
          SnapshotId = WorkspaceSnapshotId.create snapshot
          AuthorityRevision = AuthorityRevision.create ("authority-" + tag)
          Binding = scenarioBinding tag
          Valid = true
          InvalidationReason = None }

    let private scenarioRetirement tag snapshot qualityAccepted =
        let incumbent = IncumbencyId.create ("incumbency-" + tag)
        let retirementId = RetirementId.create ("retirement-" + tag)

        { Id = retirementId
          IncumbencyId = incumbent
          SnapshotId = WorkspaceSnapshotId.create snapshot
          BatonId = BatonId.create ("baton-" + tag)
          Baton =
            { SchemaVersion = 1
              RoadId = "surface-road"
              FromIncumbencyId = IncumbencyId.value incumbent
              AuthorityRevision = "authority-" + tag
              SnapshotId = snapshot
              OpenObligations = []
              EvidenceRefs = [] }
          ProjectionCutId = ProjectionCutId.create ("cut-" + tag)
          ProjectionCut =
            { RetiredIncumbencyId = IncumbencyId.value incumbent
              ThroughProviderRunId = "provider-" + tag
              ThroughToolCallId = "tool-" + tag
              StaleProviderRunIds = [ "provider-" + tag ] }
          SuccessorRequested = not qualityAccepted
          QualityCandidateAccepted = qualityAccepted }

    let private scenarioSignal index signal =
        match signal with
        | ProgramScenarioSignal.QualityCandidate snapshot ->
            let tag = string index
            let certificate = scenarioCertificate tag snapshot
            RoadSignal.QualityCandidateAccepted(scenarioRetirement tag snapshot true, certificate)
        | ProgramScenarioSignal.Retired ->
            RoadSignal.IncumbencyRetired(scenarioRetirement (string index) "retired-snapshot" false)
        | ProgramScenarioSignal.Exceptional reason -> RoadSignal.ExceptionalTerminal reason

    let private verdictObject verdict =
        match verdict with
        | OrchestratorVerdict.Published(_, head) ->
            box
                {| kind = "Published"
                   detail = CommitHash.value head |}
        | OrchestratorVerdict.RejectedDirty reason ->
            box
                {| kind = "RejectedDirty"
                   detail = reason |}
        | OrchestratorVerdict.IntegrationFailed(_, detail) ->
            box
                {| kind = "IntegrationFailed"
                   detail = detail |}
        | OrchestratorVerdict.Empty -> box {| kind = "Empty"; detail = "" |}

    /// Executes the real OrchestratorProgram against deterministic in-memory
    /// ports. The surface exposes domain effects, not old review stages, so
    /// integration requirements can prove invalidation/successor/Git/CAS order.
    let observeRelayProgram (scenarioName: string) : Task<obj> =
        task {
            let scenario = programScenario scenarioName
            let jobId = ManagerJobId.create "surface-job"
            let sessionId = SessionId.create "surface-manager-session"
            let worktreePath = WorktreePath.create "/tmp/wanxiangshu-change-surface"
            let worktreeIdentity = WorktreeIdentity.create "manager/surface-job"
            let targetRef = TargetRef.create "refs/heads/main"
            let signals = Queue<ProgramScenarioSignal>(scenario.Signals)
            let snapshots = Queue<string>(scenario.Snapshots)
            let targetReads = Queue<string>(scenario.TargetReads)
            let rebaseResults = Queue<ProgramScenarioRebase>(scenario.RebaseResults)
            let conflictReads = Queue<string list>(scenario.ConflictReads)
            let ffResults = Queue<ProgramScenarioFf>(scenario.FfResults)
            let facts = ResizeArray<string>()
            let invalidations = ResizeArray<string>()
            let successors = ResizeArray<string>()
            let timeline = ResizeArray<string>()
            let rebaseGateHeld = ResizeArray<bool>()
            let ffGateHeld = ResizeArray<bool>()
            let ffExpectedHeads = ResizeArray<string>()
            let worktreeHead = ref scenario.InitialHead
            let targetHead = ref scenario.InitialTarget
            let gateHeld = ref false
            let gateAcquireCount = ref 0
            let gateReleaseCount = ref 0
            let signalIndex = ref 0

            let nextSignal () =
                if signals.Count = 0 then
                    Error "scenario signal queue exhausted"
                else
                    signalIndex.Value <- signalIndex.Value + 1
                    let value = scenarioSignal signalIndex.Value (signals.Dequeue())

                    let label =
                        match value with
                        | RoadSignal.QualityCandidateAccepted _ -> "QualityCandidateAccepted"
                        | RoadSignal.IncumbencyRetired _ -> "IncumbencyRetired"
                        | RoadSignal.ExceptionalTerminal _ -> "ExceptionalTerminal"

                    timeline.Add("await:" + label)
                    Ok value

            let git: GitPort =
                { IsDirty = fun _ -> Task.FromResult false
                  CreateWorktree = fun _ _ -> Task.FromResult(Ok worktreeIdentity)
                  FreezeTargetBranch = fun () -> Task.FromResult(Ok targetRef)
                  Rebase =
                    fun _ _ ->
                        task {
                            rebaseGateHeld.Add gateHeld.Value
                            timeline.Add "git:rebase"

                            if rebaseResults.Count = 0 then
                                return Error "scenario rebase queue exhausted"
                            else
                                match rebaseResults.Dequeue() with
                                | ProgramScenarioRebase.Ok head ->
                                    worktreeHead.Value <- head
                                    return Ok()
                                | ProgramScenarioRebase.Error reason -> return Error reason
                        }
                  FfMerge =
                    fun _ _ expected ->
                        task {
                            ffGateHeld.Add gateHeld.Value
                            ffExpectedHeads.Add(CommitHash.value expected)
                            timeline.Add "git:ff"

                            if ffResults.Count = 0 then
                                return Error "scenario ff queue exhausted"
                            else
                                match ffResults.Dequeue() with
                                | ProgramScenarioFf.Ok head ->
                                    targetHead.Value <- head
                                    return Ok(CommitHash.create head)
                                | ProgramScenarioFf.TargetMoved ->
                                    return Error OrchestratorConstants.targetRefMovedError
                        }
                  ConflictedFiles =
                    fun _ ->
                        if conflictReads.Count = 0 then
                            Task.FromResult(Error "scenario conflict queue exhausted")
                        else
                            Task.FromResult(Ok(conflictReads.Dequeue()))
                  RemoveWorktree = fun _ -> Task.FromResult(Ok())
                  HasRebaseHead = fun _ -> Task.FromResult false
                  ListWorktrees = fun () -> Task.FromResult(Ok [])
                  ListManagerBranches = fun () -> Task.FromResult(Ok [])
                  DeleteBranch = fun _ -> Task.FromResult(Ok())
                  ReadHead = fun _ -> Task.FromResult(Ok(CommitHash.create worktreeHead.Value))
                  GetTargetHead =
                    fun _ ->
                        let value =
                            if targetReads.Count = 0 then
                                targetHead.Value
                            else
                                targetReads.Dequeue()

                        targetHead.Value <- value
                        Task.FromResult(Ok(CommitHash.create value)) }

            let worktree = WorktreeResource.Adopt(git, worktreeIdentity, worktreePath)

            let job =
                { JobId = jobId
                  ManagerSessionId = sessionId
                  ManagerAgent = "manager"
                  TargetRef = targetRef
                  Worktree = worktree }

            let created =
                OrchestratorProjection.createJob
                    {| ManagerJobId = jobId
                       ManagerSessionId = sessionId
                       ManagerAgent = "manager"
                       Byname = "surface-road"
                       WorktreeIdentity = worktreeIdentity
                       WorktreePath = worktreePath
                       TargetRef = targetRef
                       TargetBranchFrozen = TargetRef.value targetRef |}
                    Wanxiangshu.Composition.Durable.Fold.empty.AgentProjections.Orchestrator

            let initialOrchestrator =
                match scenario.InitialRebasedTarget with
                | None -> created
                | Some targetSnapshot ->
                    OrchestratorProjection.recordRebasedCandidateReady
                        jobId
                        {| RebasedCommit = CommitHash.create scenario.InitialHead
                           TargetHeadSnapshot = CommitHash.create targetSnapshot
                           WorkspaceSnapshotId = WorkspaceSnapshotId.create "snapshot-rebased-1" |}
                        created

            let initialProjection = Wanxiangshu.Composition.Durable.Fold.empty

            let projection =
                ref
                    { initialProjection with
                        AgentProjections =
                            { initialProjection.AgentProjections with
                                Orchestrator = initialOrchestrator } }

            let appendFact _ fact =
                task {
                    let name = programFactName fact
                    facts.Add name
                    timeline.Add("fact:" + name)

                    match Wanxiangshu.Composition.Durable.Fold.foldAgentFact projection.Value.AgentProjections fact with
                    | Error rejection -> return Error(sprintf "%A" rejection)
                    | Ok agents ->
                        projection.Value <-
                            { projection.Value with
                                AgentProjections = agents }

                        return Ok()
                }

            let relay: RelayPort =
                { OpenRoad = fun _ -> Task.FromResult(Ok sessionId)
                  ActivateRoad = fun _ -> Task.FromResult(Ok())
                  AwaitRoadSignal = fun _ -> Task.FromResult(nextSignal ())
                  InvalidateCertificate =
                    fun _ reason ->
                        invalidations.Add reason
                        timeline.Add("invalidate:" + reason)
                        Task.FromResult(Ok())
                  RequestSuccessor =
                    fun _ _ reason ->
                        successors.Add reason
                        timeline.Add("successor:" + reason)
                        Task.FromResult(Ok(IncumbencyId.create ("surface-successor-" + string successors.Count)))
                  CaptureSnapshot =
                    fun _ ->
                        if snapshots.Count = 0 then
                            Task.FromResult(Error "scenario snapshot queue exhausted")
                        else
                            Task.FromResult(Ok(WorkspaceSnapshotId.create (snapshots.Dequeue())))
                  PrepareCandidate =
                    fun _ ->
                        if conflictReads.Count > 0 then
                            if conflictReads.Peek() <> [] then
                                Task.FromResult(Error "unmerged paths remain in worktree")
                            else
                                conflictReads.Dequeue() |> ignore
                                Task.FromResult(Ok(CommitHash.create worktreeHead.Value))
                        else
                            Task.FromResult(Ok(CommitHash.create worktreeHead.Value))
                  TerminateRoadResources =
                    fun _ ->
                        timeline.Add "relay:terminate"
                        Task.FromResult() }

            let acquireGate () =
                gateAcquireCount.Value <- gateAcquireCount.Value + 1
                gateHeld.Value <- true
                timeline.Add "gate:acquire"

                Task.FromResult
                    { Release =
                        fun () ->
                            task {
                                gateHeld.Value <- false
                                gateReleaseCount.Value <- gateReleaseCount.Value + 1
                                timeline.Add "gate:release"
                            } }

            let deps =
                { Git = git
                  Relay = relay
                  AppendFact = appendFact
                  Snapshot = fun () -> projection.Value
                  AcquirePublishGate = acquireGate }

            let! verdict = OrchestratorProgram.run deps job

            return
                box
                    {| verdict = verdictObject verdict
                       facts = facts.ToArray()
                       invalidations = invalidations.ToArray()
                       successors = successors.ToArray()
                       timeline = timeline.ToArray()
                       rebaseGateHeld = rebaseGateHeld.ToArray()
                       ffGateHeld = ffGateHeld.ToArray()
                       ffExpectedHeads = ffExpectedHeads.ToArray()
                       gateAcquireCount = gateAcquireCount.Value
                       gateReleaseCount = gateReleaseCount.Value
                       gateHeldAfterRun = gateHeld.Value |}
        }

    let private commandObject (command: Command) : obj =
        box
            {| fileName = command.FileName
               args = command.Arguments |> List.toArray
               workingDirectory = optionObj command.WorkingDirectory |}

    let private invokeRunner (runner: obj) (command: Command) : Task<int * string * string> =
        task {
            let! raw = unbox<Task<obj>> (asPromise (apply1 runner (commandObject command)))
            let values = unbox<obj array> raw
            return int (string values.[0]), stringOf values.[1], stringOf values.[2]
        }

    let createGit (repo: string) (runner: obj) : obj =
        let port = GitOperations.createWithRepo repo (invokeRunner runner)
        GitHandle port :> obj

    let private resultObject (result: Result<'T, string>) (valueOf: 'T -> obj) : obj =
        match result with
        | Ok value -> box {| ok = true; value = valueOf value |}
        | Error error -> box {| ok = false; error = error |}

    let gitIsDirty (git: obj) (path: string) : Task<bool> =
        (git :?> GitHandle).Port.IsDirty(WorktreePath.create path)

    let gitFreezeTargetBranch (git: obj) : Task<obj> =
        task {
            let! result = (git :?> GitHandle).Port.FreezeTargetBranch()
            return resultObject result (fun value -> box (TargetRef.value value))
        }

    let gitRebase (git: obj) (path: string) (targetRef: string) : Task<obj> =
        task {
            let! result = (git :?> GitHandle).Port.Rebase (WorktreePath.create path) (TargetRef.create targetRef)
            return resultObject result (fun _ -> null)
        }

    let gitFfMerge (git: obj) (path: string) (targetRef: string) (expectedHead: string) : Task<obj> =
        task {
            let! result =
                (git :?> GitHandle).Port.FfMerge
                    (WorktreePath.create path)
                    (TargetRef.create targetRef)
                    (CommitHash.create expectedHead)

            return resultObject result (fun value -> box (CommitHash.value value))
        }

    let gitConflictedFiles (git: obj) (path: string) : Task<obj> =
        task {
            let! result = (git :?> GitHandle).Port.ConflictedFiles(WorktreePath.create path)
            return resultObject result (fun values -> values |> List.toArray |> Array.map box |> box)
        }

    let gitHasRebaseHead (git: obj) (path: string) : Task<bool> =
        (git :?> GitHandle).Port.HasRebaseHead(WorktreePath.create path)

    let gitReadHead (git: obj) (path: string) : Task<obj> =
        task {
            let! result = (git :?> GitHandle).Port.ReadHead(WorktreePath.create path)
            return resultObject result (fun value -> box (CommitHash.value value))
        }

    let gitGetTargetHead (git: obj) (targetRef: string) : Task<obj> =
        task {
            let! result = (git :?> GitHandle).Port.GetTargetHead(TargetRef.create targetRef)
            return resultObject result (fun value -> box (CommitHash.value value))
        }

    let gitCreateWorktree (git: obj) (job: string) (path: string) : Task<obj> =
        task {
            let! result = (git :?> GitHandle).Port.CreateWorktree (ManagerJobId.create job) (WorktreePath.create path)
            return resultObject result (fun value -> box (WorktreeIdentity.value value))
        }

    let gitRemoveWorktree (git: obj) (path: string) : Task<obj> =
        task {
            let! result = (git :?> GitHandle).Port.RemoveWorktree(WorktreePath.create path)
            return resultObject result (fun _ -> null)
        }

    let gitDeleteBranch (git: obj) (identity: string) : Task<obj> =
        task {
            let! result = (git :?> GitHandle).Port.DeleteBranch(WorktreeIdentity.create identity)
            return resultObject result (fun _ -> null)
        }

    let worktreeIdentityOf (job: string) : string =
        WorktreeIdentity.value (WorktreeCommands.identityOf (ManagerJobId.create job))

    let gitListWorktrees (git: obj) : Task<obj> =
        task {
            let! result = (git :?> GitHandle).Port.ListWorktrees()

            return
                resultObject result (fun values ->
                    values
                    |> List.map (fun (path, identity) ->
                        box
                            {| path = WorktreePath.value path
                               identity =
                                match identity with
                                | Some id -> box (WorktreeIdentity.value id)
                                | None -> null |})
                    |> List.toArray
                    |> box)
        }

    let gitListManagerBranches (git: obj) : Task<obj> =
        task {
            let! result = (git :?> GitHandle).Port.ListManagerBranches()
            return resultObject result (fun values -> values |> List.map WorktreeIdentity.value |> List.toArray |> box)
        }

    let worktreeCreate (git: obj) (job: string) (path: string) : Task<obj> =
        task {
            let! result =
                WorktreeResource.Create((git :?> GitHandle).Port, ManagerJobId.create job, WorktreePath.create path)

            return resultObject result (fun resource -> WorktreeHandle resource :> obj)
        }

    let worktreeAdopt (git: obj) (identity: string) (path: string) : obj =
        WorktreeHandle(
            WorktreeResource.Adopt((git :?> GitHandle).Port, WorktreeIdentity.create identity, WorktreePath.create path)
        )
        :> obj

    let worktreePath (resource: obj) : string =
        WorktreePath.value ((resource :?> WorktreeHandle).Resource.Path)

    let worktreeIdentity (resource: obj) : string =
        WorktreeIdentity.value ((resource :?> WorktreeHandle).Resource.Identity)

    let worktreeMarkDurable (resource: obj) : unit =
        (resource :?> WorktreeHandle).Resource.MarkDurable()

    let worktreeRelease (resource: obj) : Task<obj> =
        task {
            let! result = (resource :?> WorktreeHandle).Resource.Release()
            return resultObject result (fun _ -> null)
        }

    let worktreeDispose (resource: obj) : Task<unit> =
        task { do! ((resource :?> WorktreeHandle).Resource :> IAsyncDisposable).DisposeAsync() }

    let lockPath (repo: string) (branch: string) : string = IntegrationGate.lockPath repo branch

    let acquireGate (path: string) : Task<obj> =
        task {
            let! gate = IntegrationGate.acquire path
            return GateHandle gate :> obj
        }

    let releaseGate (gate: obj) : Task<unit> = (gate :?> GateHandle).Gate.Release()

    let disposeGate (gate: obj) : Task<unit> =
        task { do! ((gate :?> GateHandle).Gate :> IAsyncDisposable).DisposeAsync() }
