namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.OpenCode
open Wanxiangshu.Process

[<RequireQualifiedAccess>]
module DelegationPtyAdapter =
    let ensureLf (prompt: string) =
        if
            prompt.EndsWith("\n", StringComparison.Ordinal)
            || prompt.EndsWith("\r", StringComparison.Ordinal)
        then
            prompt
        else
            prompt + "\n"

    let emptyRead (id: PtyId) : PtyRead =
        { Id = id; Output = ""; Closed = false }

    let mapRead (id: PtyId) (output: string, closed: bool) : PtyRead =
        { Id = id
          Output = output
          Closed = closed }

    let create
        (tryPtyByName: string -> PtyId option)
        (forkPty: string * ManagedAgent * string option -> Task<Result<PtyId, string>>)
        (tryBindTerminalName: string * PtyId -> Result<unit, string>)
        (untrackPtyRun: string -> unit)
        (sendPty: PtyId * string * PtySignal option -> Task<Result<PtyRead, string>>)
        (ownsPty: PtyId -> bool)
        (tryPty: string -> PtyId option)
        (tryTerminalNameByPtyId: string -> string option)
        : DelegationPtyCapability =
        { TryPtyByName = tryPtyByName
          ForkPty = forkPty
          TryBindTerminalName = tryBindTerminalName
          UntrackPtyRun = untrackPtyRun
          SendPty = sendPty
          OwnsPty = ownsPty
          TryPty = tryPty
          TryTerminalNameByPtyId = tryTerminalNameByPtyId }
