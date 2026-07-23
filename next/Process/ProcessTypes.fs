namespace Wanxiangshu.Next.Process

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel

[<RequireQualifiedAccess>]
type ProcessError =
    | SpawnFailed of reason: string
    | ProcessCancelled of reason: string
    | Timeout of reason: string
    | ExecutionFailed of reason: string

type ProcessContext =
    { WorkingDirectory: string option
      DefaultTimeout: TimeSpan option }

type PtyOptions =
    { Cols: int
      Rows: int }

type ProcessHandle =
    inherit IDisposable
    inherit IAsyncDisposable
    abstract ExitCodeTask: Task<int>
    abstract StdoutTask: Task<string * bool>
    abstract StderrTask: Task<string * bool>
    abstract IsPty: bool
    abstract ResizePty: cols: int * rows: int -> unit
    abstract RunToCompletion: unit -> Flow<ProcessContext, ProcessError, Fact.ProcessResult>
    abstract Kill: unit -> Task<unit>
