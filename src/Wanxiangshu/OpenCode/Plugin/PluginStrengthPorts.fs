namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Strength
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

module PluginStrengthPorts =

    /// Build the neutral Host ports from the composition-held Strength state.
    ///
    /// The replica-dependent closures are evaluated LAZILY, at the moment of
    /// each event, from the live `strengthScope`.
    /// `PluginSessionWiring.attach` installs the `StrengthReplicaRuntime` AFTER
    /// this record is built (SpikePlugin.fs: host wiring first, attach second),
    /// so an eager capture here would permanently observe `None` and the
    /// replica turn / cancel / delete paths would never fire. A late read with
    /// no runtime attached answers exactly like an absent handler:
    /// HostTurnObserver.fs treats `None` and a handler returning `false`
    /// identically, and the cancel/delete consumers iterate an empty effect.
    /// Ordering and failure semantics are verbatim from `HostSignalBootstrap`:
    /// replica pre-turn short-circuit first; the durable append runs before
    /// ordinary turn observation; projection-load failure trips the fuse and
    /// raises, semantic rejection is process-fatal, storage failure trips the
    /// fuse and raises.
    let create
        (strengthScope: PluginStrengthScope option)
        (strengthDurability: StrengthDurabilityPort option)
        : HostSignalBootstrap.StrengthHostPorts =
        let handlePreTurn =
            strengthScope
            |> Option.map (fun s ->
                fun (turn: ReconciledTurn) ->
                    match s.StrengthReplicaRuntime with
                    | Some runtime -> Task.FromResult(runtime.HandleTurn turn)
                    | None -> Task.FromResult false)

        /// Both durability failures leave this process unable to trust the
        /// promotion it just attempted: trip the fuse, then raise.
        let tripFuse (message: string) : unit =
            strengthScope |> Option.iter (fun s -> s.TripStrengthFuse message)
            raise (InvalidOperationException message)

        let failSettlement eventId failure : unit =
            let message = AppendError.describe failure
            strengthScope |> Option.iter (fun scope -> scope.TripStrengthFuse message)

            if not (List.isEmpty (AppendError.semanticCuts failure)) then
                Diagnostic.fatal "strength-semantic-cut" [ "result", message ]

            raise (StrengthAppendException(eventId, failure, []))

        let commitAppendResult (appendResult: StrengthDurableAppend) : unit =
            match appendResult with
            | StrengthDurableAppend.Applied -> ()
            | StrengthDurableAppend.SemanticRejected error ->
                // durable-events-021: process is no longer trustworthy
                Diagnostic.fatal "strength-semantic-cut" [ "result", error ]
            | StrengthDurableAppend.StorageInvalid reason ->
                tripFuse ("Strength promotion commit storage invalid: " + reason)
            | StrengthDurableAppend.StorageFailed reason ->
                tripFuse ("Strength promotion commit storage failure: " + reason)
            | StrengthDurableAppend.SettlementFailed(eventId, failure) -> failSettlement eventId failure

        let commitReconciledEvent durability projection (turn: ReconciledTurn) : Task<unit> =
            task {
                match StrengthLifecycle.reconcileEvent projection turn with
                | None -> ()
                | Some ev ->
                    let! appendResult = durability.Append ev
                    return commitAppendResult appendResult
            }

        let commitDurablePromotion (durability: StrengthDurabilityPort) (turn: ReconciledTurn) : Task<unit> =
            task {
                match! durability.LoadProjection() with
                | Error err -> return tripFuse ("Strength promotion projection failed: " + err)
                | Ok projection -> return! commitReconciledEvent durability projection turn
            }

        /// speculative-investigation-010 / STRENGTH-007: the durable append of
        /// Promoted/Abandoned runs before ordinary turn observation. Promotion
        /// consumption proof is exactly the reconciled turn evidence; primary
        /// counterfactual observation no longer exists.
        let observePrimaryTurnBody (turn: ReconciledTurn) : Task<unit> =
            task {
                match strengthDurability with
                | Some durability -> do! commitDurablePromotion durability turn
                | None -> ()
            }

        let observePrimaryTurn =
            match strengthDurability, strengthScope with
            | None, None -> None
            | _ -> Some observePrimaryTurnBody

        let cancelStrengthOwner =
            strengthScope
            |> Option.map (fun s ->
                fun (sessionId: SessionId) ->
                    s.StrengthReplicaRuntime
                    |> Option.iter (fun runtime -> runtime.CancelOwner sessionId |> ignore))

        let onSessionDeleted =
            strengthScope
            |> Option.map (fun s ->
                fun (sid: SessionId) ->
                    s.StrengthReplicaRuntime
                    |> Option.iter (fun runtime ->
                        runtime.CancelOwner sid |> ignore
                        runtime.HandleSessionDeleted sid))

        { HandlePreTurn = handlePreTurn
          ObservePrimaryTurn = observePrimaryTurn
          CancelStrengthOwner = cancelStrengthOwner
          OnSessionDeleted = onSessionDeleted }
