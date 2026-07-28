namespace Wanxiangshu.Next.Domain

open System
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity

[<RequireQualifiedAccess>]
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

    type AuthorityExecutionProfile =
        { SessionId: SessionId
          LogicalRunId: string
          AuthorityRootUserMessageId: MessageId
          AuthorityKind: RootAuthorityKind
          SelectedAgent: string
          PeerAgent: string
          CanonicalRole: Role
          SelectedTier: AgentTier }

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

    let empty: PromptAuthorityProjection =
        { LastAuthorityProfile = None
          ActiveLogicalRun = None
          PendingClaims = Map.empty
          AcceptedContinuationIds = Map.empty
          RepairClaims = Set.empty }

    let newPromptKey () =
        PromptKeyRef.create (Guid.NewGuid().ToString("N"))

    let originLabel (origin: PromptOrigin) =
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

    let roleLabel (role: Role) =
        match role with
        | Role.Manager -> "manager"
        | Role.Orchestrator -> "orchestrator"
        | Role.Coder -> "coder"
        | Role.Inspector -> "inspector"
        | Role.DevOps -> "devops"
        | Role.Browser -> "browser"
        | Role.Meditator -> "meditator"
        | Role.Reviewer -> "reviewer"
        | Role.Executor -> "executor"
        | Role.Blogger -> "blogger"

    let tryParseRole (value: string) =
        match value.ToLowerInvariant() with
        | "manager" -> Some Role.Manager
        | "orchestrator" -> Some Role.Orchestrator
        | "coder" -> Some Role.Coder
        | "inspector" -> Some Role.Inspector
        | "devops" -> Some Role.DevOps
        | "browser" -> Some Role.Browser
        | "meditator" -> Some Role.Meditator
        | "reviewer" -> Some Role.Reviewer
        | "executor" -> Some Role.Executor
        | "blogger" -> Some Role.Blogger
        | _ -> None

    let tierLabel (tier: AgentTier) =
        match tier with
        | AgentTier.Fast -> "Fast"
        | AgentTier.Deep -> "Deep"

    let tryParseTier (value: string) =
        match value.ToLowerInvariant() with
        | "fast" -> Some AgentTier.Fast
        | "deep" -> Some AgentTier.Deep
        | _ -> None

    let private roleOfName (name: string) =
        match name.ToLowerInvariant() with
        | "manager" -> Some Role.Manager
        | "orchestrator" -> Some Role.Orchestrator
        | "coder" -> Some Role.Coder
        | "inspector" -> Some Role.Inspector
        | "devops" -> Some Role.DevOps
        | "browser" -> Some Role.Browser
        | "meditator" -> Some Role.Meditator
        | "reviewer" -> Some Role.Reviewer
        | "blogger" -> Some Role.Blogger
        | "executor" -> Some Role.Executor
        | _ -> None

    let private legacySet =
        set
            [ "orchestrator"
              "manager"
              "build"
              "plan"
              "coder"
              "inspector"
              "devops"
              "browser"
              "meditator"
              "reviewer"
              "blogger"
              "executor"
              "fast"
              "deep" ]

    let parseAgentName (value: string) : Result<string * Role * AgentTier * string, string> =
        if String.IsNullOrWhiteSpace value then
            Error "Expected fast-ROLE or deep-ROLE."
        else
            let trimmed = value.Trim()
            let lower = trimmed.ToLowerInvariant()

            if
                legacySet.Contains lower
                || lower.Contains("_")
                || lower.EndsWith("-fast")
                || lower.EndsWith("-deep")
                || lower.StartsWith("fast_")
                || lower.StartsWith("deep_")
            then
                Error(sprintf "Legacy agent name '%s' is not supported." trimmed)
            else
                let parts = trimmed.Split([| '-' |], 2)

                if parts.Length <> 2 then
                    Error "Expected fast-ROLE or deep-ROLE."
                else
                    let tier =
                        match parts.[0] with
                        | "fast" -> Some AgentTier.Fast
                        | "deep" -> Some AgentTier.Deep
                        | _ -> None

                    match tier with
                    | None -> Error "Unknown tier. Use fast-* or deep-*."
                    | Some tierValue ->
                        match roleOfName parts.[1] with
                        | None -> Error "Unknown role. Use fast-* or deep-*."
                        | Some role ->
                            let peerTier =
                                match tierValue with
                                | AgentTier.Fast -> AgentTier.Deep
                                | AgentTier.Deep -> AgentTier.Fast

                            let peerName =
                                sprintf "%s-%s" ((tierLabel peerTier).ToLowerInvariant()) (roleLabel role)

                            Ok(trimmed, role, tierValue, peerName)

    let stableLogicalRunId
        (sha256: string -> string)
        (runtimeId: string)
        (sessionId: SessionId)
        (rootUserMessageId: MessageId)
        =
        sha256 (
            String.Concat(
                [| runtimeId
                   "\n"
                   SessionId.value sessionId
                   "\n"
                   MessageId.value rootUserMessageId |]
            )
        )

    let agentPair (profile: AuthorityExecutionProfile) : AgentPairCursor.AuthorityAgentPair =
        { AgentPairCursor.AuthorityAgentPair.SelectedAgent = profile.SelectedAgent
          AgentPairCursor.AuthorityAgentPair.PeerAgent = profile.PeerAgent }

    let effectiveAgentAt (profile: AuthorityExecutionProfile) (offset: byte) : string =
        AgentPairCursor.effectiveAgent (agentPair profile) (AgentPairCursor.atOffset offset)

    let selectedEffectiveAgent (profile: AuthorityExecutionProfile) = profile.SelectedAgent

    let effectiveAgentFromManaged (selected: string) (peer: string) (cursor: AgentPairCursor.FallbackCursor) : string =
        AgentPairCursor.effectiveAgent
            { AgentPairCursor.AuthorityAgentPair.SelectedAgent = selected
              AgentPairCursor.AuthorityAgentPair.PeerAgent = peer }
            cursor

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
