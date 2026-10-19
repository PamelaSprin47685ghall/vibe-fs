namespace Wanxiangshu.Process

open System.Threading
open System.Threading.Tasks

module ProcessRunner =
    val runWithHost:
        host:
            (Command
                -> ProcessContext
                -> (byte array -> unit)
                -> (byte array -> unit)
                -> CancellationToken
                -> Task<Result<NodeProcessHost.ChildProcess, ProcessResult>>) ->
        cmd: Command ->
        estimate: ProcessEstimate ->
        ctx: ProcessContext ->
        ct: CancellationToken ->
            Task<ProcessResult>

    val run:
        cmd: Command -> estimate: ProcessEstimate -> ctx: ProcessContext -> ct: CancellationToken -> Task<ProcessResult>

    val runWithLauncher:
        launcher: (Command -> CancellationToken -> Task<EndEvidence * byte array * byte array>) ->
        cmd: Command ->
        estimate: ProcessEstimate ->
        ctx: ProcessContext ->
        ct: CancellationToken ->
            Task<ProcessResult>
