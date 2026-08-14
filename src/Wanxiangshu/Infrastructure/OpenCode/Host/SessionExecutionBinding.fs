namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
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

    let private sameModel (left: OpencodeModel) (right: OpencodeModel) =
        left.providerID = right.providerID
        && left.modelID = right.modelID
        && left.variant = right.variant

    let requiresProviderBindingProof (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            internalBindings.ContainsKey key || parents.ContainsKey key)

    let validateObservedProvider (sessionId: SessionId) (agent: string) (model: OpencodeModel) : Result<bool, string> =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            let expected =
                match internalBindings.TryGetValue key with
                | true, current :: _ -> Ok(Some current)
                | _ when parents.ContainsKey key ->
                    match agents.TryGetValue key, models.TryGetValue key with
                    | (true, baseAgent), (true, baseModel) -> Ok(Some { Agent = baseAgent; Model = baseModel })
                    | _ -> Error "PROMPT-006: parented provider run has no frozen agent/model binding"
                | _ -> Ok None

            expected
            |> Result.bind (function
                | None -> Ok false
                | Some binding when binding.Agent <> agent.Trim() ->
                    Error(sprintf "PROMPT-006: provider agent drift (%s -> %s)" binding.Agent agent)
                | Some binding when not (sameModel binding.Model model) ->
                    Error(
                        sprintf
                            "PROMPT-006: provider model drift (%s/%s -> %s/%s)"
                            binding.Model.providerID
                            binding.Model.modelID
                            model.providerID
                            model.modelID
                    )
                | Some _ -> Ok true))

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
        match tryAgent sessionId, tryModel sessionId with
        | Some baseAgent, Some baseModel ->
            match opts.BindingIntent with
            | SessionBindingIntent.Preserve -> preserveBinding "user-facing session" baseAgent (Some baseModel) opts
            | SessionBindingIntent.ExplicitExecutionOverride -> normalizeOverride opts
        | _ -> Error "PROMPT-006: user-facing session has no observed user binding"
