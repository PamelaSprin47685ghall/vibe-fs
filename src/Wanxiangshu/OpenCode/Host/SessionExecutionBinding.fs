namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Persona
open Wanxiangshu.OpenCode.ProviderWireDecode
open Wanxiangshu.OpenCode.ProviderWireCapture

/// Process-local execution identity binding. Agent identity is frozen/observed here;
/// physical model authority lives in ModelRouting and is leased by the exact
/// (SessionId, PhysicalUserMessageId) provider execution.
module SessionExecutionBinding =

    type private ExpectedBinding =
        { PhysicalUserMessageId: PhysicalUserMessageId
          Agent: string
          Model: OpencodeModel }

    let private gate = obj ()
    // DSL-MUTABLE: resource — session parent binding map
    let private parents = Dictionary<string, string>()
    // DSL-MUTABLE: resource — session agent binding map
    let private agents = Dictionary<string, string>()
    // DSL-MUTABLE: resource — internal execution roots without logical parent bindings
    let private internalRoots = HashSet<string>()
    // Accepted plugin prompt execution identity awaiting the provider transform
    // that answers that exact PromptKey. Process-local only; restart
    // intentionally forgets it and therefore cannot resume old sends.
    // DSL-MUTABLE: resource — accepted prompt execution identity map.
    let private acceptedPromptBindings = Dictionary<string, ExpectedBinding>()
    // Binding frozen for the provider attempt currently being built by
    // experimental.chat.messages.transform. Replaced at every provider-attempt
    // boundary; never session authority.
    // DSL-MUTABLE: resource — provider attempt binding map.
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

    let private modelText (model: OpencodeModel) =
        sprintf "%s/%s[%s]" model.providerID model.modelID (model.variant |> Option.defaultValue "<missing>")

    let bind (parentId: SessionId) (childId: SessionId) (agent: string option) =
        lock gate (fun () ->
            let childKey = SessionId.value childId
            internalRoots.Remove childKey |> ignore
            rememberParent childKey (SessionId.value parentId)

            match agent |> Option.bind nonEmpty with
            | Some proposed -> rememberAgent childKey proposed
            | None -> ())

        ModelRouting.bindCapacityChild parentId childId

    let restore (parentId: SessionId) (childId: SessionId) (agent: string option) = bind parentId childId agent

    /// Fission physical lane: preserve a managed execution identity without declaring
    /// the lane a managed child of the logical owner.
    let bindInternalRoot (sessionId: SessionId) (agent: string option) =
        match agent |> Option.bind nonEmpty with
        | None -> invalidOp "PROMPT-006: internal root requires a managed agent binding"
        | Some selected ->
            lock gate (fun () ->
                let key = SessionId.value sessionId
                internalRoots.Add key |> ignore
                agents.[key] <- selected)

    let isInternalRoot (sessionId: SessionId) =
        lock gate (fun () -> internalRoots.Contains(SessionId.value sessionId))

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
    let private exactBinding physicalUserMessageId agent model =
        { PhysicalUserMessageId = physicalUserMessageId
          Agent = agent
          Model = model }

    let acceptExternalExecution
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (effectiveAgent: string)
        (model: OpencodeModel)
        : unit =
        match nonEmpty effectiveAgent with
        | None -> invalidOp "PROMPT-006: accepted external execution has no EffectiveAgent"
        | Some agent ->
            lock gate (fun () ->
                providerAttemptBindings.[SessionId.value sessionId] <- exactBinding physicalUserMessageId agent model)

    let acceptPromptExecution
        (sessionId: SessionId)
        (promptKey: PromptKey)
        (physicalUserMessageId: PhysicalUserMessageId)
        (effectiveAgent: string)
        (model: OpencodeModel)
        : unit =
        match nonEmpty effectiveAgent with
        | None -> invalidOp "PROMPT-006: accepted plugin prompt has no EffectiveAgent"
        | Some agent ->
            lock gate (fun () ->
                let sessionKey = SessionId.value sessionId
                let binding = exactBinding physicalUserMessageId agent model
                clearAcceptedPromptBindingsForSession sessionKey
                acceptedPromptBindings.[promptBindingKey sessionId promptKey] <- binding
                // chat.params is triggered before messages.transform. Keep the
                // same exact physical binding available immediately, then let the
                // transform re-prove it from its trailing user message.
                providerAttemptBindings.[sessionKey] <- binding)

    /// PROMPT-006 process-local execution capability commit. The routing owner
    /// publishes the typed route; this owner alone decides how an accepted route
    /// becomes provider-attempt binding state. The composition root projects the
    /// later-compiled dispatcher into the one predicate this earlier owner needs.
    let acceptRoutedExecution dispatchAccepted (routed: ModelRouting.RoutedChatExecution) : unit =
        match routed, dispatchAccepted with
        | ModelRouting.RoutedChatExecution.PluginManaged(claim, physical, agent, model), Some isAccepted ->
            if isAccepted claim then
                acceptPromptExecution claim.SessionId claim.PromptKey physical agent model
            else
                invalidOp (
                    sprintf
                        "PROMPT-006: PromptKey %s did not reach durable PhysicalAccepted"
                        (PromptKey.value claim.PromptKey)
                )
        | ModelRouting.RoutedChatExecution.PluginManaged _, None -> ()
        | ModelRouting.RoutedChatExecution.ExternalManaged(sessionId, physical, agent, model), _ ->
            acceptExternalExecution sessionId physical agent model
        | ModelRouting.RoutedChatExecution.NoRoute, _
        | ModelRouting.RoutedChatExecution.Superseded, _ -> ()

    /// Provider-attempt boundary. The transform must bind the exact trailing
    /// PhysicalUserMessageId that chat.message admitted. PromptKey is still the
    /// plugin authority identity, but it can never substitute for physical
    /// execution identity.
    let private clearProviderAttempt sessionKey =
        lock gate (fun () -> providerAttemptBindings.Remove sessionKey |> ignore)

    let private physicalMismatch promptKey expected observed =
        Error(
            sprintf
                "PROMPT-006: provider attempt for PromptKey %s changed physical user message (%s -> %s)"
                (PromptKey.value promptKey)
                (PhysicalUserMessageId.value expected)
                (PhysicalUserMessageId.value observed)
        )

    let private useAcceptedPromptBinding
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId option)
        (promptKey: PromptKey)
        : Result<unit, string> =
        lock gate (fun () ->
            let sessionKey = SessionId.value sessionId
            let bindingKey = promptBindingKey sessionId promptKey

            match acceptedPromptBindings.TryGetValue bindingKey, physicalUserMessageId with
            | (true, expected), Some physical when expected.PhysicalUserMessageId = physical ->
                providerAttemptBindings.[sessionKey] <- expected
                Ok()
            | (true, expected), Some physical -> physicalMismatch promptKey expected.PhysicalUserMessageId physical
            | (true, _), None ->
                Error(
                    sprintf
                        "PROMPT-006: provider attempt for PromptKey %s has no physical user message id"
                        (PromptKey.value promptKey)
                )
            | (false, _), _ ->
                Error(
                    sprintf
                        "PROMPT-006: provider attempt for PromptKey %s has no accepted execution binding"
                        (PromptKey.value promptKey)
                ))

    let private baseAgent sessionKey =
        lock gate (fun () ->
            clearAcceptedPromptBindingsForSession sessionKey

            match agents.TryGetValue sessionKey with
            | true, agent -> Some agent
            | false, _ -> None)

    let private rememberExternalAttempt sessionId physical agent target =
        let sessionKey = SessionId.value sessionId

        lock gate (fun () ->
            providerAttemptBindings.[sessionKey] <-
                { PhysicalUserMessageId = physical
                  Agent = agent
                  Model = ModelRouting.toOpenCodeModel target })

        Ok()

    let private bindExternalExecutionLease sessionId physical agent =
        match ModelRouting.tryLease sessionId physical agent with
        | None ->
            Error(
                sprintf
                    "PROMPT-006: physical provider attempt %s has no model-routing execution lease"
                    (PhysicalUserMessageId.value physical)
            )
        | Some target -> rememberExternalAttempt sessionId physical agent target

    let private beginExternalProviderAttempt sessionId physicalUserMessageId =
        let sessionKey = SessionId.value sessionId

        match baseAgent sessionKey, physicalUserMessageId with
        | None, _ -> Ok()
        | Some _, None -> Error "PROMPT-006: managed provider attempt has no physical user message id"
        | Some agent, Some physical -> bindExternalExecutionLease sessionId physical agent

    let beginProviderAttempt
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId option)
        (promptKey: PromptKey option)
        : Result<unit, string> =
        clearProviderAttempt (SessionId.value sessionId)

        match promptKey with
        | None -> beginExternalProviderAttempt sessionId physicalUserMessageId
        | Some key -> useAcceptedPromptBinding sessionId physicalUserMessageId key

    let currentProviderModel (sessionId: SessionId) : OpencodeModel option =
        lock gate (fun () ->
            match providerAttemptBindings.TryGetValue(SessionId.value sessionId) with
            | true, binding -> Some binding.Model
            | false, _ -> None)

    /// HOST-004: Begin a physical provider attempt for the transform boundary.
    /// Combines quiescence begin, execution binding, and model routing step entry.
    /// Domain decision: managed provider step must have physical user message id (EMR-010).
    let beginPhysicalProviderAttemptForTransform
        (beginQuiescence: SessionId -> unit)
        (sessionId: SessionId)
        (outObj: obj)
        : Task<unit> =
        task {
            let rawMessages =
                ProviderWireDecode.rawArray (ProviderWireDecode.readField outObj "messages")

            let physicalUserMessageId = ProviderWireCapture.lastUserMessageId rawMessages

            beginQuiescence sessionId

            match
                beginProviderAttempt
                    sessionId
                    physicalUserMessageId
                    (ProviderWireCapture.lastUserPromptKey rawMessages)
            with
            | Error error -> invalidOp error
            | Ok() ->
                match currentProviderModel sessionId, physicalUserMessageId with
                | Some _, Some physical ->
                    do!
                        ModelRouting.enterProviderStep
                            sessionId
                            physical
                            (ProviderWireCapture.visibleProviderRuns rawMessages)
                | Some _, None -> invalidOp "EMR-010: managed provider step has no physical user message id"
                | None, _ -> ()
        }

    let drop (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            parents.Remove key |> ignore
            internalRoots.Remove key |> ignore
            agents.Remove key |> ignore
            providerAttemptBindings.Remove key |> ignore

            clearAcceptedPromptBindingsForSession key)

        ModelRouting.releaseExecution sessionId
        ModelRouting.dropCapacityLineage sessionId

    let cancelUnacquired (sessionId: SessionId) =
        ModelRouting.cancelUnacquiredExecution sessionId

    let requiresProviderBindingProof (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            parents.ContainsKey key || agents.ContainsKey key)

    let private validateRuntimeLease
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (agent: string)
        (model: OpencodeModel)
        =
        match ModelRouting.tryLease sessionId physicalUserMessageId agent with
        | None ->
            Error(
                sprintf
                    "PROMPT-006: managed provider execution %s has no model-routing lease"
                    (PhysicalUserMessageId.value physicalUserMessageId)
            )
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

    let private validateLease
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (agent: string)
        (model: OpencodeModel)
        =
        if not (ModelRouting.hasRuntime ()) then
            Ok true
        else
            validateRuntimeLease sessionId physicalUserMessageId agent model

    [<RequireQualifiedAccess>]
    type private ProviderExpectation =
        | ExactAttempt of ExpectedBinding
        | ManagedWithoutAttempt
        | Unbound

    let private providerExpectation (sessionId: SessionId) : ProviderExpectation =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match providerAttemptBindings.TryGetValue key, parents.ContainsKey key || agents.ContainsKey key with
            | (true, expected), _ -> ProviderExpectation.ExactAttempt expected
            | (false, _), true -> ProviderExpectation.ManagedWithoutAttempt
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
            Error(
                sprintf
                    "PROMPT-006: provider model/reasoning drift from accepted prompt binding (%s -> %s)"
                    (modelText expected.Model)
                    (modelText model)
            )
        else
            validateLease sessionId expected.PhysicalUserMessageId observedAgent model

    let validateObservedProvider (sessionId: SessionId) (agent: string) (model: OpencodeModel) : Result<bool, string> =
        let observedAgent = if isNull agent then "" else agent.Trim()

        match providerExpectation sessionId with
        | ProviderExpectation.ExactAttempt expected -> validateExactAttempt sessionId expected observedAgent model
        | ProviderExpectation.ManagedWithoutAttempt ->
            Error "PROMPT-006: managed provider run has no exact physical execution binding"
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

    let private validateExecutionIntent label baseAgent intent agent : Result<unit, string> =
        match intent with
        | SessionBindingIntent.Preserve when agent <> baseAgent ->
            Error(sprintf "PROMPT-006: %s agent drift (%s -> %s)" label baseAgent agent)
        | SessionBindingIntent.ExplicitExecutionOverride when
            agent <> baseAgent && not (tryPeerName baseAgent |> Option.exists ((=) agent))
            ->
            Error(sprintf "PROMPT-006: execution override is not the peer of '%s': %s" baseAgent agent)
        | _ -> Ok()

    let private prepareForBaseAgent label (sessionId: SessionId) (baseAgent: string) (opts: OpenCodePromptOptions) =
        result {
            let! agent = effectiveAgent sessionId opts
            do! validateExecutionIntent label baseAgent opts.BindingIntent agent
            // EMR: dispatching a user message is not provider execution admission.
            // Model capacity is acquired exactly once later at chat.message, when
            // the Host is about to execute this physical user message. Keeping the
            // send model-free prevents fork/repair from waiting on a provider slot.
            return
                { opts with
                    Agent = Some agent
                    Model = None }
        }

    let private requireBaseAgent error sessionId =
        tryAgent sessionId |> Option.bind nonEmpty |> Result.requireSome error

    let prepareManagedPrompt (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        result {
            let! baseAgent = requireBaseAgent "PROMPT-006: parented session has no frozen agent binding" sessionId
            return! prepareForBaseAgent "parented session" sessionId baseAgent opts
        }

    let prepareUserFacingPrompt (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        result {
            let! baseAgent = requireBaseAgent "PROMPT-006: user-facing session has no observed user binding" sessionId

            return! prepareForBaseAgent "user-facing session" sessionId baseAgent opts
        }
