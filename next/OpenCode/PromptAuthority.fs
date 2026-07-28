namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
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

    type AuthorityExecutionProfile =
        { SessionId: SessionId
          LogicalRunId: string
          AuthorityRootUserMessageId: MessageId
          AuthorityKind: RootAuthorityKind
          Agent: string
          BaseModel: OpencodeModel option
          Variant: string option }

    type PromptClaim =
        { PromptKey: PromptKeyRef
          SessionId: SessionId
          Origin: PromptOrigin
          LogicalRunId: string
          AuthorityRootUserMessageId: MessageId
          Agent: string option
          EffectiveModel: OpencodeModel option
          Variant: string option }

    type PromptAuthorityProjection =
        { LastAuthorityProfile: AuthorityExecutionProfile option
          ActiveLogicalRun: AuthorityExecutionProfile option
          PendingClaims: Map<PromptKeyRef, PromptClaim>
          AcceptedContinuationIds: Map<MessageId, ContinuationKind> }

    let empty =
        { LastAuthorityProfile = None
          ActiveLogicalRun = None
          PendingClaims = Map.empty
          AcceptedContinuationIds = Map.empty }

    let private keyValue key = PromptKeyRef.value key

    let newPromptKey () =
        PromptKeyRef.create (Guid.NewGuid().ToString("N"))

    let createAuthorityRoot
        (sessionId: SessionId)
        (rootKind: RootAuthorityKind)
        (messageId: MessageId)
        (agent: string)
        (baseModel: OpencodeModel option)
        (variant: string option)
        : AuthorityExecutionProfile =
        { SessionId = sessionId
          LogicalRunId = Guid.NewGuid().ToString("N")
          AuthorityRootUserMessageId = messageId
          AuthorityKind = rootKind
          Agent = agent
          BaseModel = baseModel
          Variant = variant }

    let claimContinuation
        (key: PromptKeyRef)
        (sessionId: SessionId)
        (continuation: ContinuationKind)
        (profile: AuthorityExecutionProfile)
        (effectiveModel: OpencodeModel option)
        : PromptClaim =
        { PromptKey = key
          SessionId = sessionId
          Origin = Continuation continuation
          LogicalRunId = profile.LogicalRunId
          AuthorityRootUserMessageId = profile.AuthorityRootUserMessageId
          Agent = Some profile.Agent
          EffectiveModel = effectiveModel
          Variant = profile.Variant }

    let registerAuthority profile projection =
        { projection with
            LastAuthorityProfile = Some profile
            ActiveLogicalRun = Some profile
            PendingClaims = Map.empty
            AcceptedContinuationIds = Map.empty }

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
