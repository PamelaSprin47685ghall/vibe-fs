namespace Wanxiangshu.Process

open System.Threading
open System.Threading.Tasks

module ProcessRunner =
    val runWithHost:
        hostSpawn: (Command -> ProcessContext -> (byte array -> unit) -> (byte array -> unit) -> CancellationToken -> Task<Result<NodeProcessHost.ChildProcess, string>>) ->
        cmd: Command ->
        estimate: ProcessEstimate ->
        ctx: ProcessContext ->
        ct: CancellationToken ->
        Task<Result<ProcessOutcome, ProcessError>>

    val run:
        cmd: Command ->
        estimate: ProcessEstimate ->
        ctx: ProcessContext ->
        ct: CancellationToken ->
        Task<Result<ProcessOutcome, ProcessError>>

    val runWithLauncher:
        launcher: (Command -> CancellationToken -> Task<int * byte array * byte array>) ->
        cmd: Command ->
        estimate: ProcessEstimate ->
        ctx: ProcessContext ->
        ct: CancellationToken ->
        Task<Result<ProcessOutcome, ProcessError>>
