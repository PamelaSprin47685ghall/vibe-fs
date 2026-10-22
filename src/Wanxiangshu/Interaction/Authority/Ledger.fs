namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Prompt Authority folds (docs/what/prompt.md).
///
/// Each fold takes the fact payload directly. The previous version accepted
/// anonymous records of strings that mirrored the fact shape and re-parsed every
/// identity, which let the fold disagree with the fact about what a field meant —
/// and it did: an unparseable AuthorityKind defaulted to HumanRoot, silently
/// granting human authority to an agent-owned root.
module PromptAuthorityLedger =

    let empty = PromptAuthority.empty

    let private parseAuthorityKind (value: string) =
        match value with
        | "HumanRoot" -> Ok PromptAuthority.RootAuthorityKind.HumanRoot
        | "AgentOwnerRoot" -> Ok PromptAuthority.RootAuthorityKind.AgentOwnerRoot
        | unknown -> Error(sprintf "unknown authority root kind: %s" unknown)


    /// The root kind a recorded closure kind belongs to, when the kind is known.
    let private recordedRootKindOf (closureKind: string) : string option =
        if AuthorityLifeClassification.closureKindLabels |> List.contains closureKind then
            Some(AuthorityLifeClassification.rootKindOfKind closureKind)
        else
            None

    /// The closure kind the accepted root derives, when it derives one.
    let private derivedClosureKindOf
        (payload: AuthorityRootAcceptedPayload)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : string option =
        AuthorityLifeClassification.expectedClosureKind
            payload.AuthorityKind
            (profile.CanonicalRole = Role.Manager)
            payload.AgentOwnerLife

    /// The closure kind the acceptance recorded must be the kind the root
    /// derives, and must be the only kind it may close under.
    let private validateExpectedClosureKind
        (payload: AuthorityRootAcceptedPayload)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        : Result<unit, string> =
        match (recordedRootKindOf payload.ExpectedClosureKind, derivedClosureKindOf payload profile) with
        | (None, _) ->
            Error(sprintf "AuthorityRootAccepted expected closure kind is unknown: %s" payload.ExpectedClosureKind)
        | (Some rootKind, _) when rootKind <> payload.AuthorityKind ->
            Error(
                sprintf
                    "AuthorityRootAccepted expected closure kind %s does not match authority kind %s"
                    payload.ExpectedClosureKind
                    payload.AuthorityKind
            )
        | (Some _, None) ->
            Error(
                sprintf
                    "AuthorityRootAccepted expected closure kind %s cannot be derived from the accepted root"
                    payload.ExpectedClosureKind
            )
        | (Some _, Some derived) when derived = payload.ExpectedClosureKind -> Ok()
        | (Some _, Some derived) ->
            Error(
                sprintf
                    "AuthorityRootAccepted expected closure kind %s does not match the derived kind %s"
                    payload.ExpectedClosureKind
                    derived
            )

    let foldAuthorityRootAccepted
        (projection: PromptAuthority.PromptAuthorityProjection)
        (payload: AuthorityRootAcceptedPayload)
        : Result<PromptAuthority.PromptAuthorityProjection, string> =
        if payload.SchemaVersion <> 2 && payload.SchemaVersion <> 3 then
            Error(sprintf "unsupported AuthorityRootAccepted schema version: %d" payload.SchemaVersion)
        else
            parseAuthorityKind payload.AuthorityKind
            |> Result.bind (fun authorityKind ->
                PromptAuthority.createAuthorityExecutionProfileFromSeed
                    payload.SessionId
                    payload.LogicalRunId
                    payload.AuthorityRootUserMessageId
                    authorityKind
                    payload.IdentitySeed)
            |> Result.bind (fun profile ->
                validateExpectedClosureKind payload profile
                |> Result.bind (fun () ->
                    PromptAuthorityRun.registerAuthority profile projection
                    |> Result.mapError PromptAuthorityRun.describeRegistrationRejection))

    let private closeActiveRun (profile: PromptAuthority.AuthorityExecutionProfile) projection =
        match PromptAuthorityRun.closeAuthority profile.LogicalRunId profile.AuthorityRootUserMessageId projection with
        | Ok closed -> closed
        | Error error -> invalidOp error

    /// Work completion closes HumanRoot authority; AgentOwnerRoot closure belongs to its owner.
    let closeCompletedHumanRootManager
        (projection: PromptAuthority.PromptAuthorityProjection)
        : PromptAuthority.PromptAuthorityProjection =
        match projection.ActiveLogicalRun with
        | Some profile when
            profile.AuthorityKind = PromptAuthority.RootAuthorityKind.HumanRoot
            && profile.CanonicalRole = Role.Manager
            ->
            closeActiveRun profile projection
        | _ -> projection

    /// PROMPT-005 `Submitted`: the Host call returned a transport receipt.
    ///
    /// The receipt lands on the pending claim. It used to be discarded (`_fact ->
    /// projection`), which erased the distinction PROMPT-011 step 4 vs step 5 is
    /// built on: "the Host accepted something we cannot locate" and "we never got a
    /// receipt at all" both stay pending, but only the first means a logical effect
    /// may already exist.
    let foldPromptSubmitted
        (projection: PromptAuthority.PromptAuthorityProjection)
        (fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               Receipt: TransportReceipt |})
        =
        PromptAuthorityRun.submitClaim fact.PromptKey fact.Receipt projection

    /// PROMPT-005 `PhysicalAccepted`: a real physical message resolved the claim.
    let foldPromptPhysicalAccepted
        (projection: PromptAuthority.PromptAuthorityProjection)
        (fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               PhysicalUserMessageId: PhysicalUserMessageId |})
        =
        PromptAuthorityRun.acceptClaim fact.PromptKey fact.PhysicalUserMessageId projection

    /// PROMPT-005 `Abandoned`. Must not change the Active Logical Run.
    ///
    /// `Reason` is part of the fact's shape, so it appears here even though the
    /// projection does not branch on it: PROMPT-005 keeps the reason on the one
    /// `Abandoned` fact instead of splitting it into a fifth fact name, and an
    /// anonymous record that omitted the field would not accept the payload.
    let foldPromptAbandoned
        (projection: PromptAuthority.PromptAuthorityProjection)
        (fact:
            {| PromptKey: PromptKey
               SessionId: SessionId
               Reason: PromptAbandonReason |})
        =
        PromptAuthorityRun.abandonClaim fact.PromptKey projection
