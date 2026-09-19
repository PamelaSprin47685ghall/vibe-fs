namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Process

/// delegation-029: Delegation-owned narrow PTY capability port.
/// Decouples PTY tool verbs from HostForkRuntime, Gate, and physical Process types.
type DelegationPtyCapability =
    {
        /// Check if a terminal name is currently mapped to an active PTY.
        TryPtyByName: string -> PtyId option

        /// Atomically allocate and fork a PTY command under the given agent and cwd.
        ForkPty: string * ManagedAgent * string option -> Task<Result<PtyId, string>>

        /// Bind a logical terminal name to an existing active PTY id.
        TryBindTerminalName: string * PtyId -> Result<unit, string>

        /// Clean up and unbind a PTY run by ID.
        UntrackPtyRun: string -> unit

        /// Send input or signals to a PTY, or read its output.
        SendPty: PtyId * string * PtySignal option -> Task<Result<PtyRead, string>>

        /// Verify if a PTY id is owned by the current scope.
        OwnsPty: PtyId -> bool

        /// Try lookup an active PTY by raw string id.
        TryPty: string -> PtyId option

        /// Resolve logical terminal name for a given PTY id, if known.
        TryTerminalNameByPtyId: string -> string option
    }
