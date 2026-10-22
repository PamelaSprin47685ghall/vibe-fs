namespace Wanxiangshu.Execution.Fission

open System.Threading.Tasks
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Foundation.Identity

/// Executable canaries for the Fission lane. They live in the product lane
/// (not in the OpenCode adapter) so the product behaviour is proven without
/// an adapter folder. Host capabilities arrive as parameters.
[<RequireQualifiedAccess>]
module FissionCanaries =

    /// intra-participant-parallelism-009: the terminal bridge records the
    /// projection edge and opens a RetryWake snapshot occasion; the snapshot,
    /// not the edge, remains the authority for TurnCompleted.
    val missingIdleTerminalBridgeScenario: unit -> Task<obj>

    /// Absorb a Fission-replaced owner turn through Host + ordinary-turn observe.
    /// The caller must have already `markSilentInterrupt`'d the owner.
    /// `quiescenceFactory` builds the host's gate (only the OpenCode adapter
    /// can build the production one), so the canary stays adapter-free.
    val observeReplacedOwnerWith:
        quiescenceFactory: (unit -> ISessionQuiescenceGate) -> ownerSessionId: SessionId -> Task<obj>
