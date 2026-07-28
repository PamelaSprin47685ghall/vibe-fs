namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity

/// A physical `role=user` message is transport, not authorization.
module PromptAuthority =

    type RootAuthorityKind =
        | HumanRoot
        | AgentOwnerRoot

    type ContinuationKind =
        | InteractionRepair
        | ManagerGuard
        | ReviewerGuard
        | ReviewConfirmation
        | BusyAgentNudge
        | ProviderRetryAttempt
        | HostCompactionContinue

    type PromptOrigin =
        | AuthorityRoot of RootAuthorityKind
        | Continuation of ContinuationKind
        | HostInternal
        | UnknownOrigin

    /// Durable Authority Root execution archive. Stores Managed Agent pair only —
    /// never model IDs. Host resolves models from opencode.json agent bindings.
    type AuthorityExecutionProfile =
        { SessionId: SessionId
          LogicalRunId: string
          AuthorityRootUserMessageId: MessageId
          AuthorityKind: RootAuthorityKind
          SelectedAgent: string
          PeerAgent: string
          CanonicalRole: Role
          SelectedTier: AgentTier }

    /// Same-run attempt layer. Fallback may change EffectiveAgent only.
    type AttemptExecutionProfile =
        { Authority: AuthorityExecutionProfile
          PhysicalUserMessageId: MessageId
          ProviderAttempt: int64
          EffectiveAgent: string
          Origin: PromptOrigin }

    type PromptClaim =
        { PromptKey: PromptKeyRef
          SessionId: SessionId
          Origin: PromptOrigin
          LogicalRunId: string
          AuthorityRootUserMessageId: MessageId option
          EffectiveAgent: string option }

    type PromptAuthorityProjection =
        { LastAuthorityProfile: AuthorityExecutionProfile option
          ActiveLogicalRun: AuthorityExecutionProfile option
          PendingClaims: Map<PromptKeyRef, PromptClaim>
          AcceptedContinuationIds: Map<MessageId, ContinuationKind>
          RepairClaims: Set<string> }

    let empty =
        { LastAuthorityProfile = None
          ActiveLogicalRun = None
          PendingClaims = Map.empty
          AcceptedContinuationIds = Map.empty
          RepairClaims = Set.empty }

    let private keyValue key = PromptKeyRef.value key

    [<Import("createHash", "node:crypto")>]
    let private createHashImport: string -> obj = jsNative

    let sha256Hex (text: string) =
        let hash = createHashImport "sha256"
        hash?update text |> ignore
        unbox<string> (hash?digest "hex")

    let stableLogicalRunId (runtimeId: string) (sessionId: SessionId) (rootUserMessageId: MessageId) =
        sha256Hex (
            String.Concat(
                [| runtimeId
                   "\n"
                   SessionId.value sessionId
                   "\n"
                   MessageId.value rootUserMessageId |]
            )
        )

    let newPromptKey () =
        PromptKeyRef.create (Guid.NewGuid().ToString("N"))

    let tierLabel (tier: AgentTier) =
        match tier with
        | AgentTier.Fast -> "Fast"
        | AgentTier.Deep -> "Deep"

    let tryParseTier (value: string) =
        match value with
        | "Fast"
        | "fast" -> Some AgentTier.Fast
        | "Deep"
        | "deep" -> Some AgentTier.Deep
        | _ -> None

    let roleLabel (role: Role) = ManagedAgent.roleName role

    let tryParseRole (value: string) =
        ManagedAgent.tryParse ("fast-" + value) |> Option.map (fun agent -> agent.Role)

    let agentPair (profile: AuthorityExecutionProfile) : EffectiveAgentResolver.AuthorityAgentPair =
        { SelectedAgent = profile.SelectedAgent
          PeerAgent = profile.PeerAgent }

    let effectiveAgentAt (profile: AuthorityExecutionProfile) (offset: byte) : string =
        EffectiveAgentResolver.effectiveAgent
            (agentPair profile)
            { Offset = offset
              LastProviderAttempt = None }

    /// Default EffectiveAgent for a new root / offset-0 attempt is SelectedAgent.
    let selectedEffectiveAgent (profile: AuthorityExecutionProfile) = profile.SelectedAgent

    let createAuthorityRootFromManaged
        (runtimeId: string)
        (sessionId: SessionId)
        (rootKind: RootAuthorityKind)
        (messageId: MessageId)
        (selected: ManagedAgent)
        : AuthorityExecutionProfile =
        let peer = ManagedAgent.peer selected

        { SessionId = sessionId
          LogicalRunId = stableLogicalRunId runtimeId sessionId messageId
          AuthorityRootUserMessageId = messageId
          AuthorityKind = rootKind
          SelectedAgent = selected.Name
          PeerAgent = peer.Name
          CanonicalRole = selected.Role
          SelectedTier = selected.Tier }

    /// New Authority Root requires an explicit Managed Agent name (fast-* / deep-*).
    /// Peer is derived via ManagedAgent.peer — never guessed from model IDs.
    let createAuthorityRoot
        (runtimeId: string)
        (sessionId: SessionId)
        (rootKind: RootAuthorityKind)
        (messageId: MessageId)
        (selectedAgentName: string)
        : Result<AuthorityExecutionProfile, string> =
        match ManagedAgent.parse selectedAgentName with
        | Error err -> Error(ManagedAgent.formatParseError err)
        | Ok selected -> Ok(createAuthorityRootFromManaged runtimeId sessionId rootKind messageId selected)

    let claimContinuation
        (key: PromptKeyRef)
        (sessionId: SessionId)
        (continuation: ContinuationKind)
        (profile: AuthorityExecutionProfile)
        (effectiveAgent: string)
        : PromptClaim =
        { PromptKey = key
          SessionId = sessionId
          Origin = Continuation continuation
          LogicalRunId = profile.LogicalRunId
          AuthorityRootUserMessageId = Some profile.AuthorityRootUserMessageId
          EffectiveAgent = Some effectiveAgent }

    let claimAgentOwnerRoot
        (key: PromptKeyRef)
        (sessionId: SessionId)
        (selectedAgentName: string)
        : Result<PromptClaim, string> =
        match ManagedAgent.parse selectedAgentName with
        | Error err -> Error(ManagedAgent.formatParseError err)
        | Ok selected ->
            Ok
                { PromptKey = key
                  SessionId = sessionId
                  Origin = AuthorityRoot AgentOwnerRoot
                  LogicalRunId = ""
                  AuthorityRootUserMessageId = None
                  EffectiveAgent = Some selected.Name }

    let registerAuthority profile projection =
        { projection with
            LastAuthorityProfile = Some profile
            ActiveLogicalRun = Some profile
            PendingClaims = Map.empty
            AcceptedContinuationIds = Map.empty
            RepairClaims = Set.empty }

    let registerClaim claim projection =
        { projection with
            PendingClaims = Map.add claim.PromptKey claim projection.PendingClaims }

    let acceptClaim key hostMessageId projection =
        match Map.tryFind key projection.PendingClaims with
        | Some { Origin = Continuation continuation } ->
            { projection with
                PendingClaims = Map.remove key projection.PendingClaims
                AcceptedContinuationIds = Map.add hostMessageId continuation projection.AcceptedContinuationIds }
        | Some _ ->
            { projection with
                PendingClaims = Map.remove key projection.PendingClaims }
        | None -> projection

    let abandonClaim key projection =
        { projection with
            PendingClaims = Map.remove key projection.PendingClaims }

    let repairIdentity
        (logicalRunId: string)
        (authorityRootUserMessageId: MessageId)
        (terminalAssistantMessageId: MessageId)
        (repairKind: string)
        =
        String.Concat(
            [| logicalRunId
               "|"
               MessageId.value authorityRootUserMessageId
               "|"
               MessageId.value terminalAssistantMessageId
               "|"
               repairKind |]
        )

    let tryClaimRepair identity projection =
        if Set.contains identity projection.RepairClaims then
            None
        else
            Some
                { projection with
                    RepairClaims = Set.add identity projection.RepairClaims }

    let originLabel origin =
        match origin with
        | AuthorityRoot HumanRoot -> "HumanRoot"
        | AuthorityRoot AgentOwnerRoot -> "AgentOwnerRoot"
        | Continuation InteractionRepair -> "InteractionRepair"
        | Continuation ManagerGuard -> "ManagerGuard"
        | Continuation ReviewerGuard -> "ReviewerGuard"
        | Continuation ReviewConfirmation -> "ReviewConfirmation"
        | Continuation BusyAgentNudge -> "BusyAgentNudge"
        | Continuation ProviderRetryAttempt -> "ProviderRetryAttempt"
        | Continuation HostCompactionContinue -> "HostCompactionContinue"
        | HostInternal -> "HostInternal"
        | UnknownOrigin -> "UnknownOrigin"

    let tryParseContinuationKind (value: string) =
        match value with
        | "InteractionRepair" -> Some InteractionRepair
        | "ManagerGuard" -> Some ManagerGuard
        | "ReviewerGuard" -> Some ReviewerGuard
        | "ReviewConfirmation" -> Some ReviewConfirmation
        | "BusyAgentNudge" -> Some BusyAgentNudge
        | "ProviderRetryAttempt" -> Some ProviderRetryAttempt
        | "HostCompactionContinue" -> Some HostCompactionContinue
        | _ -> None

    /// Resolution deliberately never guesses Human authority. The caller must
    /// separately prove an external prompt-acceptance boundary for HumanRoot.
    let resolveKnownOrigin messageId promptKey hostCompaction projection =
        match Map.tryFind messageId projection.AcceptedContinuationIds with
        | Some continuation -> Continuation continuation
        | None ->
            match promptKey |> Option.bind (fun key -> Map.tryFind key projection.PendingClaims) with
            | Some claim -> claim.Origin
            | None when hostCompaction -> HostInternal
            | None -> UnknownOrigin
