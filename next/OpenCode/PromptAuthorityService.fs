namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

type PromptAuthorityService(runtimeId: string, ?journal: AgentJournal) =
    let gate = obj ()

    let mutable authority =
        match journal with
        | Some j -> PromptAuthorityRestore.fromJournal j
        | None -> PromptAuthority.empty

    member _.RuntimeId = runtimeId
    member _.Projection = lock gate (fun () -> authority)

    member private _.Persist (sessionId: SessionId) (turnId: TurnId option) (fact: AgentFact) : Result<unit, string> =
        match journal with
        | None -> Ok()
        | Some j ->
            AgentJournal.appendAgent (StreamId.Session sessionId) turnId fact j
            |> Result.map (fun _ -> ())
            |> Result.mapError string

    member private _.Update(f: PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptAuthorityProjection) =
        lock gate (fun () -> authority <- f authority)

    member private _.Read(f: PromptAuthority.PromptAuthorityProjection -> 'a) : 'a = lock gate (fun () -> f authority)

    member this.RegisterAuthority(profile: PromptAuthority.AuthorityExecutionProfile) =
        this.Update(PromptAuthority.registerAuthority profile)

        this.Persist
            profile.SessionId
            (Some(TurnId.ofMessageId profile.AuthorityRootUserMessageId))
            (AgentFact.AuthorityRootAccepted
                {| SessionId = profile.SessionId
                   LogicalRunId = profile.LogicalRunId
                   HostMessageId = MessageId.value profile.AuthorityRootUserMessageId
                   AuthorityKind = string profile.AuthorityKind
                   SelectedAgent = profile.SelectedAgent
                   PeerAgent = profile.PeerAgent
                   CanonicalRole = PromptAuthority.roleLabel profile.CanonicalRole
                   SelectedTier = PromptAuthority.tierLabel profile.SelectedTier |})
        |> function
            | Ok() -> ()
            | Error error -> raise (InvalidOperationException error)

    member this.AcceptHumanRoot sessionId messageId explicitAgent hostAgent =
        PromptAuthorityAccept.acceptHumanRoot
            runtimeId
            this.Persist
            this.Update
            this.Read
            sessionId
            messageId
            explicitAgent
            hostAgent

    member this.AcceptAgentOwnerRoot promptKey sessionId hostMessageId =
        PromptAuthorityAccept.acceptAgentOwnerRoot
            runtimeId
            this.Persist
            this.Update
            this.Read
            (fun () -> this.Read(fun auth -> auth.ActiveLogicalRun))
            promptKey
            sessionId
            hostMessageId

    member this.AcceptContinuation promptKey sessionId hostMessageId =
        PromptAuthorityAccept.acceptContinuation this.Persist this.Update this.Read promptKey sessionId hostMessageId

    member this.TryClaimInteractionRepair
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (terminalAssistantMessageId: MessageId)
        (repairKind: string)
        =
        let identity =
            PromptAuthority.repairIdentity
                profile.LogicalRunId
                profile.AuthorityRootUserMessageId
                terminalAssistantMessageId
                repairKind

        match
            this.Persist
                profile.SessionId
                (Some(TurnId.ofMessageId profile.AuthorityRootUserMessageId))
                (AgentFact.InteractionRepairClaimed
                    {| SessionId = profile.SessionId
                       LogicalRunId = profile.LogicalRunId
                       AuthorityRootUserMessageId = MessageId.value profile.AuthorityRootUserMessageId
                       TerminalAssistantMessageId = MessageId.value terminalAssistantMessageId
                       RepairKind = repairKind |})
        with
        | Error _ -> false
        | Ok() ->
            match this.Read(fun auth -> PromptAuthority.tryClaimRepair identity auth) with
            | None -> false
            | Some next ->
                this.Update(fun _ -> next)
                true

    member private this.ClaimOps: PromptAuthoritySend.ClaimOps =
        { ClaimAndPersist =
            fun sessionId turnId fact claim ->
                match this.Persist sessionId turnId fact with
                | Error e -> Error e
                | Ok() ->
                    this.Update(PromptAuthority.registerClaim claim)
                    Ok()
          AbandonClaim =
            fun sessionId key reason ->
                let abandoned =
                    this.Persist
                        sessionId
                        None
                        (AgentFact.PluginPromptAbandoned
                            {| PromptKey = PromptKeyRef.value key
                               SessionId = sessionId
                               Reason = reason |})

                this.Update(PromptAuthority.abandonClaim key)
                abandoned
          AcceptPhysicalClaim =
            fun sessionId key messageId profile ->
                match
                    this.Persist
                        sessionId
                        None
                        (AgentFact.PluginPromptAccepted
                            {| PromptKey = PromptKeyRef.value key
                               SessionId = sessionId
                               HostMessageId = MessageId.value messageId |})
                with
                | Error e -> Error e
                | Ok() ->
                    match profile with
                    | None ->
                        this.Update(PromptAuthority.acceptClaim key messageId)
                        Ok()
                    | Some p ->
                        match
                            this.Persist
                                sessionId
                                (Some(TurnId.ofMessageId messageId))
                                (AgentFact.AuthorityRootAccepted
                                    {| SessionId = sessionId
                                       LogicalRunId = p.LogicalRunId
                                       HostMessageId = MessageId.value messageId
                                       AuthorityKind = string p.AuthorityKind
                                       SelectedAgent = p.SelectedAgent
                                       PeerAgent = p.PeerAgent
                                       CanonicalRole = PromptAuthority.roleLabel p.CanonicalRole
                                       SelectedTier = PromptAuthority.tierLabel p.SelectedTier |})
                        with
                        | Error e -> Error e
                        | Ok() ->
                            this.Update(fun auth ->
                                auth
                                |> PromptAuthority.acceptClaim key messageId
                                |> PromptAuthority.registerAuthority p)

                            Ok() }

    member this.SendAgentOwnerRoot port sessionId text agent directory onAccepted =
        PromptAuthoritySend.sendAgentOwnerRoot this.ClaimOps runtimeId port sessionId text agent directory onAccepted

    member this.SendContinuation port sessionId text continuation profile effectiveAgent directory onAccepted =
        PromptAuthoritySend.sendContinuation
            this.ClaimOps
            port
            sessionId
            text
            continuation
            profile
            effectiveAgent
            directory
            onAccepted

    member this.ResolveOrigin messageId promptKey hostCompaction =
        this.Read(fun auth -> PromptAuthority.resolveKnownOrigin messageId promptKey hostCompaction auth)

    member this.ActiveProfile(sessionId: SessionId) =
        this.Read(fun (auth: PromptAuthority.PromptAuthorityProjection) ->
            match auth.ActiveLogicalRun with
            | Some profile when profile.SessionId = sessionId -> Some profile
            | _ ->
                match auth.LastAuthorityProfile with
                | Some profile when profile.SessionId = sessionId -> Some profile
                | _ -> None)
