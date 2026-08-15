namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Persona

/// Process-local execution identity binding. Agent identity is frozen/observed here;
/// physical model authority lives in ModelRouting and is leased by (SessionId, EffectiveAgent).
module SessionExecutionBinding =

    type private ExpectedBinding = { Agent: string; Model: OpencodeModel }

    let private gate = obj ()
    let private parents = Dictionary<string, string>()
    let private agents = Dictionary<string, string>()
    let private internalBindings = Dictionary<string, ExpectedBinding list>()
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

    let private tryPeerName (agentName: string) : string option =
        if ManagedAgentCatalog.isBookkeeperName agentName then
            ManagedAgentCatalog.bookkeeperPeerName agentName
        else
            let trimmed = agentName.Trim()

            match trimmed.IndexOf '-' with
            | index when index > 0 && index < trimmed.Length - 1 ->
                let tierText = trimmed.Substring(0, index)
                let roleText = trimmed.Substring(index + 1)

                match ManagedAgentCatalog.tryParseTier tierText, ManagedAgentCatalog.tryParseRole roleText with
                | Some tier, Some role -> Some(ManagedAgentCatalog.peerNameOf tier role)
                | _ -> None
            | _ -> None

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

            if
                not (internalBindings.ContainsKey key)
                && not (parents.ContainsKey key)
                && not (String.IsNullOrWhiteSpace agent)
            then
                agents.[key] <- agent.Trim())

    let private pushInternalBinding (key: string) (agent: string) (model: OpencodeModel) =
        let previous =
            match internalBindings.TryGetValue key with
            | true, value -> value
            | false, _ -> []

        internalBindings.[key] <- { Agent = agent.Trim(); Model = model } :: previous

    let beginInternalSend (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        match opts.Agent, opts.Model with
        | Some agent, Some model when not (String.IsNullOrWhiteSpace agent) ->
            lock gate (fun () -> pushInternalBinding (SessionId.value sessionId) agent model)
        | _ -> invalidOp "PROMPT-006: internal send has no concrete agent/model binding"

    let endInternalSend (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match internalBindings.TryGetValue key with
            | true, _ :: remaining when not (List.isEmpty remaining) -> internalBindings.[key] <- remaining
            | true, _ -> internalBindings.Remove key |> ignore
            | false, _ -> ())

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
                acceptedPromptBindings.[promptBindingKey sessionId promptKey] <- { Agent = agent; Model = model })

    /// Provider-attempt boundary. A plugin-owned user message must consume the
    /// execution binding registered for its exact PromptKey. External user roots
    /// have no PromptKey and intentionally fall back to the session base binding.
    let beginProviderAttempt (sessionId: SessionId) (promptKey: PromptKey option) : Result<unit, string> =
        lock gate (fun () ->
            let sessionKey = SessionId.value sessionId
            providerAttemptBindings.Remove sessionKey |> ignore

            match promptKey with
            | None -> Ok()
            | Some key ->
                let bindingKey = promptBindingKey sessionId key

                match acceptedPromptBindings.TryGetValue bindingKey with
                | true, expected ->
                    acceptedPromptBindings.Remove bindingKey |> ignore
                    providerAttemptBindings.[sessionKey] <- expected
                    Ok()
                | false, _ ->
                    Error(
                        sprintf
                            "PROMPT-006: provider attempt for PromptKey %s has no accepted execution binding"
                            (PromptKey.value key)
                    ))

    let drop (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            parents.Remove key |> ignore
            agents.Remove key |> ignore
            internalBindings.Remove key |> ignore
            providerAttemptBindings.Remove key |> ignore

            acceptedPromptBindings.Keys
            |> Seq.filter (fun bindingKey -> bindingKey.StartsWith(key + "\u001f", StringComparison.Ordinal))
            |> Seq.toArray
            |> Array.iter (fun bindingKey -> acceptedPromptBindings.Remove bindingKey |> ignore))

        ModelRouting.releaseSession sessionId

    let cancelUnacquired (sessionId: SessionId) =
        ModelRouting.cancelUnacquiredSession sessionId

    let requiresProviderBindingProof (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            internalBindings.ContainsKey key
            || parents.ContainsKey key
            || agents.ContainsKey key)

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

    let validateObservedProvider (sessionId: SessionId) (agent: string) (model: OpencodeModel) : Result<bool, string> =
        let observedAgent = if isNull agent then "" else agent.Trim()

        lock gate (fun () ->
            let key = SessionId.value sessionId

            match internalBindings.TryGetValue key with
            | true, current :: _ ->
                if current.Agent <> observedAgent then
                    Error(sprintf "PROMPT-006: provider agent drift (%s -> %s)" current.Agent observedAgent)
                elif not (sameModel current.Model model) then
                    Error "PROMPT-006: provider model/reasoning drift from internal send binding"
                else
                    validateLease sessionId observedAgent model
            | _ ->
                match providerAttemptBindings.TryGetValue key with
                | true, expected ->
                    if expected.Agent <> observedAgent then
                        Error(sprintf "PROMPT-006: provider agent drift (%s -> %s)" expected.Agent observedAgent)
                    elif not (sameModel expected.Model model) then
                        Error "PROMPT-006: provider model/reasoning drift from accepted prompt binding"
                    else
                        validateLease sessionId observedAgent model
                | false, _ when parents.ContainsKey key ->
                    match agents.TryGetValue key with
                    | false, _ -> Error "PROMPT-006: parented provider run has no frozen agent binding"
                    | true, baseAgent ->
                        let allowed =
                            observedAgent = baseAgent
                            || (tryPeerName baseAgent |> Option.exists ((=) observedAgent))

                        if allowed then
                            validateLease sessionId observedAgent model
                        else
                            Error(sprintf "PROMPT-006: provider agent drift (%s -> %s)" baseAgent observedAgent)
                | false, _ ->
                    match agents.TryGetValue key with
                    | true, selected when selected = observedAgent -> validateLease sessionId observedAgent model
                    | true, selected ->
                        Error(sprintf "PROMPT-006: provider agent drift (%s -> %s)" selected observedAgent)
                    | false, _ -> Ok false)

    let effectiveAgent (sessionId: SessionId) (opts: OpenCodePromptOptions) : Result<string, string> =
        match opts.BindingIntent with
        | SessionBindingIntent.Preserve ->
            match tryAgent sessionId |> Option.bind nonEmpty with
            | None -> Error "PROMPT-006: session has no frozen/observed agent binding"
            | Some agent ->
                match opts.Agent |> Option.bind nonEmpty with
                | Some requested when requested <> agent ->
                    Error(sprintf "PROMPT-006: preserve agent drift (%s -> %s)" agent requested)
                | _ -> Ok agent
        | SessionBindingIntent.ExplicitExecutionOverride ->
            match opts.Agent |> Option.bind nonEmpty with
            | Some agent -> Ok agent
            | None -> Error "PROMPT-006: execution override requires an explicit managed agent"

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

    let private normalizeForBaseAgent label (sessionId: SessionId) (baseAgent: string) (opts: OpenCodePromptOptions) =
        match effectiveAgent sessionId opts with
        | Error error -> Error error
        | Ok agent ->
            match opts.BindingIntent with
            | SessionBindingIntent.Preserve when agent <> baseAgent ->
                Error(sprintf "PROMPT-006: %s agent drift (%s -> %s)" label baseAgent agent)
            | SessionBindingIntent.ExplicitExecutionOverride when
                agent <> baseAgent && not (tryPeerName baseAgent |> Option.exists ((=) agent))
                ->
                Error(sprintf "PROMPT-006: execution override is not the peer of '%s': %s" baseAgent agent)
            | _ -> requireRoutedModel sessionId agent opts

    let normalizeManagedPrompt (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        match tryAgent sessionId |> Option.bind nonEmpty with
        | None -> Error "PROMPT-006: parented session has no frozen agent binding"
        | Some baseAgent -> normalizeForBaseAgent "parented session" sessionId baseAgent opts

    let normalizeUserFacingPrompt (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        match tryAgent sessionId |> Option.bind nonEmpty with
        | None -> Error "PROMPT-006: user-facing session has no observed user binding"
        | Some baseAgent -> normalizeForBaseAgent "user-facing session" sessionId baseAgent opts
