namespace Wanxiangshu.Process

open System.Threading
open System.Threading.Tasks

module ProcessResultWire =
    val read: obj -> ProcessResult

module NodeProcessHost =
    type ChildProcess =
        { Process: obj
          Observe: unit -> EndEvidence
          Kill: unit -> unit
          Exited: bool ref
          OnExited: ResizeArray<unit -> unit>
          Failure: string option ref
          Rejection: NoExecution option ref }

    val notifyExited: child: ChildProcess -> unit

    val spawn:
        cmd: Command ->
        ctx: ProcessContext ->
        onStdout: (byte array -> unit) ->
        onStderr: (byte array -> unit) ->
        ct: CancellationToken ->
            Task<Result<ChildProcess, ProcessResult>>

    val spawnBinary:
        fileName: string ->
        arguments: string list ->
        workingDirectory: string ->
        stdin: byte[] option ->
        onStdout: (byte[] -> unit) ->
        onStderr: (byte[] -> unit) ->
            Task<Result<ChildProcess, ProcessResult>>

    val tempPath: unit -> string
    val writeFile: path: string -> data: byte array -> unit
    val appendFile: path: string -> data: byte array -> unit
    val deleteFile: path: string -> unit
    val readFileSyncChunks: path: string -> chunkSize: int -> consume: (byte array -> unit) -> unit

    val readFileAsyncChunks: path: string -> chunkSize: int -> consume: (byte array -> Task<unit>) -> Task<unit>
