namespace Wanxiangshu.Domain

open System
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// 命令式封闭 AST（FLOW-002）。
/// Program 本身不执行、不持有 Runtime、不读取时钟、不追加 Journal、
/// 不捕获 Host 对象、不作为恢复中的暂停协程。
type OrchestratorCommand =
    | AwaitManager of ManagerJobId
    | ReadTargetHead of TargetRef
    | RebaseOnto of WorktreePath * TargetRef
    | ReviewRound of ManagerJobId * SessionId * WorktreePath * ReviewBarrierId * phase: string * round: int
    | RecordCandidateReady of ManagerJobId * CommitHash * ReviewBarrierId
    | RecordRebasedReady of ManagerJobId * rebased: CommitHash * target: CommitHash * ReviewBarrierId
    | RecordConflict of ManagerJobId * candidate: CommitHash * target: CommitHash * files: string list
    | PublishUnderGate of ManagerJobId * CommitHash
    | TerminateChildren of ManagerJobId
    | ReleaseWorktree of WorktreePath
    | AppendFact of AgentFact

[<AutoOpen>]
module OrchestratorProgramTypes =

    /// DSL 程序是待解释的数据，不是正在执行的协程。
    type OrchestratorProgram =
        | Return of Result<CommitHash, string> option
        | Step of OrchestratorCommand * (unit -> OrchestratorProgram)

    /// 递归 monadic bind：把后续步骤接到当前程序末尾。
    let rec bindProgram (program: OrchestratorProgram) (cont: unit -> OrchestratorProgram) : OrchestratorProgram =
        match program with
        | Return _ -> cont ()
        | Step(cmd, next) -> Step(cmd, (fun () -> bindProgram (next ()) cont))

    /// Computation expression builder for FLOW-002 DSL.
    /// Does not run anything; it merely assembles a data structure.
    type OrchestratorBuilder() =
        member _.Return(value: Result<CommitHash, string> option) : OrchestratorProgram = Return value

        member _.ReturnFrom(program: OrchestratorProgram) : OrchestratorProgram = program

        member _.Zero() : OrchestratorProgram = Return None

        member _.Bind(program: OrchestratorProgram, cont: unit -> OrchestratorProgram) : OrchestratorProgram =
            bindProgram program cont

        member _.Delay(f: unit -> OrchestratorProgram) : OrchestratorProgram = f ()

        member _.Combine(left: OrchestratorProgram, right: OrchestratorProgram) : OrchestratorProgram =
            bindProgram left (fun () -> right)

    /// 空程序与 builder 实例。
    [<RequireQualifiedAccess>]
    module OrchestratorProgram =
        let empty: OrchestratorProgram = Return None

    let orchestrator = OrchestratorBuilder()

/// Trace Interpreter：将程序解释为可观察轨迹（FLOW-003 / 9.4）。
/// 不执行副作用，只输出领域操作名称与顺序。
module TraceInterpreter =
    let private describeCommand (command: OrchestratorCommand) : string =
        match command with
        | AwaitManager id -> $"AwaitManager({ManagerJobId.value id})"
        | ReadTargetHead ref -> $"ReadTargetHead({TargetRef.value ref})"
        | RebaseOnto(path, ref) -> $"RebaseOnto({WorktreePath.value path}, {TargetRef.value ref})"
        | ReviewRound(jobId, _, _, _, phase, round) -> $"ReviewRound({ManagerJobId.value jobId}, {phase}, {round})"
        | RecordCandidateReady(jobId, commit, _) ->
            $"RecordCandidateReady({ManagerJobId.value jobId}, {CommitHash.value commit})"
        | RecordRebasedReady(jobId, rebased, target, _) ->
            $"RecordRebasedReady({ManagerJobId.value jobId}, {CommitHash.value rebased}, {CommitHash.value target})"
        | RecordConflict(jobJobId, candidate, target, files) ->
            let filesText = String.concat ";" files

            $"RecordConflict({ManagerJobId.value jobJobId}, {CommitHash.value candidate}, {CommitHash.value target}, [{filesText}])"
        | PublishUnderGate(jobId, head) -> $"PublishUnderGate({ManagerJobId.value jobId}, {CommitHash.value head})"
        | TerminateChildren id -> $"TerminateChildren({ManagerJobId.value id})"
        | ReleaseWorktree path -> $"ReleaseWorktree({WorktreePath.value path})"
        | AppendFact _ -> "AppendFact"

    let rec interpret (program: OrchestratorProgram) : string list =
        match program with
        | Return value ->
            match value with
            | None -> [ "Return(None)" ]
            | Some(Ok head) -> [ $"Return(Ok {CommitHash.value head})" ]
            | Some(Error reason) -> [ $"Return(Error {reason})" ]
        | Step(cmd, next) -> describeCommand cmd :: interpret (next ())
