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

    val tempPath: unit -> string
    val writeFile: path: string -> data: byte array -> unit
    val appendFile: path: string -> data: byte array -> unit
    val deleteFile: path: string -> unit
    val readFileSyncChunks: path: string -> chunkSize: int -> consume: (byte array -> unit) -> unit

    val readFileAsyncChunks: path: string -> chunkSize: int -> consume: (byte array -> Task<unit>) -> Task<unit>
