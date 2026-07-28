namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact

module PromptAuthorityAccept =

    let private persistAuthorityRoot
        (persist: SessionId -> TurnId option -> AgentFact -> Result<unit, string>)
        (sessionId: SessionId)
        (messageId: MessageId)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (authorityKind: string)
        =
        persist
            sessionId
            (Some(TurnId.ofMessageId messageId))
            (AgentFact.AuthorityRootAccepted
                {| SessionId = sessionId
                   LogicalRunId = profile.LogicalRunId
                   HostMessageId = MessageId.value messageId
                   AuthorityKind = authorityKind
                   SelectedAgent = profile.SelectedAgent
                   PeerAgent = profile.PeerAgent
                   CanonicalRole = PromptAuthority.roleLabel profile.CanonicalRole
                   SelectedTier = PromptAuthority.tierLabel profile.SelectedTier |})

    let acceptHumanRoot
        (runtimeId: string)
        (persist: SessionId -> TurnId option -> AgentFact -> Result<unit, string>)
        (update: (PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptAuthorityProjection) -> unit)
        (read:
            (PromptAuthority.PromptAuthorityProjection -> Result<PromptAuthority.AuthorityExecutionProfile, string>)
                -> Result<PromptAuthority.AuthorityExecutionProfile, string>)
        (sessionId: SessionId)
        (messageId: MessageId)
        (explicitAgent: string option)
        (hostAgent: string option)
        : Result<PromptAuthority.AuthorityExecutionProfile, string> =
        let built =
            read (fun auth ->
                let last = auth.LastAuthorityProfile

                let agent =
                    explicitAgent
                    |> Option.orElse (last |> Option.map (fun p -> p.SelectedAgent))
                    |> Option.orElse hostAgent

                match agent with
                | None ->
                    Error
                        "HumanRoot requires explicit managed agent (fast-* / deep-*) from input, LastAuthority, or host default"
                | Some agentValue ->
                    PromptAuthority.createAuthorityRoot
                        runtimeId
                        sessionId
                        PromptAuthority.HumanRoot
                        messageId
                        agentValue)

        match built with
        | Error e -> Error e
        | Ok profile ->
            update (PromptAuthority.registerAuthority profile)

            match persistAuthorityRoot persist sessionId messageId profile "HumanRoot" with
            | Error e -> Error e
            | Ok() -> Ok profile

    let acceptAgentOwnerRoot
        (runtimeId: string)
        (persist: SessionId -> TurnId option -> AgentFact -> Result<unit, string>)
        (update: (PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptAuthorityProjection) -> unit)
        (read:
            (PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptClaim option)
                -> PromptAuthority.PromptClaim option)
        (promptKey: string)
        (sessionId: SessionId)
        (hostMessageId: MessageId)
        : Result<PromptAuthority.AuthorityExecutionProfile, string> =
        let key = PromptKeyRef.create promptKey

        match read (fun auth -> Map.tryFind key auth.PendingClaims) with
        | None -> Error(sprintf "Unknown AgentOwnerRoot claim: %s" promptKey)
        | Some claim ->
            match claim.Origin, claim.EffectiveAgent with
            | PromptAuthority.AuthorityRoot PromptAuthority.AgentOwnerRoot, Some agent ->
                match
                    PromptAuthority.createAuthorityRoot
                        runtimeId
                        sessionId
                        PromptAuthority.AgentOwnerRoot
                        hostMessageId
                        agent
                with
                | Error error -> Error error
                | Ok profile ->
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
                        match persistAuthorityRoot persist sessionId hostMessageId profile "AgentOwnerRoot" with
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
        (read:
            (PromptAuthority.PromptAuthorityProjection -> PromptAuthority.ContinuationKind option)
                -> PromptAuthority.ContinuationKind option)
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
