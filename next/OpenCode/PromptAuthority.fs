namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
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
          AuthorityRootUserMessageId: MessageId option
          Agent: string option
          EffectiveModel: OpencodeModel option
          Variant: string option }

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
        sha256Hex (String.Concat([| runtimeId; "\n"; SessionId.value sessionId; "\n"; MessageId.value rootUserMessageId |]))

    let newPromptKey () =
        PromptKeyRef.create (Guid.NewGuid().ToString("N"))

    let createAuthorityRoot
        (runtimeId: string)
        (sessionId: SessionId)
        (rootKind: RootAuthorityKind)
        (messageId: MessageId)
        (agent: string)
        (baseModel: OpencodeModel option)
        (variant: string option)
        : AuthorityExecutionProfile =
        { SessionId = sessionId
          LogicalRunId = stableLogicalRunId runtimeId sessionId messageId
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
          AuthorityRootUserMessageId = Some profile.AuthorityRootUserMessageId
          Agent = Some profile.Agent
          EffectiveModel = effectiveModel
          Variant = profile.Variant }

    let claimAgentOwnerRoot
        (key: PromptKeyRef)
        (sessionId: SessionId)
        (agent: string)
        (baseModel: OpencodeModel option)
        (variant: string option)
        : PromptClaim =
        { PromptKey = key
          SessionId = sessionId
          Origin = AuthorityRoot AgentOwnerRoot
          LogicalRunId = ""
          AuthorityRootUserMessageId = None
          Agent = Some agent
          EffectiveModel = baseModel
          Variant = variant }

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
            [|
                logicalRunId
                "|"
                MessageId.value authorityRootUserMessageId
                "|"
                MessageId.value terminalAssistantMessageId
                "|"
                repairKind
            |]
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
