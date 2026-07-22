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

type ProcessHandle =
    inherit IAsyncDisposable
    abstract ExitCodeTask: Task<int>
    abstract StdoutTask: Task<string * bool>
    abstract StderrTask: Task<string * bool>
    abstract RunToCompletion: unit -> Flow<ProcessContext, ProcessError, Fact.ProcessResult>
    abstract Kill: unit -> Task<unit>
