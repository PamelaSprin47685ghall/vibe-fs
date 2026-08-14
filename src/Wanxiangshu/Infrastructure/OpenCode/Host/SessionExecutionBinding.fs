namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

module SessionExecutionBinding =
    type private ExpectedBinding = { Agent: string; Model: OpencodeModel }

    let private gate = obj ()
    let private parents = Dictionary<string, string>()
    let private agents = Dictionary<string, string>()
    let private models = Dictionary<string, OpencodeModel>()
    let private internalBindings = Dictionary<string, ExpectedBinding list>()

    let bind (parentId: SessionId) (childId: SessionId) (agent: string option) =
        lock gate (fun () ->
            let childKey = SessionId.value childId
            let parentKey = SessionId.value parentId

            match parents.TryGetValue childKey with
            | true, existing when existing <> parentKey ->
                invalidOp (sprintf "PROMPT-006: parented session '%s' changed parent" childKey)
            | _ -> parents.[childKey] <- parentKey

            match agent with
            | Some value when not (String.IsNullOrWhiteSpace value) ->
                let proposed = value.Trim()

                match agents.TryGetValue childKey with
                | true, existing when existing <> proposed ->
                    invalidOp (
                        sprintf "PROMPT-006: parented session '%s' agent changed (%s -> %s)" childKey existing proposed
                    )
                | _ -> agents.[childKey] <- proposed
            | _ -> ())

    let restore (parentId: SessionId) (childId: SessionId) (agent: string option) = bind parentId childId agent

    /// Fission physical lane: preserve a managed execution binding without
    /// declaring the lane a managed child of the logical owner. The Host parent
    /// edge is handled separately by CreateSiblingSession.
    let bindInternalRoot (sessionId: SessionId) (agent: string option) =
        match agent |> Option.bind (fun value -> if String.IsNullOrWhiteSpace value then None else Some(value.Trim())) with
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

    let beginInternalSend (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        match opts.Agent, opts.Model with
        | Some agent, Some model when not (String.IsNullOrWhiteSpace agent) ->
            lock gate (fun () ->
                let key = SessionId.value sessionId

                let previous =
                    match internalBindings.TryGetValue key with
                    | true, value -> value
                    | false, _ -> []

                internalBindings.[key] <- { Agent = agent.Trim(); Model = model } :: previous)
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
        && match left.variant, right.variant with
           | Some lv, Some rv when not (String.IsNullOrWhiteSpace lv) && not (String.IsNullOrWhiteSpace rv) ->
               lv.Trim() = rv.Trim()
           | _ -> true

    let requiresProviderBindingProof (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            internalBindings.ContainsKey key || parents.ContainsKey key)

    let validateObservedProvider (sessionId: SessionId) (agent: string) (model: OpencodeModel) : Result<bool, string> =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match internalBindings.TryGetValue key with
            | true, current :: _ when current.Agent = agent.Trim() && sameModel current.Model model -> Ok true
            | _ when parents.ContainsKey key ->
                match agents.TryGetValue key, models.TryGetValue key with
                | (true, baseAgent), (true, baseModel) ->
                    if baseAgent = agent.Trim() then
                        if sameModel baseModel model then
                            Ok true
                        else
                            Error(
                                sprintf
                                    "PROMPT-006: provider model drift (%s/%s -> %s/%s)"
                                    baseModel.providerID
                                    baseModel.modelID
                                    model.providerID
                                    model.modelID
                            )
                    else
                        match tryPeerName baseAgent with
                        | Some peer when peer = agent.Trim() ->
                            match configuredModel peer with
                            | Some peerModel when sameModel peerModel model -> Ok true
                            | Some peerModel ->
                                Error(
                                    sprintf
                                        "PROMPT-006: provider model drift (%s/%s -> %s/%s)"
                                        peerModel.providerID
                                        peerModel.modelID
                                        model.providerID
                                        model.modelID
                                )
                            | None -> Error(sprintf "PROMPT-006: peer agent '%s' has no configured model binding" peer)
                        | _ -> Error(sprintf "PROMPT-006: provider agent drift (%s -> %s)" baseAgent agent)
                | _ -> Error "PROMPT-006: parented provider run has no frozen agent/model binding"
            | _ -> Ok false)

    let private normalizeOverride (opts: OpenCodePromptOptions) =
        match opts.Agent |> Option.bind nonEmpty with
        | None -> Error "PROMPT-006: execution override requires an explicit managed agent"
        | Some agent ->
            let configured = configuredModel agent

            match configured, opts.Model with
            | Some bound, Some requested when not (sameModel bound requested) ->
                Error(sprintf "PROMPT-006: execution override model does not match agent '%s' binding" agent)
            | _ ->
                match configured |> Option.orElse opts.Model with
                | None -> Error(sprintf "PROMPT-006: execution override for '%s' has no provable model" agent)
                | Some model ->
                    Ok
                        { opts with
                            Agent = Some agent
                            Model = Some model }

    let private preserveBinding label baseAgent baseModel (opts: OpenCodePromptOptions) =
        let requestedAgent = opts.Agent |> Option.bind nonEmpty

        if requestedAgent <> Some baseAgent then
            Error(
                sprintf
                    "PROMPT-006: %s agent drift (%s -> %s)"
                    label
                    baseAgent
                    (requestedAgent |> Option.defaultValue "<missing>")
            )
        else
            match baseModel with
            | None -> Error(sprintf "PROMPT-006: %s '%s' has no provable model binding" label baseAgent)
            | Some model ->
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
                | _ ->
                    Ok
                        { opts with
                            Agent = Some baseAgent
                            Model = Some model }

    let normalizeManagedPrompt (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        match tryAgent sessionId |> Option.bind nonEmpty with
        | None -> Error "PROMPT-006: parented session has no frozen agent binding"
        | Some baseAgent ->
            match opts.BindingIntent with
            | SessionBindingIntent.Preserve ->
                let model =
                    tryModel sessionId
                    |> Option.orElseWith (fun () -> configuredModel baseAgent)
                    |> Option.orElse opts.Model

                preserveBinding "parented session" baseAgent model opts
                |> Result.map (fun normalized ->
                    model |> Option.iter (rememberModel sessionId)
                    normalized)
            | SessionBindingIntent.ExplicitExecutionOverride ->
                match tryModel sessionId |> Option.orElseWith (fun () -> configuredModel baseAgent) with
                | None -> Error "PROMPT-006: parented session has no provable base model binding"
                | Some baseModel ->
                    rememberModel sessionId baseModel
                    normalizeOverride opts

    let normalizeUserFacingPrompt (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        match tryAgent sessionId with
        | Some baseAgent ->
            let baseModel = tryModel sessionId |> Option.orElseWith (fun () -> configuredModel baseAgent)

            match baseModel with
            | None -> Error "PROMPT-006: user-facing session has no provable model binding"
            | Some model ->
                rememberModel sessionId model

                match opts.BindingIntent with
                | SessionBindingIntent.Preserve -> preserveBinding "user-facing session" baseAgent (Some model) opts
                | SessionBindingIntent.ExplicitExecutionOverride -> normalizeOverride opts
        | None -> Error "PROMPT-006: user-facing session has no observed user binding"
