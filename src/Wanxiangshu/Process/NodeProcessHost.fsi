namespace Wanxiangshu.Process

open System.Threading
open System.Threading.Tasks

module NodeProcessHost =
    type ChildProcess =
        { Process: obj
          Exit: TaskCompletionSource<int>
          Kill: unit -> unit
          Exited: bool ref
          OnExited: ResizeArray<unit -> unit> }

    val notifyExited: child: ChildProcess -> unit

    val spawn:
        cmd: Command ->
        ctx: ProcessContext ->
        onStdout: (byte array -> unit) ->
        onStderr: (byte array -> unit) ->
        ct: CancellationToken ->
            Task<Result<ChildProcess, string>>
