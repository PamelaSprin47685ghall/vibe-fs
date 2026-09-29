namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona
open Wanxiangshu.OpenCode.ProviderWireDecode
open Wanxiangshu.OpenCode.ProviderWireCapture
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

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
    // DSL-MUTABLE: resource — public Host-created auxiliary child identities.
    let private hostAuxiliaryChildren = HashSet<string>()
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
    // DSL-MUTABLE: resource — session persistent model binding for fixed roles (DevOps)
    let private persistentDevOpsModels = Dictionary<string, OpencodeModel>()
    // DSL-MUTABLE: single-flight — durable child evidence installed at Load Phase.
    // The in-process maps above are a cache of what this process currently drives;
    // the durable handle projection is the existence truth across a restart.
    let mutable private durableChildEvidence: (string -> (string * string) option) option =
        None

    /// Install the durable resolver: sessionId -> (parentSessionId, agent). Called
    /// once at plugin load; reads fall through to it and cache the answer locally.
    let installDurableChildEvidence (resolve: string -> (string * string) option) = durableChildEvidence <- Some resolve

    let private durableParentOf (sessionKey: string) : string option =
        durableChildEvidence
        |> Option.bind (fun resolve -> resolve sessionKey)
        |> Option.map (fun (parent, _) ->
            if String.IsNullOrWhiteSpace parent then
                None
            else
                Some parent)
        |> Option.flatten

    let private nonEmpty (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Some(value.Trim())

    let private sameModel (left: OpencodeModel) (right: OpencodeModel) =
        let sameProvider =
            String.Equals(left.providerID.Trim(), right.providerID.Trim(), StringComparison.OrdinalIgnoreCase)

        let sameModelId =
            String.Equals(left.modelID.Trim(), right.modelID.Trim(), StringComparison.OrdinalIgnoreCase)

        let leftVar = left.variant |> Option.bind nonEmpty
        let rightVar = right.variant |> Option.bind nonEmpty

        let sameVariant =
            match leftVar, rightVar with
            | None, None -> true
            | Some l, Some r -> String.Equals(l, r, StringComparison.OrdinalIgnoreCase)
            | _ -> false

        sameProvider && sameModelId && sameVariant


    let private modelText (model: OpencodeModel) =
        sprintf "%s/%s[%s]" model.providerID model.modelID (model.variant |> Option.defaultValue "<missing>")

    let private roleOfParticipant (participant: string) : Role option =
        match ManagedAgent.tryParse participant with
        | Some managed -> Some managed.Role
        | None -> Roles.tryParseRole (participant.Trim())

    let private verifyDevOpsModelLocked (sessionKey: string) (model: OpencodeModel) : unit =
        match
            ModelRouting.boundDevopsModel (SessionId.create sessionKey), persistentDevOpsModels.TryGetValue sessionKey
        with
        | Some routingTarget, _ when sameModel routingTarget model -> persistentDevOpsModels.[sessionKey] <- model
        | _, (true, expected) when not (sameModel expected model) ->
            invalidOp (
                sprintf
                    "CRASH-020: DevOps model drift prohibited during resume/recovery (%s -> %s)"
                    (modelText expected)
                    (modelText model)
            )
        | _ -> ()

    let private bindDevOpsModelLocked (sessionKey: string) (model: OpencodeModel) : unit =
        verifyDevOpsModelLocked sessionKey model
        persistentDevOpsModels.[sessionKey] <- model

    let bindDevOpsModel (sessionId: SessionId) (model: OpencodeModel) : unit =
        lock gate (fun () -> bindDevOpsModelLocked (SessionId.value sessionId) model)

    let verifyDevOpsModel (sessionId: SessionId) (model: OpencodeModel) : unit =
        lock gate (fun () -> verifyDevOpsModelLocked (SessionId.value sessionId) model)

    let private promptBindingKey (sessionId: SessionId) (promptKey: PromptKey) =
        SessionId.value sessionId + "\u001f" + PromptKey.value promptKey

    let private clearAcceptedPromptBindingsForSession sessionKey =
        acceptedPromptBindings.Keys
        |> Seq.filter (fun bindingKey -> bindingKey.StartsWith(sessionKey + "\u001f", StringComparison.Ordinal))
        |> Seq.toArray
        |> Array.iter (fun bindingKey -> acceptedPromptBindings.Remove bindingKey |> ignore)

    let private matchingAcceptedPromptKeys sessionKey physicalUserMessageId =
        acceptedPromptBindings
        |> Seq.choose (fun entry ->
            if
                entry.Key.StartsWith(sessionKey + "\u001f", StringComparison.Ordinal)
                && entry.Value.PhysicalUserMessageId = physicalUserMessageId
            then
                Some entry.Key
            else
                None)
        |> Seq.toArray

    let exactExecutionBindingCount (sessionId: SessionId) (physicalUserMessageId: PhysicalUserMessageId) : int =
        lock gate (fun () ->
            let sessionKey = SessionId.value sessionId

            let acceptedCount =
                matchingAcceptedPromptKeys sessionKey physicalUserMessageId |> Array.length

            let providerCount =
                match providerAttemptBindings.TryGetValue sessionKey with
                | true, binding when binding.PhysicalUserMessageId = physicalUserMessageId -> 1
                | _ -> 0

            if acceptedCount > 0 || providerCount > 0 then 1 else 0)

    let releaseAcceptedExecution (sessionId: SessionId) (physicalUserMessageId: PhysicalUserMessageId) : unit =
        lock gate (fun () ->
            let sessionKey = SessionId.value sessionId

            matchingAcceptedPromptKeys sessionKey physicalUserMessageId
            |> Array.iter (fun bindingKey -> acceptedPromptBindings.Remove bindingKey |> ignore)

            match providerAttemptBindings.TryGetValue sessionKey with
            | true, binding when binding.PhysicalUserMessageId = physicalUserMessageId ->
                providerAttemptBindings.Remove sessionKey |> ignore
            | _ -> ())

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

    let bind (parentId: SessionId) (childId: SessionId) (agent: string option) =
        let privateHostChild =
            agent
            |> Option.bind nonEmpty
            |> Option.exists ManagedAgentCatalog.isBookkeeperName

        lock gate (fun () ->
            let childKey = SessionId.value childId
            internalRoots.Remove childKey |> ignore
            rememberParent childKey (SessionId.value parentId)

            match privateHostChild, agent |> Option.bind nonEmpty with
            | true, _ ->
                agents.Remove childKey |> ignore
                hostAuxiliaryChildren.Add childKey |> ignore
            | false, Some proposed -> rememberAgent childKey proposed
            | false, None -> ())


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

    let private tryResolveDurableParent (key: string) : SessionId option =
        match durableParentOf key with
        | Some parent ->
            parents.[key] <- parent
            Some(SessionId.create parent)
        | None -> None

    let private tryResolveDurableAgent (key: string) : string option =
        match durableChildEvidence |> Option.bind (fun resolve -> resolve key) with
        | Some(_, agent) when not (String.IsNullOrWhiteSpace agent) ->
            agents.[key] <- agent
            Some agent
        | _ -> None

    let tryParent (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match parents.TryGetValue key with
            | true, value -> Some(SessionId.create value)
            | false, _ -> tryResolveDurableParent key)

    let tryAgent (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match agents.TryGetValue key with
            | true, value -> Some value
            | false, _ -> tryResolveDurableAgent key)

    /// A Host-owned auxiliary child (for example title generation) is observed from
    /// a public session.created parent edge but has no Wanxiangshu execution agent.
    /// Managed child binding writes its agent before provider admission.
    let isUnboundHostAuxiliaryChild (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            hostAuxiliaryChildren.Contains key && not (agents.ContainsKey key))

    let observeHostAuxiliaryChild (sessionId: SessionId) =
        lock gate (fun () -> hostAuxiliaryChildren.Add(SessionId.value sessionId) |> ignore)

    /// A real external managed user message observes the participant agent. Its model
    /// is deliberately ignored; model routing replaces that field from ModelRouting.
    let observeUserFacingAgent (sessionId: SessionId) (agent: string) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            if not (parents.ContainsKey key) && not (String.IsNullOrWhiteSpace agent) then
                agents.[key] <- agent.Trim())

    /// PROMPT-006: chat.message has physically accepted one plugin-owned prompt.
    /// The caller supplies the fixed participant from the accepted evidence/claim,
    /// not from the Host message. This survives SendPrompt returning, but is addressed
    /// by PromptKey and therefore cannot bless an unrelated later request.
    let private exactBinding physicalUserMessageId agent model =
        { PhysicalUserMessageId = physicalUserMessageId
          Agent = agent
          Model = model }

    let private rememberAndBindExternalExecution
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (agent: string)
        (model: OpencodeModel)
        =
        lock gate (fun () ->
            let sessionKey = SessionId.value sessionId
            rememberAgent sessionKey agent

            if roleOfParticipant agent = Some Role.DevOps then
                bindDevOpsModelLocked sessionKey model

            providerAttemptBindings.[sessionKey] <- exactBinding physicalUserMessageId agent model)

    let acceptExternalExecution
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (participant: string)
        (model: OpencodeModel)
        : unit =
        match nonEmpty participant with
        | None -> invalidOp "PROMPT-006: accepted external execution has no participant"
        | Some agent -> rememberAndBindExternalExecution sessionId physicalUserMessageId agent model

    let acceptPromptExecution
        (sessionId: SessionId)
        (promptKey: PromptKey)
        (physicalUserMessageId: PhysicalUserMessageId)
        (participant: string)
        (model: OpencodeModel)
        : unit =
        match nonEmpty participant with
        | None -> invalidOp "PROMPT-006: accepted plugin prompt has no participant"
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
            let bindingKey = promptBindingKey sessionId promptKey

            match acceptedPromptBindings.TryGetValue bindingKey, physicalUserMessageId with
            | (true, expected), Some physical when expected.PhysicalUserMessageId = physical -> Ok()
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

    let private rememberLeaseTarget sessionId physical agent roleOpt target =
        let model = ModelRouting.toOpenCodeModel target

        if roleOpt = Some Role.DevOps then
            verifyDevOpsModel sessionId model

        rememberExternalAttempt sessionId physical agent target

    let private bindExternalExecutionLease sessionId physical agent =
        let roleOpt = roleOfParticipant agent

        let leaseOpt =
            match roleOpt with
            | Some _ ->
                // P2a: read the exact committed lease; this boundary never
                // allocates, takes over a reservation, or re-issues a fence.
                ModelRouting.tryReadExecution
                    { SessionId = sessionId
                      PhysicalUserMessageId = physical }
            | None -> None

        match leaseOpt with
        | None ->
            Error(
                sprintf
                    "PROMPT-006: physical provider attempt %s has no model-routing execution lease"
                    (PhysicalUserMessageId.value physical)
            )
        | Some lease -> rememberLeaseTarget sessionId physical agent roleOpt lease.Identity.Target

    let private beginExternalProviderAttempt sessionId physicalUserMessageId =
        let sessionKey = SessionId.value sessionId

        match baseAgent sessionKey, physicalUserMessageId with
        | None, _ -> Ok()
        | Some _, None -> Error "PROMPT-006: managed provider attempt has no physical user message id"
        | Some agent, Some physical -> bindExternalExecutionLease sessionId physical agent

    let private fallbackExternalAttempt sessionId physicalUserMessageId (key: PromptKey) =
        match beginExternalProviderAttempt sessionId physicalUserMessageId with
        | Ok() -> Ok()
        | Error _ ->
            Error(
                sprintf
                    "PROMPT-006: provider attempt for PromptKey %s has no accepted execution binding"
                    (PromptKey.value key)
            )

    let private tryFallbackProviderAttempt sessionId physicalUserMessageId key (bindingResult: Result<unit, string>) =
        match bindingResult with
        | Ok() -> Ok()
        | Error mismatch when mismatch.Contains("changed physical user message") -> Error mismatch
        | Error _ -> fallbackExternalAttempt sessionId physicalUserMessageId key

    let beginProviderAttempt
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId option)
        (promptKey: PromptKey option)
        : Result<unit, string> =
        match promptKey with
        | None -> beginExternalProviderAttempt sessionId physicalUserMessageId
        | Some key ->
            useAcceptedPromptBinding sessionId physicalUserMessageId key
            |> tryFallbackProviderAttempt sessionId physicalUserMessageId key

    let currentProviderModel (sessionId: SessionId) : OpencodeModel option =
        lock gate (fun () ->
            match providerAttemptBindings.TryGetValue(SessionId.value sessionId) with
            | true, binding -> Some binding.Model
            | false, _ -> None)

    /// execution-model-routing-010: a managed tool body is downstream of the provider step that
    /// emitted its exact ProviderRunIdentity. Tool execution may synchronously
    /// wait for descendant provider work (for example output distillation), so
    /// carrying provider capacity across this boundary can deadlock the family.
    ///
    /// The physical execution identity is not present in HostToolContext; use the
    /// provider-attempt binding frozen by messages.transform. Missing ProviderRun
    /// on a bound managed attempt is unsafe because a later/stale tool call could
    /// otherwise release the wrong step.
    let endProviderStepAtToolBoundary
        (sessionId: SessionId)
        (providerRunId: ProviderRunIdentity option)
        : Result<unit, string> =
        let binding =
            lock gate (fun () ->
                match providerAttemptBindings.TryGetValue(SessionId.value sessionId) with
                | true, current -> Some current
                | false, _ -> None)

        match binding, providerRunId with
        | None, _ -> Ok()
        | Some _, None ->
            Error "EMR-010: managed tool execution has no exact ProviderRunIdentity for provider-step handoff"
        | Some current, Some providerRun ->
            ModelRouting.endProviderStep sessionId current.PhysicalUserMessageId providerRun
            Ok()

    let private deriveTransformRequestKey
        (sessionId: SessionId)
        (physical: PhysicalUserMessageId)
        (visibleRuns: Set<ProviderRunIdentity>)
        =
        let sortedRuns =
            visibleRuns
            |> Seq.map ProviderRunIdentity.value
            |> Seq.sort
            |> String.concat ","

        sprintf "%s:%s:%s" (SessionId.value sessionId) (PhysicalUserMessageId.value physical) sortedRuns

    let private providerBindingRequired (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            agents.ContainsKey key
            || (parents.ContainsKey key && not (hostAuxiliaryChildren.Contains key)))

    /// EMR-010 / host-boundary-008: the provider-step gate reads the exact
    /// committed lease for this physical message, never the session-current
    /// binding copy. A managed session whose physical message has no
    /// committed lease fails closed; an unmanaged session never enters the
    /// provider step.
    let private enterBoundProviderStep
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId option)
        (rawMessages: obj list)
        (requestKey: string option)
        : Task =
        match physicalUserMessageId with
        | None -> Task.FromResult(())
        | Some physical when not (providerBindingRequired sessionId) -> Task.FromResult(())
        | Some physical ->
            match
                ModelRouting.tryReadExecution
                    { SessionId = sessionId
                      PhysicalUserMessageId = physical }
            with
            | Some _ ->
                let visibleRuns = ProviderWireCapture.visibleProviderRuns rawMessages
                ModelRouting.enterProviderStep sessionId physical visibleRuns requestKey
            | None ->
                raise (
                    InvalidOperationException(
                        sprintf
                            "EMR-010: managed provider step for physical user message %s has no committed model-routing lease"
                            (PhysicalUserMessageId.value physical)
                    )
                )

    let private beginSessionPhysicalProviderAttempt
        (beginQuiescence: SessionId -> unit)
        (sessionId: SessionId)
        (outObj: obj)
        : Task =
        task {
            let rawMessages =
                ProviderWireDecode.rawArray (ProviderWireDecode.readField outObj "messages")

            let physicalUserMessageId = ProviderWireCapture.lastUserMessageId rawMessages
            beginQuiescence sessionId

            let requestKey =
                match physicalUserMessageId with
                | Some physical ->
                    let visibleRuns = ProviderWireCapture.visibleProviderRuns rawMessages
                    Some(deriveTransformRequestKey sessionId physical visibleRuns)
                | None -> None

            match
                beginProviderAttempt sessionId physicalUserMessageId (ProviderWireCapture.lastUserPromptKey rawMessages)
            with
            | Error error -> invalidOp error
            | Ok() -> do! enterBoundProviderStep sessionId physicalUserMessageId rawMessages requestKey
        }

    /// HOST-004: Begin a physical provider attempt for the transform boundary.
    /// Combines quiescence begin, execution binding, and model routing step entry.
    /// Domain decision: managed provider step must have physical user message id (execution-model-routing-010).
    let beginPhysicalProviderAttemptForTransform
        (beginQuiescence: SessionId -> unit)
        (projectionSessionIdOpt: string option)
        (outObj: obj)
        : Task<unit> =
        task {
            match projectionSessionIdOpt with
            | Some sid -> do! beginSessionPhysicalProviderAttempt beginQuiescence (SessionId.create sid) outObj
            | None -> return ()
        }

    let drop (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            parents.Remove key |> ignore
            internalRoots.Remove key |> ignore
            hostAuxiliaryChildren.Remove key |> ignore
            agents.Remove key |> ignore
            providerAttemptBindings.Remove key |> ignore
            persistentDevOpsModels.Remove key |> ignore

            clearAcceptedPromptBindingsForSession key)

        ModelRouting.releaseExecution sessionId |> ignore

    let cancelUnacquired (sessionId: SessionId) =
        ModelRouting.cancelUnacquiredExecution sessionId |> ignore

    let requiresProviderBindingProof (sessionId: SessionId) = providerBindingRequired sessionId

    let private validateRuntimeLease
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (agent: string)
        (model: OpencodeModel)
        =
        let roleOpt = roleOfParticipant agent

        let leaseOpt =
            match roleOpt with
            | Some role ->
                ModelRouting.tryLease sessionId physicalUserMessageId role agent ModelExecutionPurpose.Normal None
            | None -> None

        match leaseOpt with
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

    let private validateNonDriftAttempt sessionId expected observedAgent model =
        if roleOfParticipant observedAgent = Some Role.DevOps then
            verifyDevOpsModel sessionId model

        validateLease sessionId expected.PhysicalUserMessageId observedAgent model

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
            validateNonDriftAttempt sessionId expected observedAgent model

    let private validateManagedWithoutAttempt (sessionId: SessionId) (observedAgent: string) =
        match tryAgent sessionId with
        | Some expectedAgent when
            not (String.Equals(expectedAgent.Trim(), observedAgent, StringComparison.OrdinalIgnoreCase))
            ->
            Error(sprintf "PROMPT-006: provider agent drift (%s -> %s)" expectedAgent observedAgent)
        | _ -> Error "PROMPT-006: managed provider run has no exact physical execution binding"

    let validateObservedProvider (sessionId: SessionId) (agent: string) (model: OpencodeModel) : Result<bool, string> =
        let observedAgent = if isNull agent then "" else agent.Trim()

        match providerExpectation sessionId with
        | ProviderExpectation.ExactAttempt expected -> validateExactAttempt sessionId expected observedAgent model
        | ProviderExpectation.ManagedWithoutAttempt -> validateManagedWithoutAttempt sessionId observedAgent
        | ProviderExpectation.Unbound -> Ok false

    let participantAgent (sessionId: SessionId) (opts: OpenCodePromptOptions) : Result<string, string> =
        let baseAgent = tryAgent sessionId |> Option.bind nonEmpty
        let requested = opts.Agent |> Option.bind nonEmpty

        match opts.BindingIntent, baseAgent, requested with
        | SessionBindingIntent.Preserve, None, _ ->
            Error "PROMPT-006: session has no frozen/observed participant binding"
        | SessionBindingIntent.Preserve, Some agent, Some requested when requested <> agent ->
            Error(sprintf "PROMPT-006: preserve participant drift (%s -> %s)" agent requested)
        | SessionBindingIntent.Preserve, Some agent, _ -> Ok agent
        | SessionBindingIntent.ExplicitExecutionOverride, Some baseParticipant, Some requested when
            requested <> baseParticipant
            ->
            Error(
                sprintf
                    "PROMPT-006: explicit execution agent '%s' must equal authority participant '%s'"
                    requested
                    baseParticipant
            )
        | SessionBindingIntent.ExplicitExecutionOverride, Some baseParticipant, _ -> Ok baseParticipant
        | SessionBindingIntent.ExplicitExecutionOverride, None, Some agent -> Ok agent
        | SessionBindingIntent.ExplicitExecutionOverride, _, None ->
            Error "PROMPT-006: execution override requires an explicit managed participant"

    let private validateExecutionIntent label baseAgent intent agent : Result<unit, string> =
        match intent with
        | _ when agent <> baseAgent ->
            Error(sprintf "PROMPT-006: %s participant drift (%s -> %s)" label baseAgent agent)
        | _ -> Ok()

    let private prepareForBaseAgent label (sessionId: SessionId) (baseAgent: string) (opts: OpenCodePromptOptions) =
        result {
            let! agent = participantAgent sessionId opts
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
