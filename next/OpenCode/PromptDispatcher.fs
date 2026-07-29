namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

[<RequireQualifiedAccess>]
module PromptDispatcher =

    let internal sha256Hex = PromptAuthority.sha256Hex
    let internal newPromptKey = PromptAuthority.newPromptKey
    let internal originLabel = PromptAuthority.originLabel

    let private fromJournal (journal: AgentJournal) : PromptAuthority.PromptAuthorityProjection =
        (AgentJournal.snapshot journal).AgentProjections.Sessions
        |> Map.toList
        |> List.choose (fun (_, session) -> session.PromptAuthority)
        |> List.fold
            (fun acc proj ->
                { LastAuthorityProfile = proj.LastAuthorityProfile |> Option.orElse acc.LastAuthorityProfile
                  ActiveLogicalRun = proj.ActiveLogicalRun |> Option.orElse acc.ActiveLogicalRun
                  PendingClaims = Map.fold (fun m k v -> Map.add k v m) acc.PendingClaims proj.PendingClaims
                  AcceptedContinuationIds =
                    Map.fold (fun m k v -> Map.add k v m) acc.AcceptedContinuationIds proj.AcceptedContinuationIds
                  AcceptedContinuationRoots =
                    Map.fold (fun m k v -> Map.add k v m) acc.AcceptedContinuationRoots proj.AcceptedContinuationRoots
                  RepairClaims = Set.union acc.RepairClaims proj.RepairClaims })
            PromptAuthority.empty

    type Runtime(runtimeId: string, ?journal: AgentJournal) =
        let gate = obj ()

        let mutable authority =
            match journal with
            | Some j -> fromJournal j
            | None -> PromptAuthority.empty

        member _.RuntimeId = runtimeId

        member _.Projection = lock gate (fun () -> authority)

        member internal _.Persist
            (sessionId: SessionId)
            (turnId: TurnId option)
            (fact: AgentFact)
            : Result<unit, string> =
            match journal with
            | None -> Ok()
            | Some j ->
                AgentJournal.appendAgent (StreamId.Session sessionId) turnId fact j
                |> Result.map (fun _ -> ())
                |> Result.mapError (fun f -> sprintf "%A" f.Failure)

        member internal _.Update
            (f: PromptAuthority.PromptAuthorityProjection -> PromptAuthority.PromptAuthorityProjection)
            =
            lock gate (fun () -> authority <- f authority)

        member private _.Read(f: PromptAuthority.PromptAuthorityProjection -> 'a) : 'a =
            lock gate (fun () -> f authority)

        member this.RegisterAuthority(profile: PromptAuthority.AuthorityExecutionProfile) =
            let fact =
                AgentFact.AuthorityRootAccepted
                    {| SessionId = profile.SessionId
                       LogicalRunId = profile.LogicalRunId
                       HostMessageId = MessageId.value profile.AuthorityRootUserMessageId
                       AuthorityKind =
                        match profile.AuthorityKind with
                        | PromptAuthority.RootAuthorityKind.AgentOwnerRoot -> "AgentOwnerRoot"
                        | PromptAuthority.RootAuthorityKind.HumanRoot -> "HumanRoot"
                       SelectedAgent = profile.SelectedAgent
                       PeerAgent = profile.PeerAgent
                       CanonicalRole = PromptAuthority.roleLabel profile.CanonicalRole
                       SelectedTier = PromptAuthority.tierLabel profile.SelectedTier |}

            match this.Persist profile.SessionId (Some(TurnId.ofMessageId profile.AuthorityRootUserMessageId)) fact with
            | Error e -> raise (InvalidOperationException e)
            | Ok() ->
                match profile.AuthorityKind, journal with
                | PromptAuthority.RootAuthorityKind.HumanRoot, Some j ->
                    match
                        AgentJournal.recordHumanPromptAccepted
                            j
                            profile.SessionId
                            profile.AuthorityRootUserMessageId
                    with
                    | Ok() -> ()
                    | Error e -> raise (InvalidOperationException e)
                | _ -> ()

                this.Update(PromptAuthorityRun.registerAuthority profile)

        member this.AcceptHumanRoot (sessionId: SessionId) (messageId: MessageId) (explicitAgent: string option) =
            match explicitAgent with
            | None -> Error "HumanRoot requires explicit managed agent (fast-* / deep-*)"
            | Some agent ->
                match
                    PromptAuthorityRun.createAuthorityRoot
                        sha256Hex
                        this.RuntimeId
                        sessionId
                        PromptAuthority.RootAuthorityKind.HumanRoot
                        messageId
                        agent
                with
                | Error e -> Error e
                | Ok profile ->
                    this.RegisterAuthority profile
                    Ok profile

        member internal this.AcceptPhysicalAgentOwnerRoot
            (key: PromptKeyRef)
            (sessionId: SessionId)
            (hostMessageId: MessageId)
            (agent: string)
            =
            match
                PromptAuthorityRun.createAuthorityRoot
                    sha256Hex
                    this.RuntimeId
                    sessionId
                    PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                    hostMessageId
                    agent
            with
            | Error e -> Error e
            | Ok profile ->
                let promptKey = PromptKeyRef.value key

                let acceptedFact =
                    AgentFact.PluginPromptAccepted
                        {| PromptKey = promptKey
                           SessionId = sessionId
                           HostMessageId = MessageId.value hostMessageId |}

                match this.Persist sessionId None acceptedFact with
                | Error e -> Error e
                | Ok() ->
                    let rootFact =
                        AgentFact.AuthorityRootAccepted
                            {| SessionId = sessionId
                               LogicalRunId = profile.LogicalRunId
                               HostMessageId = MessageId.value hostMessageId
                               AuthorityKind = "AgentOwnerRoot"
                               SelectedAgent = profile.SelectedAgent
                               PeerAgent = profile.PeerAgent
                               CanonicalRole = PromptAuthority.roleLabel profile.CanonicalRole
                               SelectedTier = PromptAuthority.tierLabel profile.SelectedTier |}

                    match this.Persist sessionId (Some(TurnId.ofMessageId hostMessageId)) rootFact with
                    | Error e -> Error e
                    | Ok() ->
                        this.Update(fun auth ->
                            auth
                            |> PromptAuthorityRun.acceptClaim key hostMessageId
                            |> PromptAuthorityRun.registerAuthority profile)

                        Ok profile

        member this.AcceptAgentOwnerRoot (promptKey: string) (sessionId: SessionId) (hostMessageId: MessageId) =
            let key = PromptKeyRef.create promptKey

            match this.Read(fun auth -> Map.tryFind key auth.PendingClaims) with
            | Some claim when
                claim.Origin = PromptAuthority.PromptOrigin.AuthorityRoot
                                   PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                ->
                match claim.EffectiveAgent with
                | None -> Error(sprintf "AgentOwnerRoot claim %s has no effective agent" promptKey)
                | Some agent -> this.AcceptPhysicalAgentOwnerRoot key sessionId hostMessageId agent
            | Some _ -> Error(sprintf "PromptKey %s is not a pending AgentOwnerRoot" promptKey)
            | None ->
                // Idempotent re-accept: if an active run already exists, return it.
                match this.Read(fun auth -> auth.ActiveLogicalRun) with
                | Some profile when profile.SessionId = sessionId -> Ok profile
                | _ -> Error(sprintf "Unknown AgentOwnerRoot claim: %s" promptKey)

        member this.AcceptContinuation (promptKey: string) (sessionId: SessionId) (hostMessageId: MessageId) =
            let key = PromptKeyRef.create promptKey

            let acceptedFact =
                AgentFact.PluginPromptAccepted
                    {| PromptKey = promptKey
                       SessionId = sessionId
                       HostMessageId = MessageId.value hostMessageId |}

            match this.Persist sessionId None acceptedFact with
            | Error e -> Error e
            | Ok() ->
                let kind =
                    this.Read(fun auth ->
                        match Map.tryFind key auth.PendingClaims with
                        | Some { Origin = PromptAuthority.PromptOrigin.Continuation c } -> Some c
                        | _ -> None)

                this.Update(PromptAuthorityRun.acceptClaim key hostMessageId)
                Ok kind

        member this.ActiveProfile(sessionId: SessionId) =
            this.Read(fun auth ->
                match auth.ActiveLogicalRun with
                | Some p when p.SessionId = sessionId -> Some p
                | _ ->
                    match auth.LastAuthorityProfile with
                    | Some p when p.SessionId = sessionId -> Some p
                    | _ -> None)

        member this.ResolveOrigin (messageId: MessageId) (promptKey: PromptKeyRef option) (hostCompaction: bool) =
            this.Read(fun auth -> PromptAuthorityRun.resolveKnownOrigin messageId promptKey hostCompaction auth)

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

            let fact =
                AgentFact.InteractionRepairClaimed
                    {| SessionId = profile.SessionId
                       LogicalRunId = profile.LogicalRunId
                       AuthorityRootUserMessageId = MessageId.value profile.AuthorityRootUserMessageId
                       TerminalAssistantMessageId = MessageId.value terminalAssistantMessageId
                       RepairKind = repairKind |}

            match this.Persist profile.SessionId (Some(TurnId.ofMessageId profile.AuthorityRootUserMessageId)) fact with
            | Error _ -> false
            | Ok() ->
                match this.Read(fun auth -> PromptAuthorityRun.tryClaimRepair identity auth) with
                | None -> false
                | Some next ->
                    this.Update(fun _ -> next)
                    true

        member internal this.Metadata
            (key: PromptKeyRef)
            (origin: string)
            (logicalRunId: string)
            (authorityRootUserMessageId: string)
            =
            createObj
                [ "wanxiangshu_prompt_key", box (PromptKeyRef.value key)
                  "wanxiangshu_origin", box origin
                  "wanxiangshu_logical_run", box logicalRunId
                  "wanxiangshu_authority_root", box authorityRootUserMessageId ]

        member internal this.SubscribeNoOp (port: ISessionHostPort) (sessionId: SessionId) =
            port.SubscribeTerminal(sessionId, (fun _ _ -> ()))

    let forRuntime (runtimeId: string) (journal: AgentJournal option) =
        Runtime(runtimeId, ?journal = journal)

    let forJournal (journal: AgentJournal) =
        forRuntime (RuntimeId.value (AgentJournal.runtimeId journal)) (Some journal)

    let ephemeral () =
        forRuntime (Guid.NewGuid().ToString("N")) None

    let ephemeralNamed (runtimeId: string) = forRuntime runtimeId None

    /// Backward-compatible test alias.
    type Dispatcher(?journal: AgentJournal) =
        inherit
            Runtime(
                (match journal with
                 | Some j -> RuntimeId.value (AgentJournal.runtimeId j)
                 | None -> "test-runtime"),
                ?journal = journal
            )
