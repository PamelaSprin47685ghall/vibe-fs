namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Ablation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Foundation.HostSession

/// JS-native Host boundary surface for Fission turn absorption.
///
/// This module composes host session/event ports with ordinary-turn observation
/// and publishes only a JSON-shaped observation for the logical-owner law. It
/// keeps Host capabilities private; callers cannot obtain emitted turn values.
/// The canary logic itself lives in the product lane
/// (`Wanxiangshu.Execution.Fission.FissionCanaries`); this surface only binds
/// it to the production OpenCode quiescence gate.
module FissionHostSurface =

    /// intra-participant-parallelism-013: expose the exact request-local
    /// provider tool projection without exposing Host session registries.
    let projectFissionToolVisibility (hasPhysicalParent: bool) (tools: obj) : obj =
        if
            FissionToolVisibility.hidesFissionTool
                hasPhysicalParent
                (AblationGate.fissionVisible (AblationGate.registry ()))
        then
            tools?fission <- box false

        tools

    /// Executable canary for the production Fission terminal bridge. OpenCode
    /// transports may deliver the exact final assistant message while dropping
    /// the later session.status/session.idle event. The bridge records that
    /// projection edge and opens a RetryWake snapshot occasion; the snapshot,
    /// not the edge, remains the authority for TurnCompleted.
    let missingIdleTerminalBridgeScenario () : Task<obj> =
        FissionCanaries.missingIdleTerminalBridgeScenario ()

    /// Absorb a Fission-replaced owner turn through Host + ordinary-turn observe.
    /// Caller must have already `markSilentInterrupt`'d the owner.
    let observeReplacedOwner (ownerSessionId: string) : Task<obj> =
        FissionCanaries.observeReplacedOwnerWith
            (fun () -> SessionQuiescenceGate() :> ISessionQuiescenceGate)
            (SessionId.create ownerSessionId)
