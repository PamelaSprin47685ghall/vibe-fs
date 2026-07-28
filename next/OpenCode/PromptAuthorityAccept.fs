namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact

module PromptAuthorityAccept =

    let acceptHumanRoot
        (runtimeId: string)
        (persist: SessionId -> TurnId option -> AgentFact -> Result<unit, string>)
        (update: (PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptAuthorityProjection) -> unit)
        (read: (PromptAuthority.PromptAuthorityProjection -> Result<PromptAuthority.AuthorityExecutionProfile, string>) -> Result<PromptAuthority.AuthorityExecutionProfile, string>)
        (sessionId: SessionId)
        (messageId: MessageId)
        (explicitAgent: string option)
        (explicitModel: OpencodeModel option)
        (explicitVariant: string option)
        (hostAgent: string option)
        (hostModel: OpencodeModel option)
        (hostVariant: string option)
        : Result<PromptAuthority.AuthorityExecutionProfile, string> =
        let built =
            read (fun auth ->
                let last = auth.LastAuthorityProfile

                let agent =
                    explicitAgent
                    |> Option.orElse (last |> Option.map (fun p -> p.Agent))
                    |> Option.orElse hostAgent

                match agent with
                | None -> Error "HumanRoot requires agent from explicit input, LastAuthority, or host default"
                | Some agentValue ->
                    let model =
                        explicitModel
                        |> Option.orElse (last |> Option.bind (fun p -> p.BaseModel))
                        |> Option.orElse hostModel

                    let variant =
                        explicitVariant
                        |> Option.orElse (last |> Option.bind (fun p -> p.Variant))
                        |> Option.orElse hostVariant

                    Ok(
                        PromptAuthority.createAuthorityRoot
                            runtimeId
                            sessionId
                            PromptAuthority.HumanRoot
                            messageId
                            agentValue
                            model
                            variant
                    ))

        match built with
        | Error e -> Error e
        | Ok profile ->
            update (PromptAuthority.registerAuthority profile)

            match
                persist
                    sessionId
                    (Some(TurnId.ofMessageId messageId))
                    (AgentFact.AuthorityRootAccepted
                        {| SessionId = sessionId
                           LogicalRunId = profile.LogicalRunId
                           HostMessageId = MessageId.value messageId
                           AuthorityKind = "HumanRoot"
                           Agent = profile.Agent
                           BaseProviderID = profile.BaseModel |> Option.map (fun m -> m.providerID)
                           BaseModelID = profile.BaseModel |> Option.map (fun m -> m.modelID)
                           Variant = profile.Variant |})
            with
            | Error e -> Error e
            | Ok() -> Ok profile

    let acceptAgentOwnerRoot
        (runtimeId: string)
        (persist: SessionId -> TurnId option -> AgentFact -> Result<unit, string>)
        (update: (PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptAuthorityProjection) -> unit)
        (read: (PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptClaim option) -> PromptAuthority.PromptClaim option)
        (promptKey: string)
        (sessionId: SessionId)
        (hostMessageId: MessageId)
        : Result<PromptAuthority.AuthorityExecutionProfile, string> =
        let key = PromptKeyRef.create promptKey

        match read (fun auth -> Map.tryFind key auth.PendingClaims) with
        | None -> Error(sprintf "Unknown AgentOwnerRoot claim: %s" promptKey)
        | Some claim ->
            match claim.Origin, claim.Agent with
            | PromptAuthority.AuthorityRoot PromptAuthority.AgentOwnerRoot, Some agent ->
                let profile =
                    PromptAuthority.createAuthorityRoot
                        runtimeId
                        sessionId
                        PromptAuthority.AgentOwnerRoot
                        hostMessageId
                        agent
                        claim.EffectiveModel
                        claim.Variant

                match
                    persist
                        sessionId
                        None
                        (AgentFact.PluginPromptAccepted
                            {| PromptKey = promptKey
                               SessionId = sessionId
                               HostMessageId = MessageId.value hostMessageId |})
                with
                | Error error -> Error error
                | Ok() ->
                    match
                        persist
                            sessionId
                            (Some(TurnId.ofMessageId hostMessageId))
                            (AgentFact.AuthorityRootAccepted
                                {| SessionId = sessionId
                                   LogicalRunId = profile.LogicalRunId
                                   HostMessageId = MessageId.value hostMessageId
                                   AuthorityKind = "AgentOwnerRoot"
                                   Agent = agent
                                   BaseProviderID = profile.BaseModel |> Option.map (fun m -> m.providerID)
                                   BaseModelID = profile.BaseModel |> Option.map (fun m -> m.modelID)
                                   Variant = profile.Variant |})
                    with
                    | Error error -> Error error
                    | Ok() ->
                        update (fun auth ->
                            auth
                            |> PromptAuthority.acceptClaim key hostMessageId
                            |> PromptAuthority.registerAuthority profile)

                        Ok profile
            | _ -> Error(sprintf "PromptKey %s is not a pending AgentOwnerRoot" promptKey)

    let acceptContinuation
        (persist: SessionId -> TurnId option -> AgentFact -> Result<unit, string>)
        (update: (PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptAuthorityProjection) -> unit)
        (read: (PromptAuthority.PromptAuthorityProjection -> PromptAuthority.ContinuationKind option) -> PromptAuthority.ContinuationKind option)
        (promptKey: string)
        (sessionId: SessionId)
        (hostMessageId: MessageId)
        : Result<PromptAuthority.ContinuationKind option, string> =
        let key = PromptKeyRef.create promptKey

        match
            persist
                sessionId
                None
                (AgentFact.PluginPromptAccepted
                    {| PromptKey = promptKey
                       SessionId = sessionId
                       HostMessageId = MessageId.value hostMessageId |})
        with
        | Error error -> Error error
        | Ok() ->
            let kind =
                read (fun auth ->
                    match Map.tryFind key auth.PendingClaims with
                    | Some { Origin = PromptAuthority.Continuation c } -> Some c
                    | _ -> None)

            update (PromptAuthority.acceptClaim key hostMessageId)
            Ok kind
