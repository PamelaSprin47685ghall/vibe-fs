namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Persona

/// Process-local execution identity binding. Agent identity is frozen/observed here;
/// physical model authority lives in ModelRouting and is leased by (SessionId, EffectiveAgent).
module SessionExecutionBinding =

    type private ExpectedBinding = { Agent: string; Model: OpencodeModel }

    let private gate = obj ()
    let private parents = Dictionary<string, string>()
    let private agents = Dictionary<string, string>()
    // DSL-MUTABLE: resource — accepted plugin prompt execution identity awaiting
    // the provider transform that answers that exact PromptKey. Process-local only;
    // restart intentionally forgets it and therefore cannot resume old sends.
    let private acceptedPromptBindings = Dictionary<string, ExpectedBinding>()
    // DSL-MUTABLE: resource — binding frozen for the provider attempt currently
    // being built by experimental.chat.messages.transform. Replaced at every
    // provider-attempt boundary; never session authority.
    let private providerAttemptBindings = Dictionary<string, ExpectedBinding>()

    let private nonEmpty (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Some(value.Trim())

    let private promptBindingKey (sessionId: SessionId) (promptKey: PromptKey) =
        SessionId.value sessionId + "\u001f" + PromptKey.value promptKey

    let private clearAcceptedPromptBindingsForSession sessionKey =
        acceptedPromptBindings.Keys
        |> Seq.filter (fun bindingKey -> bindingKey.StartsWith(sessionKey + "\u001f", StringComparison.Ordinal))
        |> Seq.toArray
        |> Array.iter (fun bindingKey -> acceptedPromptBindings.Remove bindingKey |> ignore)

    let private rememberParent (childKey: string) (parentKey: string) =
        match parents.TryGetValue childKey with
        | true, existing when existing <> parentKey ->
            invalidOp (sprintf "PROMPT-006: parented session '%s' changed parent" childKey)
        | _ -> parents.[childKey] <- parentKey

    let private rememberAgent (sessionKey: string) (proposed: string) =
        match agents.TryGetValue sessionKey with
        | true, existing when existing <> proposed ->
            invalidOp (
                sprintf "PROMPT-006: parented session '%s' agent changed (%s -> %s)" sessionKey existing proposed
            )
        | _ -> agents.[sessionKey] <- proposed

    let private tierAndRoleTokens (trimmed: string) : (string * string) option =
        match trimmed.IndexOf '-' with
        | index when index > 0 && index < trimmed.Length - 1 ->
            Some(trimmed.Substring(0, index), trimmed.Substring(index + 1))
        | _ -> None

    let private peerFromTokens (tierText, roleText) : string option =
        match ManagedAgentCatalog.tryParseTier tierText, ManagedAgentCatalog.tryParseRole roleText with
        | Some tier, Some role -> Some(ManagedAgentCatalog.peerNameOf tier role)
        | _ -> None

    let private peerFromTieredName (trimmed: string) : string option =
        trimmed |> tierAndRoleTokens |> Option.bind peerFromTokens

    let private tryPeerName (agentName: string) : string option =
        if ManagedAgentCatalog.isBookkeeperName agentName then
            ManagedAgentCatalog.bookkeeperPeerName agentName
        else
            agentName.Trim() |> peerFromTieredName

    let private sameModel (left: OpencodeModel) (right: OpencodeModel) =
        left.providerID = right.providerID
        && left.modelID = right.modelID
        && left.variant = right.variant

    let bind (parentId: SessionId) (childId: SessionId) (agent: string option) =
        lock gate (fun () ->
            let childKey = SessionId.value childId
            rememberParent childKey (SessionId.value parentId)

            match agent |> Option.bind nonEmpty with
            | Some proposed -> rememberAgent childKey proposed
            | None -> ())

    let restore (parentId: SessionId) (childId: SessionId) (agent: string option) = bind parentId childId agent

    /// Fission physical lane: preserve a managed execution identity without declaring
    /// the lane a managed child of the logical owner.
    let bindInternalRoot (sessionId: SessionId) (agent: string option) =
        match agent |> Option.bind nonEmpty with
        | None -> invalidOp "PROMPT-006: internal root requires a managed agent binding"
        | Some selected -> lock gate (fun () -> agents.[SessionId.value sessionId] <- selected)

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

    /// A real external managed user message may choose a new EffectiveAgent. Its model
    /// is deliberately ignored; chat.message routing replaces that field from ModelRouting.
    let observeUserFacingAgent (sessionId: SessionId) (agent: string) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            if not (parents.ContainsKey key) && not (String.IsNullOrWhiteSpace agent) then
                agents.[key] <- agent.Trim())

    /// PROMPT-006: chat.message has physically accepted one plugin-owned prompt.
    /// The caller supplies EffectiveAgent from the still-pending PromptClaim, not
    /// from the Host message. This survives SendPrompt returning, but is addressed
    /// by PromptKey and therefore cannot bless an unrelated later request.
    let acceptPromptExecution
        (sessionId: SessionId)
        (promptKey: PromptKey)
        (effectiveAgent: string)
        (model: OpencodeModel)
        : unit =
        match nonEmpty effectiveAgent with
        | None -> invalidOp "PROMPT-006: accepted plugin prompt has no EffectiveAgent"
        | Some agent ->
            lock gate (fun () ->
                let sessionKey = SessionId.value sessionId
                clearAcceptedPromptBindingsForSession sessionKey
                acceptedPromptBindings.[promptBindingKey sessionId promptKey] <- { Agent = agent; Model = model })

    /// Provider-attempt boundary. A plugin-owned user message must consume the
    /// execution binding registered for its exact PromptKey. External user roots
    /// have no PromptKey and intentionally fall back to the session base binding.
    let private clearProviderAttempt sessionKey =
        lock gate (fun () -> providerAttemptBindings.Remove sessionKey |> ignore)

    let private useAcceptedPromptBinding (sessionId: SessionId) (promptKey: PromptKey) : Result<unit, string> =
        lock gate (fun () ->
            let sessionKey = SessionId.value sessionId
            let bindingKey = promptBindingKey sessionId promptKey

            match acceptedPromptBindings.TryGetValue bindingKey with
            | true, expected ->
                providerAttemptBindings.[sessionKey] <- expected
                Ok()
            | false, _ ->
                Error(
                    sprintf
                        "PROMPT-006: provider attempt for PromptKey %s has no accepted execution binding"
                        (PromptKey.value promptKey)
                ))

    let private beginExternalProviderAttempt sessionId =
        let sessionKey = SessionId.value sessionId
        lock gate (fun () -> clearAcceptedPromptBindingsForSession sessionKey)
        Ok()

    let beginProviderAttempt (sessionId: SessionId) (promptKey: PromptKey option) : Result<unit, string> =
        clearProviderAttempt (SessionId.value sessionId)

        match promptKey with
        | None -> beginExternalProviderAttempt sessionId
        | Some key -> useAcceptedPromptBinding sessionId key

    let drop (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            parents.Remove key |> ignore
            agents.Remove key |> ignore
            providerAttemptBindings.Remove key |> ignore

            clearAcceptedPromptBindingsForSession key)

        ModelRouting.releaseSession sessionId

    let cancelUnacquired (sessionId: SessionId) =
        ModelRouting.cancelUnacquiredSession sessionId

    let requiresProviderBindingProof (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            parents.ContainsKey key || agents.ContainsKey key)

    let private validateLease (sessionId: SessionId) (agent: string) (model: OpencodeModel) =
        match ModelRouting.tryLease sessionId agent with
        | None -> Error(sprintf "PROMPT-006: managed provider run '%s' has no model-routing lease" agent)
        | Some target when ModelRouting.sameTarget target model -> Ok true
        | Some target ->
            let expected = ModelRouting.toOpenCodeModel target

            Error(
                sprintf
                    "PROMPT-006: provider model/reasoning drift (%s/%s[%s] -> %s/%s[%s])"
                    expected.providerID
                    expected.modelID
                    (expected.variant |> Option.defaultValue "<missing>")
                    model.providerID
                    model.modelID
                    (model.variant |> Option.defaultValue "<missing>")
            )

    [<RequireQualifiedAccess>]
    type private ProviderExpectation =
        | ExactAttempt of ExpectedBinding
        | BaseAgent of string
        | MissingParentBinding
        | Unbound

    let private providerExpectation (sessionId: SessionId) : ProviderExpectation =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            let attempt = providerAttemptBindings.TryGetValue key
            let parented = parents.ContainsKey key
            let baseAgent = agents.TryGetValue key

            match attempt, parented, baseAgent with
            | (true, expected), _, _ -> ProviderExpectation.ExactAttempt expected
            | (false, _), true, (false, _) -> ProviderExpectation.MissingParentBinding
            | (false, _), _, (true, agent) -> ProviderExpectation.BaseAgent agent
            | _ -> ProviderExpectation.Unbound)

    let private validateExactAttempt
        (sessionId: SessionId)
        (expected: ExpectedBinding)
        (observedAgent: string)
        (model: OpencodeModel)
        =
        if expected.Agent <> observedAgent then
            Error(sprintf "PROMPT-006: provider agent drift (%s -> %s)" expected.Agent observedAgent)
        elif not (sameModel expected.Model model) then
            Error "PROMPT-006: provider model/reasoning drift from accepted prompt binding"
        else
            validateLease sessionId observedAgent model

    let private validateBaseAgent (sessionId: SessionId) baseAgent observedAgent model =
        if baseAgent = observedAgent then
            validateLease sessionId observedAgent model
        else
            Error(sprintf "PROMPT-006: provider agent drift (%s -> %s)" baseAgent observedAgent)

    let validateObservedProvider (sessionId: SessionId) (agent: string) (model: OpencodeModel) : Result<bool, string> =
        let observedAgent = if isNull agent then "" else agent.Trim()

        match providerExpectation sessionId with
        | ProviderExpectation.ExactAttempt expected -> validateExactAttempt sessionId expected observedAgent model
        | ProviderExpectation.BaseAgent baseAgent -> validateBaseAgent sessionId baseAgent observedAgent model
        | ProviderExpectation.MissingParentBinding ->
            Error "PROMPT-006: parented provider run has no frozen agent binding"
        | ProviderExpectation.Unbound -> Ok false

    let effectiveAgent (sessionId: SessionId) (opts: OpenCodePromptOptions) : Result<string, string> =
        let baseAgent = tryAgent sessionId |> Option.bind nonEmpty
        let requested = opts.Agent |> Option.bind nonEmpty

        match opts.BindingIntent, baseAgent, requested with
        | SessionBindingIntent.Preserve, None, _ -> Error "PROMPT-006: session has no frozen/observed agent binding"
        | SessionBindingIntent.Preserve, Some agent, Some requested when requested <> agent ->
            Error(sprintf "PROMPT-006: preserve agent drift (%s -> %s)" agent requested)
        | SessionBindingIntent.Preserve, Some agent, _ -> Ok agent
        | SessionBindingIntent.ExplicitExecutionOverride, _, Some agent -> Ok agent
        | SessionBindingIntent.ExplicitExecutionOverride, _, None ->
            Error "PROMPT-006: execution override requires an explicit managed agent"

    let private requireRoutedModel (sessionId: SessionId) (agent: string) (opts: OpenCodePromptOptions) =
        match opts.Model, ModelRouting.tryLease sessionId agent with
        | None, _ -> Error(sprintf "PROMPT-006: routed send for '%s' has no explicit model" agent)
        | Some _, None -> Error(sprintf "PROMPT-006: routed send for '%s' has no model-routing lease" agent)
        | Some requested, Some target when ModelRouting.sameTarget target requested ->
            Ok
                { opts with
                    Agent = Some agent
                    Model = Some requested }
        | Some _, Some _ -> Error(sprintf "PROMPT-006: routed send for '%s' does not match its model lease" agent)

    let private validateExecutionIntent label baseAgent intent agent : Result<unit, string> =
        match intent with
        | SessionBindingIntent.Preserve when agent <> baseAgent ->
            Error(sprintf "PROMPT-006: %s agent drift (%s -> %s)" label baseAgent agent)
        | SessionBindingIntent.ExplicitExecutionOverride when
            agent <> baseAgent && not (tryPeerName baseAgent |> Option.exists ((=) agent))
            ->
            Error(sprintf "PROMPT-006: execution override is not the peer of '%s': %s" baseAgent agent)
        | _ -> Ok()

    let private normalizeForBaseAgent label (sessionId: SessionId) (baseAgent: string) (opts: OpenCodePromptOptions) =
        result {
            let! agent = effectiveAgent sessionId opts
            do! validateExecutionIntent label baseAgent opts.BindingIntent agent
            return! requireRoutedModel sessionId agent opts
        }

    let private requireBaseAgent error sessionId =
        tryAgent sessionId |> Option.bind nonEmpty |> Result.requireSome error

    let normalizeManagedPrompt (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        result {
            let! baseAgent = requireBaseAgent "PROMPT-006: parented session has no frozen agent binding" sessionId
            return! normalizeForBaseAgent "parented session" sessionId baseAgent opts
        }

    let normalizeUserFacingPrompt (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        result {
            let! baseAgent =
                requireBaseAgent "PROMPT-006: user-facing session has no observed user binding" sessionId

            return! normalizeForBaseAgent "user-facing session" sessionId baseAgent opts
        }
