namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity

/// Pure authority-run lifecycle operations. Types and identity rules live in
/// PromptAuthority; this module owns claim/run/projection transitions only.
[<RequireQualifiedAccess>]
module PromptAuthorityRun =

    let createAuthorityRoot
        (sha256: string -> string)
        (runtimeId: string)
        (sessionId: SessionId)
        (rootKind: PromptAuthority.RootAuthorityKind)
        (messageId: MessageId)
        (selectedAgentName: string)
        : Result<PromptAuthority.AuthorityExecutionProfile, string> =
        match PromptAuthority.parseAgentName selectedAgentName with
        | Error e -> Error e
        | Ok(name, role, tier, peer) ->
            Ok
                { SessionId = sessionId
                  LogicalRunId = PromptAuthority.stableLogicalRunId sha256 runtimeId sessionId messageId
                  AuthorityRootUserMessageId = messageId
                  AuthorityKind = rootKind
                  SelectedAgent = name
                  PeerAgent = peer
                  CanonicalRole = role
                  SelectedTier = tier }

    let claimAgentOwnerRoot
        (key: PromptKeyRef)
        (sessionId: SessionId)
        (selectedAgentName: string)
        : Result<PromptAuthority.PromptClaim, string> =
        match PromptAuthority.parseAgentName selectedAgentName with
        | Error e -> Error e
        | Ok(name, _role, _tier, _peer) ->
            Ok
                { PromptKey = key
                  SessionId = sessionId
                  Origin = PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                  LogicalRunId = ""
                  AuthorityRootUserMessageId = None
                  EffectiveAgent = Some name }

    let claimContinuation
        (key: PromptKeyRef)
        (sessionId: SessionId)
        (continuation: PromptAuthority.ContinuationKind)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (effectiveAgent: string)
        : PromptAuthority.PromptClaim =
        { PromptKey = key
          SessionId = sessionId
          Origin = PromptAuthority.PromptOrigin.Continuation continuation
          LogicalRunId = profile.LogicalRunId
          AuthorityRootUserMessageId = Some profile.AuthorityRootUserMessageId
          EffectiveAgent = Some effectiveAgent }

    let registerAuthority
        (profile: PromptAuthority.AuthorityExecutionProfile)
        (projection: PromptAuthority.PromptAuthorityProjection)
        =
        { projection with
            LastAuthorityProfile = Some profile
            ActiveLogicalRun = Some profile
            PendingClaims = Map.empty
            AcceptedContinuationIds = Map.empty
            RepairClaims = Set.empty }

    let registerClaim (claim: PromptAuthority.PromptClaim) (projection: PromptAuthority.PromptAuthorityProjection) =
        { projection with
            PendingClaims = Map.add claim.PromptKey claim projection.PendingClaims }

    let acceptClaim
        (key: PromptKeyRef)
        (hostMessageId: MessageId)
        (projection: PromptAuthority.PromptAuthorityProjection)
        =
        match Map.tryFind key projection.PendingClaims with
        | Some { Origin = PromptAuthority.PromptOrigin.Continuation continuation } ->
            { projection with
                PendingClaims = Map.remove key projection.PendingClaims
                AcceptedContinuationIds = Map.add hostMessageId continuation projection.AcceptedContinuationIds }
        | Some _ ->
            { projection with
                PendingClaims = Map.remove key projection.PendingClaims }
        | None -> projection

    let abandonClaim (key: PromptKeyRef) (projection: PromptAuthority.PromptAuthorityProjection) =
        { projection with
            PendingClaims = Map.remove key projection.PendingClaims }

    let tryClaimRepair (identity: string) (projection: PromptAuthority.PromptAuthorityProjection) =
        if Set.contains identity projection.RepairClaims then
            None
        else
            Some
                { projection with
                    RepairClaims = Set.add identity projection.RepairClaims }

    let resolveKnownOrigin
        (messageId: MessageId)
        (promptKey: PromptKeyRef option)
        (hostCompaction: bool)
        (projection: PromptAuthority.PromptAuthorityProjection)
        : PromptAuthority.PromptOrigin =
        match Map.tryFind messageId projection.AcceptedContinuationIds with
        | Some continuation -> PromptAuthority.PromptOrigin.Continuation continuation
        | None ->
            match promptKey |> Option.bind (fun key -> Map.tryFind key projection.PendingClaims) with
            | Some claim -> claim.Origin
            | None when hostCompaction -> PromptAuthority.PromptOrigin.HostInternal
            | None ->
                match promptKey, projection.ActiveLogicalRun with
                | Some _, Some { AuthorityKind = PromptAuthority.RootAuthorityKind.AgentOwnerRoot } ->
                    PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                | _ -> PromptAuthority.PromptOrigin.UnknownOrigin
