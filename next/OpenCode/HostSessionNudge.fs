namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module HostSessionNudge =

    let private service (journal: AgentJournal option) =
        match journal with
        | Some j -> PromptDispatcher.forJournal j
        | None -> PromptDispatcher.ephemeral ()

    let private toProfile (sessionId: SessionId) (durable: AuthorityProfileProjection) =
        let authorityKind =
            match durable.AuthorityKind with
            | "AgentOwnerRoot" -> PromptAuthority.AgentOwnerRoot
            | _ -> PromptAuthority.HumanRoot

        match ManagedAgent.parse durable.SelectedAgent with
        | Error _ -> None
        | Ok selected ->
            let peer =
                if String.IsNullOrWhiteSpace durable.PeerAgent then
                    (ManagedAgent.peer selected).Name
                else
                    durable.PeerAgent

            let canonicalRole =
                match PromptAuthority.tryParseRole durable.CanonicalRole with
                | Some role -> role
                | None -> selected.Role

            let selectedTier =
                match PromptAuthority.tryParseTier durable.SelectedTier with
                | Some tier -> tier
                | None -> selected.Tier

            Some(
                { SessionId = sessionId
                  LogicalRunId = durable.LogicalRunId
                  AuthorityRootUserMessageId = MessageId.create durable.AuthorityRootUserMessageId
                  AuthorityKind = authorityKind
                  SelectedAgent = selected.Name
                  PeerAgent = peer
                  CanonicalRole = canonicalRole
                  SelectedTier = selectedTier }
                : PromptAuthority.AuthorityExecutionProfile
            )

    let private tryActiveProfile (journal: AgentJournal option) (sessionId: SessionId) =
        let svc = service journal

        match svc.ActiveProfile sessionId with
        | Some profile -> Some profile
        | None ->
            match journal with
            | None -> None
            | Some j ->
                match Map.tryFind sessionId (AgentJournal.snapshot j).AgentProjections.Sessions with
                | None -> None
                | Some session ->
                    session.PromptAuthority
                    |> Option.bind (fun authority -> authority.ActiveLogicalRun)
                    |> Option.bind (toProfile sessionId)

    let private effectiveAgentOf
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        =
        match journal with
        | None -> profile.SelectedAgent
        | Some j ->
            match Map.tryFind sessionId (AgentJournal.snapshot j).AgentProjections.Sessions with
            | Some session ->
                match session.Fallback with
                | Some fb -> PromptAuthority.effectiveAgentAt profile fb.Offset
                | None -> profile.SelectedAgent
            | None -> profile.SelectedAgent

    /// Reconciled linked children have a host-proven root user message even when
    /// the host omitted agent metadata from `chat.message`. Register that real
    /// AgentOwner authority once; never use this for an unlinked/unknown session.
    /// `agent` must be a Managed Agent name (fast-* / deep-*).
    let ensureAgentOwnerAuthority
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (rootUserMessageId: MessageId)
        (agent: string)
        =
        match tryActiveProfile journal sessionId with
        | Some _ -> ()
        | None ->
            let svc = service journal
            let runtimeId = svc.RuntimeId

            match
                PromptAuthority.createAuthorityRoot
                    runtimeId
                    sessionId
                    PromptAuthority.AgentOwnerRoot
                    rootUserMessageId
                    agent
            with
            | Ok profile -> svc.RegisterAuthority profile
            | Error _ -> ()

    /// Sends a continuation only when a durable Authority Root exists. Unknown
    /// physical user messages fail closed rather than manufacturing a new root.
    let sendContinuation
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (kind: PromptAuthority.ContinuationKind)
        (options: SessionPromptOptions)
        (journal: AgentJournal option)
        (onAccepted: (MessageId -> unit) option)
        =
        match tryActiveProfile journal sessionId with
        | None -> ()
        | Some profile ->
            let svc = service journal
            let effectiveAgent = effectiveAgentOf journal sessionId profile

            task {
                let! _ =
                    svc.SendContinuation
                        sessionPort
                        sessionId
                        prompt
                        kind
                        profile
                        effectiveAgent
                        options.Directory
                        onAccepted

                ()
            }
            |> ignore

    let trySendInteractionRepair
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (options: SessionPromptOptions)
        (journal: AgentJournal option)
        (terminalAssistantMessageId: MessageId)
        (repairKind: string)
        (onAccepted: (MessageId -> unit) option)
        : bool =
        match tryActiveProfile journal sessionId with
        | None -> false
        | Some profile ->
            let svc = service journal

            if not (svc.TryClaimInteractionRepair profile terminalAssistantMessageId repairKind) then
                false
            else
                let effectiveAgent = effectiveAgentOf journal sessionId profile

                task {
                    let! _ =
                        svc.SendContinuation
                            sessionPort
                            sessionId
                            prompt
                            PromptAuthority.InteractionRepair
                            profile
                            effectiveAgent
                            options.Directory
                            onAccepted

                    ()
                }
                |> ignore

                true
