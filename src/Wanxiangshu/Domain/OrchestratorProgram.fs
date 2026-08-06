namespace Wanxiangshu.Domain

open System
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// 命令式封闭 AST（FLOW-002）。
/// Program 本身不执行、不持有 Runtime、不读取时钟、不追加 Journal、
/// 不捕获 Host 对象、不作为恢复中的暂停协程。
type OrchestratorCommand =
    | AwaitManager of ManagerJobId
    | ResumeManager of ManagerJobId * WorktreePath * prompt: string
    | ReadTargetHead of TargetRef
    | ReadWorktreeHead of WorktreePath
    | RebaseOnto of WorktreePath * TargetRef
    | ReviewRound of ManagerJobId * SessionId * WorktreePath * ReviewBarrierId * phase: string * round: int
    | RecordCandidateReady of ManagerJobId * CommitHash * ReviewBarrierId
    | RecordRebasedReady of ManagerJobId * rebased: CommitHash * target: CommitHash * ReviewBarrierId
    | RecordConflict of ManagerJobId * candidate: CommitHash * target: CommitHash * files: string list
    | PublishUnderGate of ManagerJobId * expectedHead: CommitHash
    | TerminateChildren of ManagerJobId
    | ReleaseWorktree of WorktreePath
    | AppendFact of AgentFact

/// Interpreter → Program 的回复。业务分支留在 Program 数据里（FLOW-003）。
type OrchestratorReply =
    | UnitOk
    | Head of CommitHash
    | RebaseOk
    | RebaseConflict of files: string list * worktreeHead: CommitHash
    | ReviewOk
    | PublishLanded of CommitHash
    | PublishTargetMoved
    | PublishFailed of string
    | Failed of string

[<AutoOpen>]
module OrchestratorProgramTypes =

    /// DSL 程序是待解释的数据，不是正在执行的协程。
    type OrchestratorProgram =
        | Return of Result<CommitHash, string> option
        | Step of OrchestratorCommand * (OrchestratorReply -> OrchestratorProgram)

    let rec bindProgram
        (program: OrchestratorProgram)
        (cont: Result<CommitHash, string> option -> OrchestratorProgram)
        : OrchestratorProgram =
        match program with
        | Return value -> cont value
        | Step(cmd, next) -> Step(cmd, (fun reply -> bindProgram (next reply) cont))

    /// Computation expression builder for FLOW-002 DSL.
    /// Does not run anything; it merely assembles a data structure.
    type OrchestratorBuilder() =
        member _.Return(value: Result<CommitHash, string> option) : OrchestratorProgram = Return value

        member _.ReturnFrom(program: OrchestratorProgram) : OrchestratorProgram = program

        member _.Zero() : OrchestratorProgram = Return None

        member _.Bind
            (program: OrchestratorProgram, cont: Result<CommitHash, string> option -> OrchestratorProgram)
            : OrchestratorProgram =
            bindProgram program cont

        member _.Bind
            (command: OrchestratorCommand, cont: OrchestratorReply -> OrchestratorProgram)
            : OrchestratorProgram =
            Step(command, cont)

        member _.Delay(f: unit -> OrchestratorProgram) : OrchestratorProgram = f ()

        member _.Combine(left: OrchestratorProgram, right: OrchestratorProgram) : OrchestratorProgram =
            bindProgram left (fun _ -> right)

    [<RequireQualifiedAccess>]
    module OrchestratorProgram =
        let empty: OrchestratorProgram = Return None

    let orchestrator = OrchestratorBuilder()

/// Pure production control-flow shapes (FLOW-002). Zero I/O.
module OrchestratorPrograms =

    let private barrierId (jobId: ManagerJobId) (phase: string) (round: int) =
        ReviewBarrierId.create (sprintf "%s:%s:%d" (ManagerJobId.value jobId) phase round)

    let private conflictResumePrompt (files: string list) : string =
        let names =
            if List.isEmpty files then
                "<unable to enumerate conflicted files>"
            else
                String.concat "\n  " files

        sprintf
            "[CONFLICT RESUMPTION] An in-progress rebase hit conflicts. Conflicted files:\n  %s\nYou are RESUMING an in-progress rebase in this same session — do NOT restart the original task. Resolve the conflicts, then continue and finish the rebase."
            names

    let private unexpected (context: string) (reply: OrchestratorReply) : OrchestratorProgram =
        let detail =
            match reply with
            | Failed reason -> reason
            | PublishFailed reason -> reason
            | _ -> context

        Return(Some(Error detail))

    let rec private afterRebaseOk
        (jobId: ManagerJobId)
        (sessionId: SessionId)
        (worktree: WorktreePath)
        (targetRef: TargetRef)
        (targetHead: CommitHash)
        (round: int)
        : OrchestratorProgram =
        Step(
            ReviewRound(jobId, sessionId, worktree, barrierId jobId "post-rebase" round, "post-rebase", round),
            function
            | ReviewOk ->
                Step(
                    ReadWorktreeHead worktree,
                    function
                    | Head rebased ->
                        Step(
                            RecordRebasedReady(jobId, rebased, targetHead, barrierId jobId "post-rebase" round),
                            function
                            | UnitOk ->
                                Step(
                                    PublishUnderGate(jobId, targetHead),
                                    function
                                    | PublishLanded landed ->
                                        Step(
                                            ReleaseWorktree worktree,
                                            function
                                            | UnitOk -> Return(Some(Ok landed))
                                            | Failed reason ->
                                                Return(
                                                    Some(
                                                        Error(
                                                            sprintf
                                                                "Published %s but cleanup failed: %s"
                                                                (CommitHash.value landed)
                                                                reason
                                                        )
                                                    )
                                                )
                                            | other -> unexpected "release after publish" other
                                        )
                                    | PublishTargetMoved ->
                                        rebaseReviewPublish jobId sessionId worktree targetRef (round + 1)
                                    | PublishFailed reason -> Return(Some(Error reason))
                                    | Failed reason -> Return(Some(Error reason))
                                    | other -> unexpected "publish" other
                                )
                            | Failed reason -> Return(Some(Error reason))
                            | other -> unexpected "record rebased ready" other
                        )
                    | Failed reason -> Return(Some(Error reason))
                    | other -> unexpected "read worktree head after rebase" other
                )
            | Failed reason -> Return(Some(Error reason))
            | other -> unexpected "post-rebase review" other
        )

    and private rebaseOntoPath
        (jobId: ManagerJobId)
        (sessionId: SessionId)
        (worktree: WorktreePath)
        (targetRef: TargetRef)
        (targetHead: CommitHash)
        (round: int)
        : OrchestratorProgram =
        Step(
            RebaseOnto(worktree, targetRef),
            function
            | RebaseOk -> afterRebaseOk jobId sessionId worktree targetRef targetHead round
            | RebaseConflict(files, worktreeHead) ->
                Step(
                    RecordConflict(jobId, worktreeHead, targetHead, files),
                    function
                    | UnitOk ->
                        Step(
                            ResumeManager(jobId, worktree, conflictResumePrompt files),
                            function
                            | UnitOk ->
                                Step(
                                    RebaseOnto(worktree, targetRef),
                                    function
                                    | RebaseOk -> afterRebaseOk jobId sessionId worktree targetRef targetHead round
                                    | Failed reason ->
                                        Return(Some(Error(sprintf "Rebase continuation failed: %s" reason)))
                                    | RebaseConflict _ ->
                                        Return(Some(Error "Rebase continuation failed: still conflicted"))
                                    | other -> unexpected "rebase after conflict resume" other
                                )
                            | Failed reason ->
                                Return(Some(Error(sprintf "Rebase conflict; manager continuation failed: %s" reason)))
                            | other -> unexpected "resume manager after conflict" other
                        )
                    | Failed reason -> Return(Some(Error reason))
                    | other -> unexpected "record conflict" other
                )
            | Failed reason -> Return(Some(Error reason))
            | other -> unexpected "rebase" other
        )

    and rebaseReviewPublish
        (jobId: ManagerJobId)
        (sessionId: SessionId)
        (worktree: WorktreePath)
        (targetRef: TargetRef)
        (round: int)
        : OrchestratorProgram =
        Step(
            ReadTargetHead targetRef,
            function
            | Head targetHead -> rebaseOntoPath jobId sessionId worktree targetRef targetHead round
            | Failed reason -> Return(Some(Error(sprintf "Git target head lookup failed: %s" reason)))
            | other -> unexpected "read target head" other
        )

    let private afterManager
        (jobId: ManagerJobId)
        (sessionId: SessionId)
        (worktree: WorktreePath)
        (targetRef: TargetRef)
        : OrchestratorProgram =
        Step(
            ReviewRound(jobId, sessionId, worktree, barrierId jobId "pre-rebase" 0, "pre-rebase", 0),
            function
            | ReviewOk ->
                Step(
                    ReadWorktreeHead worktree,
                    function
                    | Head candidate ->
                        Step(
                            RecordCandidateReady(jobId, candidate, barrierId jobId "pre-rebase" 0),
                            function
                            | UnitOk -> rebaseReviewPublish jobId sessionId worktree targetRef 0
                            | Failed reason -> Return(Some(Error reason))
                            | other -> unexpected "record candidate ready" other
                        )
                    | Failed reason -> Return(Some(Error reason))
                    | other -> unexpected "read worktree head pre-rebase" other
                )
            | Failed reason -> Return(Some(Error reason))
            | other -> unexpected "pre-rebase review" other
        )

    let freshStart
        (jobId: ManagerJobId)
        (sessionId: SessionId)
        (worktree: WorktreePath)
        (targetRef: TargetRef)
        : OrchestratorProgram =
        Step(
            AwaitManager jobId,
            function
            | UnitOk -> afterManager jobId sessionId worktree targetRef
            | Failed reason -> Return(Some(Error(sprintf "Manager run failed: %s" reason)))
            | other -> unexpected "await manager" other
        )

    let resumeBackfillPublished
        (jobId: ManagerJobId)
        (worktree: WorktreePath)
        (rebased: CommitHash)
        (resultingHead: CommitHash)
        : OrchestratorProgram =
        let fact =
            AgentFact.Published
                {| ManagerJobId = jobId
                   CandidateCommit = rebased
                   ResultingTargetHead = resultingHead |}

        Step(
            AppendFact fact,
            function
            | UnitOk ->
                Step(
                    TerminateChildren jobId,
                    function
                    | UnitOk ->
                        Step(
                            ReleaseWorktree worktree,
                            function
                            | UnitOk -> Return(Some(Ok resultingHead))
                            | Failed reason ->
                                Return(Some(Error(sprintf "Backfilled Published but cleanup failed: %s" reason)))
                            | other -> unexpected "release after backfill" other
                        )
                    | Failed reason -> Return(Some(Error reason))
                    | other -> unexpected "terminate after backfill" other
                )
            | Failed reason -> Return(Some(Error reason))
            | other -> unexpected "append published" other
        )

    let resumeFailClosed (reason: string) : OrchestratorProgram = Return(Some(Error reason))

    let resumeCleanUp (worktree: WorktreePath) : OrchestratorProgram =
        Step(
            ReleaseWorktree worktree,
            function
            | UnitOk -> Return None
            | Failed reason -> Return(Some(Error(sprintf "Terminal job cleanup failed: %s" reason)))
            | other -> unexpected "cleanup release" other
        )

    let resumeAttemptPublish
        (jobId: ManagerJobId)
        (sessionId: SessionId)
        (worktree: WorktreePath)
        (targetRef: TargetRef)
        (expectedHead: CommitHash)
        : OrchestratorProgram =
        Step(
            PublishUnderGate(jobId, expectedHead),
            function
            | PublishLanded landed ->
                Step(
                    ReleaseWorktree worktree,
                    function
                    | UnitOk -> Return(Some(Ok landed))
                    | Failed reason ->
                        Return(
                            Some(Error(sprintf "Published %s but cleanup failed: %s" (CommitHash.value landed) reason))
                        )
                    | other -> unexpected "release after attempt publish" other
                )
            | PublishTargetMoved -> rebaseReviewPublish jobId sessionId worktree targetRef 0
            | PublishFailed reason -> Return(Some(Error reason))
            | Failed reason -> Return(Some(Error reason))
            | other -> unexpected "attempt publish" other
        )

    let resumeConflictResolution
        (jobId: ManagerJobId)
        (sessionId: SessionId)
        (worktree: WorktreePath)
        (targetRef: TargetRef)
        (files: string list)
        : OrchestratorProgram =
        Step(
            ResumeManager(jobId, worktree, conflictResumePrompt files),
            function
            | UnitOk -> rebaseReviewPublish jobId sessionId worktree targetRef 0
            | Failed reason -> Return(Some(Error(sprintf "Conflict resolution failed: %s" reason)))
            | other -> unexpected "conflict resume manager" other
        )

    let resumeRebaseReviewPublish
        (jobId: ManagerJobId)
        (sessionId: SessionId)
        (worktree: WorktreePath)
        (targetRef: TargetRef)
        : OrchestratorProgram =
        rebaseReviewPublish jobId sessionId worktree targetRef 0

/// Trace Interpreter：将程序解释为可观察轨迹（FLOW-003 / 9.4）。
/// 不执行副作用，只输出领域操作名称与顺序。
module TraceInterpreter =

    let commandName (command: OrchestratorCommand) : string =
        match command with
        | AwaitManager _ -> "AwaitManager"
        | ResumeManager _ -> "ResumeManager"
        | ReadTargetHead _ -> "ReadTargetHead"
        | ReadWorktreeHead _ -> "ReadWorktreeHead"
        | RebaseOnto _ -> "RebaseOnto"
        | ReviewRound _ -> "ReviewRound"
        | RecordCandidateReady _ -> "RecordCandidateReady"
        | RecordRebasedReady _ -> "RecordRebasedReady"
        | RecordConflict _ -> "RecordConflict"
        | PublishUnderGate _ -> "PublishUnderGate"
        | TerminateChildren _ -> "TerminateChildren"
        | ReleaseWorktree _ -> "ReleaseWorktree"
        | AppendFact _ -> "AppendFact"

    let private describeCommand (command: OrchestratorCommand) : string =
        match command with
        | AwaitManager id -> $"AwaitManager({ManagerJobId.value id})"
        | ResumeManager(id, path, _) -> $"ResumeManager({ManagerJobId.value id}, {WorktreePath.value path})"
        | ReadTargetHead ref -> $"ReadTargetHead({TargetRef.value ref})"
        | ReadWorktreeHead path -> $"ReadWorktreeHead({WorktreePath.value path})"
        | RebaseOnto(path, ref) -> $"RebaseOnto({WorktreePath.value path}, {TargetRef.value ref})"
        | ReviewRound(jobId, _, _, _, phase, round) -> $"ReviewRound({ManagerJobId.value jobId}, {phase}, {round})"
        | RecordCandidateReady(jobId, commit, _) ->
            $"RecordCandidateReady({ManagerJobId.value jobId}, {CommitHash.value commit})"
        | RecordRebasedReady(jobId, rebased, target, _) ->
            $"RecordRebasedReady({ManagerJobId.value jobId}, {CommitHash.value rebased}, {CommitHash.value target})"
        | RecordConflict(jobId, candidate, target, files) ->
            let filesText = String.concat ";" files

            $"RecordConflict({ManagerJobId.value jobId}, {CommitHash.value candidate}, {CommitHash.value target}, [{filesText}])"
        | PublishUnderGate(jobId, head) -> $"PublishUnderGate({ManagerJobId.value jobId}, {CommitHash.value head})"
        | TerminateChildren id -> $"TerminateChildren({ManagerJobId.value id})"
        | ReleaseWorktree path -> $"ReleaseWorktree({WorktreePath.value path})"
        | AppendFact _ -> "AppendFact"

    let defaultReply (command: OrchestratorCommand) : OrchestratorReply =
        match command with
        | ReadTargetHead _
        | ReadWorktreeHead _ -> Head(CommitHash.create "trace-head")
        | RebaseOnto _ -> RebaseOk
        | ReviewRound _ -> ReviewOk
        | PublishUnderGate _ -> PublishLanded(CommitHash.create "landed-head")
        | AwaitManager _
        | ResumeManager _
        | RecordCandidateReady _
        | RecordRebasedReady _
        | RecordConflict _
        | TerminateChildren _
        | ReleaseWorktree _
        | AppendFact _ -> UnitOk

    let rec interpretWith
        (replyOf: OrchestratorCommand -> OrchestratorReply)
        (program: OrchestratorProgram)
        : string list =
        match program with
        | Return value ->
            match value with
            | None -> [ "Return(None)" ]
            | Some(Ok head) -> [ $"Return(Ok {CommitHash.value head})" ]
            | Some(Error reason) -> [ $"Return(Error {reason})" ]
        | Step(cmd, next) -> describeCommand cmd :: interpretWith replyOf (next (replyOf cmd))

    let interpret (program: OrchestratorProgram) : string list = interpretWith defaultReply program
