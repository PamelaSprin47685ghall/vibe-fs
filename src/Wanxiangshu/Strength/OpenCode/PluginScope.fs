namespace Wanxiangshu.Strength.OpenCode

open System.Collections.Generic
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Replica

/// Completed counterfactual pair — typed outcome consumed once by the owning CE.
/// Business layer observes only this type, never the collector's internal registries.
type CounterfactualPair =
    { Feature: StrengthFeatureKey
      FirstSymbol: StrengthPrimarySymbol
      SecondSymbol: StrengthPrimarySymbol }

type private CounterfactualFirst =
    { Feature: StrengthFeatureKey
      Symbol: StrengthPrimarySymbol
      FirstRun: ProviderRunIdentity }

/// Bounded observation fold — one episode per exact session id.
/// AwaitingFirst carries the armed target run, its exact feature and every seen run;
/// AwaitingSecond carries the immutable first observation and every seen run.
/// Illegal mixed states (an armed target beside an unrelated first, two buffered
/// firsts) are unrepresentable: a session holds exactly one episode.
/// All Arm/Observe/Clear transitions run under one lock, so concurrent or
/// reentrant calls serialize. Losing this cache on restart or drop conservatively
/// degrades evidence count back toward K0; it never authorizes a business effect.
/// Business observes only the completed CounterfactualPair, never this episode.
type private CounterfactualEpisode =
    | AwaitingFirst of TargetRun: ProviderRunIdentity * Feature: StrengthFeatureKey * SeenRuns: Set<string>
    | AwaitingSecond of First: CounterfactualFirst * SeenRuns: Set<string>

type private CounterfactualCollector() =
    // Single bounded observation fold keyed by exact session id.
    let episodes = Dictionary<string, CounterfactualEpisode>()
    let gate = obj ()

    member _.Arm(sessionId: SessionId, targetRun: ProviderRunIdentity, feature: StrengthFeatureKey) =
        let key = SessionId.value sessionId

        lock gate (fun () ->
            if not (episodes.ContainsKey key) then
                episodes.[key] <- AwaitingFirst(targetRun, feature, Set.empty))

    member private _.ObserveLocked
        (key: string, providerRun: ProviderRunIdentity, symbol: StrengthPrimarySymbol, state: StrengthPredictorState)
        : StrengthPredictorState * CounterfactualPair option =
        let runVal = ProviderRunIdentity.value providerRun

        match episodes.TryGetValue key with
        | false, _ ->
            // No episode for this exact session: deterministic no-op.
            state, None
        | true, AwaitingFirst(_, _, seen) when Set.contains runVal seen ->
            // Explicit no-op law for repeated identical observation.
            state, None
        | true, AwaitingFirst(targetRun, feature, seen) when targetRun = providerRun ->
            let next, _ = StrengthPredictor.observeFirst feature symbol state

            episodes.[key] <-
                AwaitingSecond(
                    { Feature = feature
                      Symbol = symbol
                      FirstRun = providerRun },
                    Set.add runVal seen
                )

            next, None
        | true, AwaitingFirst(targetRun, feature, seen) ->
            // Unrelated observation before the target: record the seen run, keep waiting.
            episodes.[key] <- AwaitingFirst(targetRun, feature, Set.add runVal seen)
            state, None
        | true, AwaitingSecond(first, seen) when first.FirstRun = providerRun || Set.contains runVal seen ->
            // Same run can never serve as its own second sample; repeats are idempotent no-ops.
            state, None
        | true, AwaitingSecond(first, _) ->
            let next = StrengthPredictor.observeSecond first.Feature symbol state
            episodes.Remove key |> ignore

            let pair =
                { Feature = first.Feature
                  FirstSymbol = first.Symbol
                  SecondSymbol = symbol }

            next, Some pair

    member this.Observe
        (
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            symbol: StrengthPrimarySymbol,
            state: StrengthPredictorState
        ) : StrengthPredictorState * CounterfactualPair option =
        let key = SessionId.value sessionId

        lock gate (fun () -> this.ObserveLocked(key, providerRun, symbol, state))

    member _.ClearSession(sessionId: string) =
        lock gate (fun () -> episodes.Remove sessionId |> ignore)

    /// Drop every process-local collector episode (all sessions). The fuse is
    /// process-lifetime and lives on PluginStrengthScope, so it is untouched.
    member _.ClearAll() = lock gate (fun () -> episodes.Clear())

/// STRENGTH-*: decision-local replica ownership/capability registry plus bounded
/// predictor evidence for one plugin instance. Durable causality stays in
/// EventStore; this is only live physical-session state (STRENGTH-014).
type PluginStrengthScope() =
    // STRENGTH-014: decision-local replica ownership/capability registry. Durable
    // causality remains in EventStore; this is only live physical-session state.
    let strengthRuntime = StrengthRuntime()
    // STRENGTH-004: physical coordinator is attached after Host ports are wired.
    // The Session StrengthRuntime above remains the sole live ownership/capability registry.
    // DSL-MUTABLE: resource — attached process-local Replica coordinator
    let mutable strengthReplicaRuntime: StrengthReplicaRuntime option = None
    // STRENGTH-010: bounded process-local predictor cache. Losing it on restart
    // only lowers evidence back toward K0; it is never lifecycle authority.
    // DSL-MUTABLE: resource — restart-discardable predictor evidence cache
    let mutable strengthPredictorState = StrengthPredictor.empty
    /// DSL-cross-callback-proof: physical resource — bounded restart-discardable predictor evidence cache
    let strengthRecentPrimary = Dictionary<string, StrengthPrimarySymbol list>()
    // Bounded observation fold — business observes only the completed CounterfactualPair
    // DSL-MUTABLE: resource — counterfactual collector (physical adapter, typed outcome)
    let collector = CounterfactualCollector()

    /// SPEC-INV-011: Fuse is a process-wide monotonic Result error latch owned by PluginStrengthScope
    /// (single safety owner). Ok() = operational; Error reason = permanently tripped (K0 fail-closed).
    /// One-shot idempotent: once tripped, it can never be cleared by session cleanup, turn reconciliation,
    /// or caller reset. Losing predictor cache on restart conservatively reverts to K0; the fuse ensures
    /// corrupted bundles or invariant violations freeze speculation for the remainder of the process.
    // DSL-MUTABLE: resource — strength fuse latch (Ok=operational, Error=tripped)
    let mutable strengthFuse: Result<unit, string> = Ok()

    member _.StrengthRuntime = strengthRuntime

    member _.AttachStrengthReplicaRuntime(runtime: StrengthReplicaRuntime) = strengthReplicaRuntime <- Some runtime
    member _.StrengthReplicaRuntime = strengthReplicaRuntime

    member _.StrengthFeature(sessionId: SessionId, role: Role, visibleBytes: int) =
        let key = SessionId.value sessionId

        let recent =
            match strengthRecentPrimary.TryGetValue key with
            | true, values -> values
            | false, _ -> []

        StrengthPredictor.feature role recent visibleBytes

    member _.StrengthPrediction(feature: StrengthFeatureKey) =
        StrengthPredictor.predict feature strengthPredictorState

    member _.StrengthBucket(feature: StrengthFeatureKey) =
        StrengthPredictor.bucket feature strengthPredictorState

    member _.TripStrengthFuse(reason: string) =
        match strengthFuse with
        | Ok() -> strengthFuse <- Error reason
        | Error _ -> ()

    member _.StrengthFuseReason =
        match strengthFuse with
        | Ok() -> None
        | Error reason -> Some reason

    member _.StrengthFuse = strengthFuse

    member _.ArmStrengthCounterfactual
        (sessionId: SessionId, targetRun: ProviderRunIdentity, feature: StrengthFeatureKey)
        =
        collector.Arm(sessionId, targetRun, feature)

    member _.ObserveStrengthPrimary
        (sessionId: SessionId, providerRun: ProviderRunIdentity, symbol: StrengthPrimarySymbol)
        : CounterfactualPair option =
        let key = SessionId.value sessionId

        let nextState, pair =
            collector.Observe(sessionId, providerRun, symbol, strengthPredictorState)

        strengthPredictorState <- nextState

        let recent =
            match strengthRecentPrimary.TryGetValue key with
            | true, values -> symbol :: values |> List.truncate 3
            | false, _ -> [ symbol ]

        strengthRecentPrimary.[key] <- recent

        pair

    /// Session deletion drops the decision-local Strength evidence for that session
    /// (mirror of DisposeSession's per-session cleanup in PluginRuntimeScope).
    member private _.RetireOrphanSessionBinding(replicaId: SessionId) =
        match strengthRuntime.TryFindByReplica replicaId with
        | Some _ -> strengthRuntime.Retire replicaId |> ignore
        | None ->
            strengthRuntime.TryFindByOwner replicaId
            |> Option.iter (fun binding -> strengthRuntime.Retire binding.ReplicaSessionId |> ignore)

    member this.ClearSession(sessionId: string) =
        strengthRecentPrimary.Remove sessionId |> ignore
        collector.ClearSession sessionId
        // The strength fuse is a process-lifetime latch and is never cleared here.
        // Retire this session's live-registry side (as replica, else as owner)
        // so no orphan binding survives the session. Model-lease release stays
        // with the attached StrengthReplicaRuntime, which shares this registry.
        let replicaId = SessionId.create sessionId

        match strengthReplicaRuntime with
        | Some runtime -> runtime.HandleSessionDeleted replicaId
        | None -> this.RetireOrphanSessionBinding replicaId

    member _.Dispose() =
        strengthReplicaRuntime |> Option.iter (fun runtime -> runtime.Dispose())
        strengthReplicaRuntime <- None
        // Drop every process-local cache: collector, recent-primary window,
        // predictor evidence and the live ownership registry. The strength fuse
        // is a process-lifetime latch and is never cleared here.
        collector.ClearAll()
        strengthRecentPrimary.Clear()
        strengthPredictorState <- StrengthPredictor.empty
        strengthRuntime.Clear()
