namespace Wanxiangshu.Change

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process
open Wanxiangshu.Git

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
    let private barrier value = ReviewBarrierId.create (stringOf value)

    let private worktreeIdentityValue value =
        WorktreeIdentity.create (stringOf value)

    let private worktreePathValue value = WorktreePath.create (stringOf value)
    let private target value = TargetRef.create (stringOf value)

    let private progressOf (value: obj) : JobProgress =
        let kind = stringField value [ "kind"; "case"; "name" ]
        let payload = field value [ "payload"; "value"; "data" ]

        match kind with
        | "ManagerStarted" -> JobProgress.ManagerStarted
        | "CandidateReady" ->
            JobProgress.CandidateReady
                {| CandidateCommit = commit (field payload [ "candidateCommit"; "CandidateCommit" ])
                   PreRebaseReviewBarrierId =
                    barrier (field payload [ "preRebaseReviewBarrierId"; "PreRebaseReviewBarrierId" ]) |}
        | "ConflictPending"
        | "ConflictDetected" ->
            JobProgress.ConflictPending
                {| CandidateCommit = commit (field payload [ "candidateCommit"; "CandidateCommit" ])
                   TargetHeadSnapshot = commit (field payload [ "targetHeadSnapshot"; "TargetHeadSnapshot" ])
                   ConflictFiles = stringArray (field payload [ "conflictFiles"; "ConflictFiles" ]) |> Array.toList
                   DiagnosticsDigest = stringField payload [ "diagnosticsDigest"; "DiagnosticsDigest" ] |}
        | "RebasedCandidateReady" ->
            JobProgress.RebasedCandidateReady
                {| RebasedCommit = commit (field payload [ "rebasedCommit"; "RebasedCommit" ])
                   TargetHeadSnapshot = commit (field payload [ "targetHeadSnapshot"; "TargetHeadSnapshot" ])
                   PostRebaseReviewBarrierId =
                    barrier (field payload [ "postRebaseReviewBarrierId"; "PostRebaseReviewBarrierId" ]) |}
        | "PublishClaimed" ->
            JobProgress.PublishClaimed
                {| RebasedCommit = commit (field payload [ "rebasedCommit"; "RebasedCommit" ])
                   ExpectedHead = commit (field payload [ "expectedHead"; "ExpectedHead" ]) |}
        | "Published" ->
            JobProgress.Published
                {| CandidateCommit = commit (field payload [ "candidateCommit"; "CandidateCommit" ])
                   ResultingTargetHead = commit (field payload [ "resultingTargetHead"; "ResultingTargetHead" ]) |}
        | "Failed"
        | "JobFailed" -> JobProgress.Failed(stringOf payload)
        | "Abandoned"
        | "JobAbandoned" -> JobProgress.Abandoned
        | unknown -> invalidArg "progress" ("unknown job progress: " + unknown)

    let progress (kind: string) (payload: obj) : obj =
        box {| kind = kind; payload = payload |}

    let private jobObject (job: ManagerJobProjection) : obj =
        let progressName =
            match job.Progress with
            | JobProgress.ManagerStarted -> "ManagerStarted"
            | JobProgress.CandidateReady _ -> "CandidateReady"
            | JobProgress.ConflictPending _ -> "ConflictPending"
            | JobProgress.RebasedCandidateReady _ -> "RebasedCandidateReady"
            | JobProgress.PublishClaimed _ -> "PublishClaimed"
            | JobProgress.Published _ -> "Published"
            | JobProgress.Failed _ -> "Failed"
            | JobProgress.Abandoned -> "Abandoned"

        box
            {| jobId = ManagerJobId.value job.ManagerJobId
               managerSessionId = SessionId.value job.ManagerSessionId
               managerAgent = job.ManagerAgent
               byname = job.Byname
               worktreeIdentity = WorktreeIdentity.value job.WorktreeIdentity
               worktreePath = WorktreePath.value job.WorktreePath
               targetRef = TargetRef.value job.TargetRef
               targetBranchFrozen = job.TargetBranchFrozen
               progress = progressName |}

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

    let recordProgress (projection: obj) (job: string) (value: obj) : obj =
        let current = (projection :?> ProjectionHandle).Projection
        ProjectionHandle(OrchestratorProjection.recordProgress (jobId job) (progressOf value) current) :> obj

    let find (projection: obj) (job: string) : obj =
        let current = (projection :?> ProjectionHandle).Projection

        match OrchestratorProjection.tryFind (jobId job) current with
        | Some value -> jobObject value
        | None -> null

    let activeJobs (projection: obj) : obj array =
        let current = (projection :?> ProjectionHandle).Projection
        OrchestratorProjection.activeJobs current |> List.map jobObject |> List.toArray

    let progressName (job: obj) : string =
        stringField job [ "progress"; "Progress" ]

    /// ORCH-007 domain classification for a rebased candidate. Returns a
    /// physical-world classification, not a program counter.
    let classifyRebasedCandidate (head: obj) (rebasedCommit: string) (targetHeadSnapshot: string) : obj =
        let currentHead = if isNullish head then None else Some(commit head)
        let reality = OrchestratorProjection.classifyRebasedCandidate currentHead (commit rebasedCommit) (commit targetHeadSnapshot)

        match reality with
        | RebasedCandidateReality.HeadUnreadable -> box {| kind = "HeadUnreadable" |}
        | RebasedCandidateReality.PublishReady -> box {| kind = "PublishReady" |}
        | RebasedCandidateReality.NeedsRebase -> box {| kind = "NeedsRebase" |}

    /// ORCH-007 domain classification for a publish claim. Three branches in
    /// fixed order: already-published first, then unchanged target, then
    /// everything else.
    let classifyPublishClaim (head: obj) (rebasedCommit: string) (expectedHead: string) : obj =
        let currentHead = if isNullish head then None else Some(commit head)
        let reality = OrchestratorProjection.classifyPublishClaim currentHead (commit rebasedCommit) (commit expectedHead)

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

    let private applyEvent (projection: OrchestratorProjection) (event: obj) : Result<OrchestratorProjection, string> =
        let kind = stringField event [ "kind"; "case"; "type" ]
        let payload = field event [ "payload"; "value"; "data" ]

        match kind with
        | "ManagerJobCreated" -> Ok(OrchestratorProjection.createJob (createPayload payload) projection)
        | "CandidateReady"
        | "ConflictDetected"
        | "RebasedCandidateReady"
        | "PublishClaimed"
        | "Published"
        | "JobFailed"
        | "JobAbandoned" ->
            let job = stringField payload [ "jobId"; "ManagerJobId" ]

            let value =
                match kind with
                | "JobFailed" -> progress "Failed" (box (stringField payload [ "reason"; "Reason" ]))
                | "JobAbandoned" -> progress "Abandoned" null
                | _ -> progress kind payload

            if kind = "PublishClaimed" then
                match OrchestratorProjection.tryFind (jobId job) projection with
                | Some current ->
                    match current.Progress with
                    | JobProgress.RebasedCandidateReady rebased ->
                        let expected = commit (field payload [ "expectedHead"; "ExpectedHead" ])

                        Ok(
                            OrchestratorProjection.recordProgress
                                (jobId job)
                                (JobProgress.PublishClaimed
                                    {| RebasedCommit = rebased.RebasedCommit
                                       ExpectedHead = expected |})
                                projection
                        )
                    | _ -> Error "publish claimed for a job with no rebased candidate (ORCH-004)"
                | None -> Ok projection
            else
                Ok(OrchestratorProjection.recordProgress (jobId job) (progressOf value) projection)
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
