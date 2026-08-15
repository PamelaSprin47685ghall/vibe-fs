namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open FsToolkit.ErrorHandling
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
open Wanxiangshu.Foundation.Identity

module SessionExecutionBinding =
    type private ExpectedBinding = { Agent: string; Model: OpencodeModel }

    let private gate = obj ()
    let private parents = Dictionary<string, string>()
    let private agents = Dictionary<string, string>()
    let private models = Dictionary<string, OpencodeModel>()
    let private internalBindings = Dictionary<string, ExpectedBinding list>()

    let private rememberParent (childKey: string) (parentKey: string) =
        match parents.TryGetValue childKey with
        | true, existing when existing <> parentKey ->
            invalidOp (sprintf "PROMPT-006: parented session '%s' changed parent" childKey)
        | _ -> parents.[childKey] <- parentKey

    let private rememberAgent (childKey: string) (proposed: string) =
        match agents.TryGetValue childKey with
        | true, existing when existing <> proposed ->
            invalidOp (sprintf "PROMPT-006: parented session '%s' agent changed (%s -> %s)" childKey existing proposed)
        | _ -> agents.[childKey] <- proposed

    let bind (parentId: SessionId) (childId: SessionId) (agent: string option) =
        lock gate (fun () ->
            let childKey = SessionId.value childId
            let parentKey = SessionId.value parentId
            rememberParent childKey parentKey

            match agent with
            | Some value when not (String.IsNullOrWhiteSpace value) -> rememberAgent childKey (value.Trim())
            | _ -> ())

    let restore (parentId: SessionId) (childId: SessionId) (agent: string option) = bind parentId childId agent

    /// Fission physical lane: preserve a managed execution binding without
    /// declaring the lane a managed child of the logical owner. The Host parent
    /// edge is handled separately by CreateSiblingSession.
    let bindInternalRoot (sessionId: SessionId) (agent: string option) =
        match
            agent
            |> Option.bind (fun value ->
                if String.IsNullOrWhiteSpace value then
                    None
                else
                    Some(value.Trim()))
        with
        | None -> invalidOp "PROMPT-006: internal root requires a managed agent binding"
        | Some selected ->
            lock gate (fun () ->
                let key = SessionId.value sessionId
                agents.[key] <- selected)

    let tryParent (sessionId: SessionId) =
        lock gate (fun () ->
            match parents.TryGetValue(SessionId.value sessionId) with
            | true, value -> Some(SessionId.create value)
            | false, _ -> None)

    let tryAgent (sessionId: SessionId) =
        lock gate (fun () ->
            match agents.TryGetValue(SessionId.value sessionId) with
            | true, value -> Some value
            | false, _ -> None)

    let tryModel (sessionId: SessionId) =
        lock gate (fun () ->
            match models.TryGetValue(SessionId.value sessionId) with
            | true, value -> Some value
            | false, _ -> None)

    let private rememberModel (sessionId: SessionId) (model: OpencodeModel) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            if not (models.ContainsKey key) then
                models.[key] <- model)

    let private pushInternalBinding (key: string) (agent: string) (model: OpencodeModel) =
        let previous =
            match internalBindings.TryGetValue key with
            | true, value -> value
            | false, _ -> []

        internalBindings.[key] <- { Agent = agent.Trim(); Model = model } :: previous

    let beginInternalSend (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        match opts.Agent, opts.Model with
        | Some agent, Some model when not (String.IsNullOrWhiteSpace agent) ->
            lock gate (fun () ->
                let key = SessionId.value sessionId
                pushInternalBinding key agent model)
        | _ -> invalidOp "PROMPT-006: internal send has no concrete agent/model binding"

    let endInternalSend (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match internalBindings.TryGetValue key with
            | true, _ :: remaining when not (List.isEmpty remaining) -> internalBindings.[key] <- remaining
            | true, _ -> internalBindings.Remove key |> ignore
            | false, _ -> ())

    let observeUserFacing (sessionId: SessionId) (agent: string) (model: OpencodeModel) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            if
                not (internalBindings.ContainsKey key)
                && not (parents.ContainsKey key)
                && not (String.IsNullOrWhiteSpace agent)
            then
                agents.[key] <- agent.Trim()
                models.[key] <- model)

    let drop (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            parents.Remove key |> ignore
            agents.Remove key |> ignore
            models.Remove key |> ignore
            internalBindings.Remove key |> ignore)

    let private nonEmpty (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Some(value.Trim())

    let private configuredModel agent = ManagedAgentConfig.tryBoundModel agent

    let private tryPeerFromTierRole (tierText: string) (roleText: string) : string option =
        match ManagedAgentCatalog.tryParseTier tierText, ManagedAgentCatalog.tryParseRole roleText with
        | Some tier, Some role -> Some(ManagedAgentCatalog.peerNameOf tier role)
        | _ -> None

    let private tryPeerFromDashName (trimmed: string) : string option =
        match trimmed.IndexOf '-' with
        | index when index > 0 && index < trimmed.Length - 1 ->
            tryPeerFromTierRole (trimmed.Substring(0, index)) (trimmed.Substring(index + 1))
        | _ -> None

    let private tryPeerName (agentName: string) : string option =
        if ManagedAgentCatalog.isBookkeeperName agentName then
            ManagedAgentCatalog.bookkeeperPeerName agentName
        else
            tryPeerFromDashName (agentName.Trim())

    let private sameModel (left: OpencodeModel) (right: OpencodeModel) =
        left.providerID = right.providerID
        && left.modelID = right.modelID
        && match left.variant, right.variant with
           | Some lv, Some rv when not (String.IsNullOrWhiteSpace lv) && not (String.IsNullOrWhiteSpace rv) ->
               lv.Trim() = rv.Trim()
           | _ -> true

    let requiresProviderBindingProof (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            internalBindings.ContainsKey key || parents.ContainsKey key)

    let private providerModelDriftError (expected: OpencodeModel) (actual: OpencodeModel) =
        Error(
            sprintf
                "PROMPT-006: provider model drift (%s/%s -> %s/%s)"
                expected.providerID
                expected.modelID
                actual.providerID
                actual.modelID
        )

    let private validatePeerModel (peer: string) (model: OpencodeModel) : Result<bool, string> =
        match configuredModel peer with
        | Some peerModel when sameModel peerModel model -> Ok true
        | Some peerModel -> providerModelDriftError peerModel model
        | None -> Error(sprintf "PROMPT-006: peer agent '%s' has no configured model binding" peer)

    let private validatePeerAgent (baseAgent: string) (agent: string) (model: OpencodeModel) : Result<bool, string> =
        match tryPeerName baseAgent with
        | Some peer when peer = agent.Trim() -> validatePeerModel peer model
        | _ -> Error(sprintf "PROMPT-006: provider agent drift (%s -> %s)" baseAgent agent)

    let private validateFrozenBinding
        (baseAgent: string)
        (baseModel: OpencodeModel)
        (agent: string)
        (model: OpencodeModel)
        : Result<bool, string> =
        if baseAgent <> agent.Trim() then
            validatePeerAgent baseAgent agent model
        elif sameModel baseModel model then
            Ok true
        else
            providerModelDriftError baseModel model

    let private validateParentedProvider (key: string) (agent: string) (model: OpencodeModel) : Result<bool, string> =
        match agents.TryGetValue key, models.TryGetValue key with
        | (true, baseAgent), (true, baseModel) -> validateFrozenBinding baseAgent baseModel agent model
        | _ -> Error "PROMPT-006: parented provider run has no frozen agent/model binding"

    let validateObservedProvider (sessionId: SessionId) (agent: string) (model: OpencodeModel) : Result<bool, string> =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match internalBindings.TryGetValue key with
            | true, current :: _ when current.Agent = agent.Trim() && sameModel current.Model model -> Ok true
            | _ when parents.ContainsKey key -> validateParentedProvider key agent model
            | _ -> Ok false)

    let private rejectOverrideModelMismatch
        (agent: string)
        (configured: OpencodeModel option)
        (opts: OpenCodePromptOptions)
        : Result<unit, string> =
        match configured, opts.Model with
        | Some bound, Some requested when not (sameModel bound requested) ->
            Error(sprintf "PROMPT-006: execution override model does not match agent '%s' binding" agent)
        | _ -> Ok()

    let private resolveOverrideModel
        (agent: string)
        (configured: OpencodeModel option)
        (opts: OpenCodePromptOptions)
        : Result<OpencodeModel, string> =
        configured
        |> Option.orElse opts.Model
        |> Result.requireSome (sprintf "PROMPT-006: execution override for '%s' has no provable model" agent)

    let private normalizeOverride (opts: OpenCodePromptOptions) =
        result {
            let! agent =
                opts.Agent
                |> Option.bind nonEmpty
                |> Result.requireSome "PROMPT-006: execution override requires an explicit managed agent"

            let configured = configuredModel agent
            do! rejectOverrideModelMismatch agent configured opts
            let! model = resolveOverrideModel agent configured opts

            return
                { opts with
                    Agent = Some agent
                    Model = Some model }
        }

    let private requireRequestedAgent (label: string) (baseAgent: string) (requestedAgent: string option) =
        if requestedAgent <> Some baseAgent then
            Error(
                sprintf
                    "PROMPT-006: %s agent drift (%s -> %s)"
                    label
                    baseAgent
                    (requestedAgent |> Option.defaultValue "<missing>")
            )
        else
            Ok()

    let private requireBindingModel (label: string) (baseAgent: string) (baseModel: OpencodeModel option) =
        match baseModel with
        | None -> Error(sprintf "PROMPT-006: %s '%s' has no provable model binding" label baseAgent)
        | Some model -> Ok model

    let private requireModelCompatible (label: string) (model: OpencodeModel) (opts: OpenCodePromptOptions) =
        match opts.Model with
        | Some requested when not (sameModel requested model) ->
            Error(
                sprintf
                    "PROMPT-006: %s model drift (%s/%s -> %s/%s)"
                    label
                    model.providerID
                    model.modelID
                    requested.providerID
                    requested.modelID
            )
        | _ -> Ok()

    let private preserveBinding label baseAgent baseModel (opts: OpenCodePromptOptions) =
        result {
            let requestedAgent = opts.Agent |> Option.bind nonEmpty
            do! requireRequestedAgent label baseAgent requestedAgent
            let! model = requireBindingModel label baseAgent baseModel
            do! requireModelCompatible label model opts

            return
                { opts with
                    Agent = Some baseAgent
                    Model = Some model }
        }

    let private normalizeManagedPreserve (sessionId: SessionId) (baseAgent: string) (opts: OpenCodePromptOptions) =
        let model =
            tryModel sessionId
            |> Option.orElseWith (fun () -> configuredModel baseAgent)
            |> Option.orElse opts.Model

        preserveBinding "parented session" baseAgent model opts
        |> Result.map (fun normalized ->
            model |> Option.iter (rememberModel sessionId)
            normalized)

    let private normalizeManagedOverride (sessionId: SessionId) (baseAgent: string) (opts: OpenCodePromptOptions) =
        match tryModel sessionId |> Option.orElseWith (fun () -> configuredModel baseAgent) with
        | None -> Error "PROMPT-006: parented session has no provable base model binding"
        | Some baseModel ->
            rememberModel sessionId baseModel
            normalizeOverride opts

    let private normalizeManagedByIntent (sessionId: SessionId) (baseAgent: string) (opts: OpenCodePromptOptions) =
        match opts.BindingIntent with
        | SessionBindingIntent.Preserve -> normalizeManagedPreserve sessionId baseAgent opts
        | SessionBindingIntent.ExplicitExecutionOverride -> normalizeManagedOverride sessionId baseAgent opts

    let normalizeManagedPrompt (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        match tryAgent sessionId |> Option.bind nonEmpty with
        | None -> Error "PROMPT-006: parented session has no frozen agent binding"
        | Some baseAgent -> normalizeManagedByIntent sessionId baseAgent opts

    let private normalizeUserFacingByIntent (baseAgent: string) (model: OpencodeModel) (opts: OpenCodePromptOptions) =
        match opts.BindingIntent with
        | SessionBindingIntent.Preserve -> preserveBinding "user-facing session" baseAgent (Some model) opts
        | SessionBindingIntent.ExplicitExecutionOverride -> normalizeOverride opts

    let private normalizeUserFacingWithAgent (sessionId: SessionId) (baseAgent: string) (opts: OpenCodePromptOptions) =
        match tryModel sessionId |> Option.orElseWith (fun () -> configuredModel baseAgent) with
        | None -> Error "PROMPT-006: user-facing session has no provable model binding"
        | Some model ->
            rememberModel sessionId model
            normalizeUserFacingByIntent baseAgent model opts

    let normalizeUserFacingPrompt (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        match tryAgent sessionId with
        | Some baseAgent -> normalizeUserFacingWithAgent sessionId baseAgent opts
        | None -> Error "PROMPT-006: user-facing session has no observed user binding"
