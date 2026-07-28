namespace Wanxiangshu.Next.Session

open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Busy-agent nudge via PromptAuthorityService (KISS-N12 Continuation).
module HostForkBusyNudge =

    let private service (journal: AgentJournal option) =
        match journal with
        | Some j -> PromptDispatcher.forJournal j
        | None -> PromptDispatcher.ephemeral ()

    let private toProfile
        (sessionId: SessionId)
        (durable: AuthorityProfileProjection)
        : PromptAuthority.AuthorityExecutionProfile option =
        match ManagedAgent.parse durable.SelectedAgent with
        | Error _ -> None
        | Ok selected ->
            let peer = ManagedAgent.peer selected

            let kind =
                match durable.AuthorityKind with
                | "AgentOwnerRoot" -> PromptAuthority.AgentOwnerRoot
                | _ -> PromptAuthority.HumanRoot

            let canonicalRole =
                match PromptAuthority.tryParseRole durable.CanonicalRole with
                | Some role -> role
                | None -> selected.Role

            let selectedTier =
                match PromptAuthority.tryParseTier durable.SelectedTier with
                | Some tier -> tier
                | None -> selected.Tier

            let peerAgent =
                if System.String.IsNullOrWhiteSpace durable.PeerAgent then
                    peer.Name
                else
                    durable.PeerAgent

            Some
                { SessionId = sessionId
                  LogicalRunId = durable.LogicalRunId
                  AuthorityRootUserMessageId = MessageId.create durable.AuthorityRootUserMessageId
                  AuthorityKind = kind
                  SelectedAgent = selected.Name
                  PeerAgent = peerAgent
                  CanonicalRole = canonicalRole
                  SelectedTier = selectedTier }

    /// Continuation of the child's active Logical Run. Never creates a new
    /// Authority Root / RunId / completion.
    let send
        (sessions: ISessionHostPort)
        (parentId: SessionId)
        (journal: AgentJournal option)
        (childId: SessionId)
        (_role: AgentRole)
        (agent: string)
        (directory: string option)
        (prompt: string)
        : Task<Result<unit, string>> =
        task {
            match journal with
            | None ->
                return!
                    sessions.SendChildPromptFireAndForget(
                        parentId,
                        childId,
                        prompt,
                        { Model = None
                          Agent = Some agent
                          Directory = directory
                          Metadata = None }
                    )
            | Some j ->
                let svc = service (Some j)

                let profileOpt =
                    match svc.ActiveProfile childId with
                    | Some profile -> Some profile
                    | None ->
                        match Map.tryFind childId (AgentJournal.snapshot j).AgentProjections.Sessions with
                        | None -> None
                        | Some session ->
                            session.PromptAuthority
                            |> Option.bind (fun authority -> authority.ActiveLogicalRun)
                            |> Option.bind (toProfile childId)

                match profileOpt with
                | None -> return Error "Busy nudge requires ActiveLogicalRun on child session"
                | Some profile ->
                    let offset =
                        match Map.tryFind childId (AgentJournal.snapshot j).AgentProjections.Sessions with
                        | Some session ->
                            session.Fallback |> Option.map (fun fb -> fb.Offset) |> Option.defaultValue 0uy
                        | None -> 0uy

                    let effectiveAgent = PromptAuthority.effectiveAgentAt profile offset

                    let! sent =
                        svc.SendContinuation
                            sessions
                            childId
                            prompt
                            PromptAuthority.BusyAgentNudge
                            profile
                            effectiveAgent
                            directory
                            None

                    match sent with
                    | Ok _ -> return Ok()
                    | Error err -> return Error err
        }

    let sender sessions parentId journal directoryOf =
        fun agentId childId (role: AgentRole) agent prompt ->
            send sessions parentId journal childId role agent (directoryOf agentId) prompt
