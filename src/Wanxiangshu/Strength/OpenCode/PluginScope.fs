namespace Wanxiangshu.Strength.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Resources
open Wanxiangshu.Strength.Persistence

open System.Collections.Generic
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Completed counterfactual pair — typed outcome consumed once by the owning CE.
/// Business layer observes only this type, never the collector's internal registries.
type CounterfactualPair =
    { Feature: StrengthFeatureKey
      FirstSymbol: StrengthPrimarySymbol
      SecondSymbol: StrengthPrimarySymbol }

type private CounterfactualFirst =
    { Feature: StrengthFeatureKey
      Symbol: StrengthPrimarySymbol }

/// Physical adapter — collects two primary observations into one CounterfactualPair.
/// No cross-callback program counter: armed targets and buffered firsts are
/// independent physical resource registries, not a phased await state machine.
/// Observe returns the completed pair directly; no separate TryTake/consume API.
/// DSL-cross-callback-proof: physical — armed target registry and buffered
/// first-observation registry are physical resources consumed once by Observe.
type private CounterfactualCollector() =
    /// DSL-cross-callback-proof: physical single-flight — target identity for one two-observation collector
    // DSL-MUTABLE: resource — armed counterfactual target by session (physical adapter)
    let armedTargets = Dictionary<string, ProviderRunIdentity * StrengthFeatureKey>()
    /// DSL-cross-callback-proof: physical resource — first observation buffer; business receives only completed pair
    // DSL-MUTABLE: resource — buffered first observation by session (physical adapter)
    let observedFirsts = Dictionary<string, CounterfactualFirst>()

    member _.Arm(sessionId: SessionId, targetRun: ProviderRunIdentity, feature: StrengthFeatureKey) =
        let key = SessionId.value sessionId

        if not (armedTargets.ContainsKey key) then
            armedTargets.[key] <- (targetRun, feature)

    member private _.ObserveFirst
        (key: string, feature: StrengthFeatureKey, symbol: StrengthPrimarySymbol, state: StrengthPredictorState)
        : StrengthPredictorState * CounterfactualPair option =
        let next, firstReadonly = StrengthPredictor.observeFirst feature symbol state
        armedTargets.Remove key |> ignore

        if firstReadonly then
            observedFirsts.[key] <- { Feature = feature; Symbol = symbol }

        next, None

    member private _.CompletePair(key: string, feature: StrengthFeatureKey, symbol: StrengthPrimarySymbol) =
        match observedFirsts.TryGetValue key with
        | true, first ->
            observedFirsts.Remove key |> ignore

            let pair =
                { Feature = feature
                  FirstSymbol = first.Symbol
                  SecondSymbol = symbol }

            Some pair
        | false, _ -> None

    member private this.ObserveSecond
        (key: string, feature: StrengthFeatureKey, symbol: StrengthPrimarySymbol, state: StrengthPredictorState)
        : StrengthPredictorState * CounterfactualPair option =
        let next = StrengthPredictor.observeSecond feature symbol state
        next, this.CompletePair(key, feature, symbol)

    member private this.ObserveFromFirstSeen
        (key: string, symbol: StrengthPrimarySymbol, state: StrengthPredictorState)
        : StrengthPredictorState * CounterfactualPair option =
        match observedFirsts.TryGetValue key with
        | true, first -> this.ObserveSecond(key, first.Feature, symbol, state)
        | false, _ -> state, None

    member this.Observe
        (
            sessionId: SessionId,
            providerRun: ProviderRunIdentity,
            symbol: StrengthPrimarySymbol,
            state: StrengthPredictorState
        ) : StrengthPredictorState * CounterfactualPair option =
        let key = SessionId.value sessionId

        match armedTargets.TryGetValue key with
        | true, (targetRun, feature) when targetRun = providerRun -> this.ObserveFirst(key, feature, symbol, state)
        | _ -> this.ObserveFromFirstSeen(key, symbol, state)

    member _.ClearSession(sessionId: string) =
        armedTargets.Remove sessionId |> ignore
        observedFirsts.Remove sessionId |> ignore

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
    // Physical adapter collector — no DU state machine, business observes only CounterfactualPair
    // DSL-MUTABLE: resource — counterfactual collector (physical adapter, typed outcome)
    let collector = CounterfactualCollector()

    /// Fuse is a Result error latch, not a string option. Ok = operational,
    /// Error reason = tripped. TripStrengthFuse is one-shot idempotent: the
    /// first Error wins, matching the former IsNone guard. The Result type
    /// lets callers participate in Result.bind / CE short-circuit flows.
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
    member _.ClearSession(sessionId: string) =
        strengthRecentPrimary.Remove sessionId |> ignore
        collector.ClearSession sessionId

    member _.Dispose() =
        strengthReplicaRuntime |> Option.iter (fun runtime -> runtime.Dispose())
        strengthReplicaRuntime <- None
