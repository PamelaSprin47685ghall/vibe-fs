namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// STRENGTH-*: decision-local replica ownership/capability registry plus bounded
/// predictor evidence for one plugin instance. Durable causality stays in
/// EventStore; this is only live physical-session state (STRENGTH-014).
type PluginStrengthScope() =
    // STRENGTH-014: decision-local replica ownership/capability registry. Durable
    // causality remains in EventStore; this is only live physical-session state.
    let strengthRuntime = StrengthRuntime()
    // PROMPT-002/AGENT-003: Host-final managed inventory is captured from the
    // config gate so Strength can prove same-role fast/deep model bindings differ.
    // DSL-MUTABLE: resource — process-local Host-final managed-agent inventory cache
    let mutable managedAgentInventory: ManagedAgentConfig.ManagedAgentInventory option =
        None
    // STRENGTH-004: physical coordinator is attached after Host ports are wired.
    // The Session StrengthRuntime above remains the sole live ownership/capability registry.
    // DSL-MUTABLE: resource — attached process-local Replica coordinator
    let mutable strengthReplicaRuntime: StrengthReplicaRuntime option = None
    // STRENGTH-010: bounded process-local predictor cache. Losing it on restart
    // only lowers evidence back toward K0; it is never lifecycle authority.
    // DSL-MUTABLE: resource — restart-discardable predictor evidence cache
    let mutable strengthPredictorState = StrengthPredictor.empty
    let strengthRecentPrimary = Dictionary<string, StrengthPrimarySymbol list>()

    let strengthPendingFirst =
        Dictionary<string, ProviderRunIdentity * StrengthFeatureKey>()

    let strengthPendingSecond = Dictionary<string, StrengthFeatureKey>()
    // DSL-MUTABLE: resource — process-local Strength fuse latch
    let mutable strengthFuseReason: string option = None

    member _.StrengthRuntime = strengthRuntime

    member _.RecordManagedAgentInventory(inventory: ManagedAgentConfig.ManagedAgentInventory) =
        managedAgentInventory <- Some inventory

    member _.ManagedAgentInventory = managedAgentInventory
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
        if strengthFuseReason.IsNone then
            strengthFuseReason <- Some reason

    member _.StrengthFuseReason = strengthFuseReason

    member _.ArmStrengthCounterfactual
        (sessionId: SessionId, targetRun: ProviderRunIdentity, feature: StrengthFeatureKey)
        =
        let key = SessionId.value sessionId

        if
            not (strengthPendingFirst.ContainsKey key)
            && not (strengthPendingSecond.ContainsKey key)
        then
            strengthPendingFirst.[key] <- (targetRun, feature)

    member _.ObserveStrengthPrimary
        (sessionId: SessionId, providerRun: ProviderRunIdentity, symbol: StrengthPrimarySymbol)
        =
        let key = SessionId.value sessionId

        match strengthPendingFirst.TryGetValue key with
        | true, (targetRun, feature) when targetRun = providerRun ->
            strengthPendingFirst.Remove key |> ignore

            let next, firstReadonly =
                StrengthPredictor.observeFirst feature symbol strengthPredictorState

            strengthPredictorState <- next

            if firstReadonly then
                strengthPendingSecond.[key] <- feature
        | _ ->
            match strengthPendingSecond.TryGetValue key with
            | true, feature ->
                strengthPendingSecond.Remove key |> ignore
                strengthPredictorState <- StrengthPredictor.observeSecond feature symbol strengthPredictorState
            | false, _ -> ()

        let recent =
            match strengthRecentPrimary.TryGetValue key with
            | true, values -> symbol :: values |> List.truncate 3
            | false, _ -> [ symbol ]

        strengthRecentPrimary.[key] <- recent

    /// Session deletion drops the decision-local Strength evidence for that session
    /// (mirror of DisposeSession's per-session cleanup in PluginRuntimeScope).
    member _.ClearSession(sessionId: string) =
        strengthRecentPrimary.Remove sessionId |> ignore
        strengthPendingFirst.Remove sessionId |> ignore
        strengthPendingSecond.Remove sessionId |> ignore

    member _.Dispose() =
        strengthReplicaRuntime |> Option.iter (fun runtime -> runtime.Dispose())
        strengthReplicaRuntime <- None
