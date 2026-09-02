namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks

/// JS-native Host boundary surface for Fission turn absorption.
///
/// This module composes host session/event ports with ordinary-turn observation
/// and publishes only a JSON-shaped observation for the logical-owner law. It
/// keeps Host capabilities private; callers cannot obtain emitted turn values.
module FissionHostSurface =

    /// INTRA-PARTICIPANT-PARALLELISM-013: expose the exact request-local
    /// provider tool projection without exposing Host session registries.
    val projectFissionToolVisibility: hasPhysicalParent: bool -> tools: obj -> obj

    /// Executable canary for the production Fission terminal bridge. OpenCode
    /// transports may deliver the exact final assistant message while dropping
    /// the later session.status/session.idle event. The bridge records that
    /// projection edge and opens a RetryWake snapshot occasion; the snapshot,
    /// not the edge, remains the authority for TurnCompleted.
    val missingIdleTerminalBridgeScenario: unit -> Task<obj>

    /// Absorb a Fission-replaced owner turn through Host + ordinary-turn observe.
    /// Caller must have already `markSilentInterrupt`'d the owner.
    val observeReplacedOwner: ownerSessionId: string -> Task<obj>
