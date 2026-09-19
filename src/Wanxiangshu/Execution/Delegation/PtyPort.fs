namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Process

/// delegation-029: Delegation-owned narrow PTY capability port.
/// Decouples PTY tool verbs from HostForkRuntime, Gate, and physical Process types.
type DelegationPtyCapability =
    { TryPtyByName: string -> PtyId option
      ForkPty: string * ManagedAgent * string option -> Task<Result<PtyId, string>>
      TryBindTerminalName: string * PtyId -> Result<unit, string>
      UntrackPtyRun: string -> unit
      SendPty: PtyId * string * PtySignal option -> Task<Result<PtyRead, string>>
      OwnsPty: PtyId -> bool
      TryPty: string -> PtyId option
      TryTerminalNameByPtyId: string -> string option }
