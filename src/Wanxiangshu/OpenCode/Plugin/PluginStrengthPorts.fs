namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Strength
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

module PluginStrengthPorts =

    /// Build the neutral Host ports from the composition-held Strength state.
    ///
    /// Evaluation point matches the old inline construction: `strengthScope`
    /// is the live scope object (primary observation and fuse trips read
    /// through it per turn), while the replica runtime is the wire-time value
    /// (attached later by `PluginSessionWiring`, so still absent here).
    /// Ordering and failure semantics are verbatim from `HostSignalBootstrap`:
    /// replica pre-turn short-circuit first; dry-run close, primary
    /// observation and durability commit before ordinary turn observation;
    /// projection-load failure trips the fuse and raises, semantic rejection
    /// is process-fatal, storage failure trips the fuse and raises.
    let create
        (strengthScope: PluginStrengthScope option)
        (strengthDurability: StrengthDurabilityPort option)
        : HostSignalBootstrap.StrengthHostPorts =
        let strengthReplicaRuntime =
            strengthScope |> Option.bind (fun s -> s.StrengthReplicaRuntime)

        let handlePreTurn =
            strengthReplicaRuntime
            |> Option.map (fun runtime -> fun (turn: ReconciledTurn) -> Task.FromResult(runtime.HandleTurn turn))

        let closeDryRunAtPrimaryTerminal (turn: ReconciledTurn) =
            match strengthReplicaRuntime with
            | Some runtime -> runtime.CloseDryRunAtTargetTerminal turn
            | None -> Task.FromResult() :> Task

        let observeStrengthPrimary (turn: ReconciledTurn) =
            strengthScope
            |> Option.bind (fun s ->
                s.ObserveStrengthPrimary(
                    turn.SessionId,
                    turn.ProviderRun,
                    StrengthTurnEvidence.primarySymbol turn.Parts
                ))
            |> Option.iter (fun pair ->
                Diagnostic.emit
                    "strength-counterfactual-observed"
                    [ "session_id", SessionId.value turn.SessionId
                      "provider_run", ProviderRunIdentity.value turn.ProviderRun
                      "result", sprintf "first=%A second=%A" pair.FirstSymbol pair.SecondSymbol ])

        /// Both durability failures leave this process unable to trust the
        /// promotion it just attempted: trip the fuse, then raise.
        let tripFuse (message: string) : unit =
            strengthScope |> Option.iter (fun s -> s.TripStrengthFuse message)
            raise (InvalidOperationException message)

        let commitAppendResult (appendResult: StrengthDurableAppend) : unit =
            match appendResult with
            | StrengthDurableAppend.Applied -> ()
            | StrengthDurableAppend.SemanticRejected error ->
                // DURABLE-EVENTS-021: process is no longer trustworthy
                Diagnostic.fatal "strength-semantic-cut" [ "result", error ]
            | StrengthDurableAppend.StorageFailed reason ->
                tripFuse ("Strength promotion commit storage failure: " + reason)

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

        /// SPEC-INV-013 / STRENGTH-010 / STRENGTH-007: dry-run close, primary
        /// observation and the durable append all run before ordinary turn
        /// observation.
        let observePrimaryTurnBody (turn: ReconciledTurn) : Task<unit> =
            task {
                do! closeDryRunAtPrimaryTerminal turn
                observeStrengthPrimary turn

                match strengthDurability with
                | Some durability -> do! commitDurablePromotion durability turn
                | None -> ()
            }

        let observePrimaryTurn =
            match strengthDurability, strengthScope with
            | None, None -> None
            | _ -> Some observePrimaryTurnBody

        let cancelStrengthOwner =
            strengthReplicaRuntime
            |> Option.map (fun runtime -> fun (sessionId: SessionId) -> runtime.CancelOwner sessionId |> ignore)

        let onSessionDeleted =
            strengthReplicaRuntime
            |> Option.map (fun runtime ->
                fun (sid: SessionId) ->
                    runtime.CancelOwner sid |> ignore
                    runtime.HandleSessionDeleted sid)

        { HandlePreTurn = handlePreTurn
          ObservePrimaryTurn = observePrimaryTurn
          CancelStrengthOwner = cancelStrengthOwner
          OnSessionDeleted = onSessionDeleted }
