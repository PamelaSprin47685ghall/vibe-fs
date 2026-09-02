namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Process

[<AutoOpen>]
module HostForkRuntimePty =
    type HostForkRuntime with
        member TrackPtyRun: id: PtyId -> unit
        member RegisterPtySnapshot: id: PtyId -> command: string -> unit
        member UntrackPtyRun: id: string -> unit
        member OwnsPty: id: PtyId -> bool
        member IsPtyCompletion: runId: string -> bool
        member TryBindTerminalName: name: string * id: PtyId -> Result<unit, string>
        member TryPtyByName: name: string -> PtyId option
        member ForkPty: command: string * agent: ManagedAgent * ?cwd: string -> Task<Result<PtyId, string>>
        member TryPty: id: string -> PtyId option
        member SendPty: id: PtyId * prompt: string * signal: PtySignal option -> Task<Result<PtyRead, string>>
