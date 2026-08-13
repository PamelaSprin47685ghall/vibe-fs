namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Kernel.Identity

module SessionExecutionBinding =
    let private gate = obj ()
    let private parents = Dictionary<string, string>()
    let private agents = Dictionary<string, string>()
    let private models = Dictionary<string, OpencodeModel>()
    let private internalSends = Dictionary<string, int>()

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
            if not (models.ContainsKey key) then models.[key] <- model)

    let beginInternalSend (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            let count =
                match internalSends.TryGetValue key with
                | true, value -> value
                | false, _ -> 0
            internalSends.[key] <- count + 1)

    let endInternalSend (sessionId: SessionId) =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            match internalSends.TryGetValue key with
            | true, count when count > 1 -> internalSends.[key] <- count - 1
            | true, _ -> internalSends.Remove key |> ignore
            | false, _ -> ())

    let observeUserFacing (sessionId: SessionId) (agent: string) (model: OpencodeModel) =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            if
                not (internalSends.ContainsKey key)
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
            internalSends.Remove key |> ignore)

    let private nonEmpty (value: string) =
        if String.IsNullOrWhiteSpace value then None else Some(value.Trim())

    let private configuredModel agent = ManagedAgentConfig.tryBoundModel agent

    let private sameModel (left: OpencodeModel) (right: OpencodeModel) =
        left.providerID = right.providerID
        && left.modelID = right.modelID
        && left.variant = right.variant

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
                | Some model -> Ok { opts with Agent = Some agent; Model = Some model }

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
                | _ -> Ok { opts with Agent = Some baseAgent; Model = Some model }

    let normalizeManagedPrompt (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        match opts.BindingIntent with
        | SessionBindingIntent.ExplicitExecutionOverride -> normalizeOverride opts
        | SessionBindingIntent.Preserve ->
            match tryAgent sessionId |> Option.bind nonEmpty with
            | None -> Error "PROMPT-006: parented session has no frozen agent binding"
            | Some baseAgent ->
                let model =
                    tryModel sessionId
                    |> Option.orElseWith (fun () -> configuredModel baseAgent)
                    |> Option.orElse opts.Model

                preserveBinding "parented session" baseAgent model opts
                |> Result.map (fun normalized ->
                    model |> Option.iter (rememberModel sessionId)
                    normalized)

    let normalizeUserFacingPrompt (sessionId: SessionId) (opts: OpenCodePromptOptions) =
        match opts.BindingIntent with
        | SessionBindingIntent.ExplicitExecutionOverride -> normalizeOverride opts
        | SessionBindingIntent.Preserve ->
            match tryAgent sessionId, tryModel sessionId with
            | Some baseAgent, Some baseModel -> preserveBinding "user-facing session" baseAgent (Some baseModel) opts
            | _ -> Error "PROMPT-006: user-facing session has no observed user binding"
