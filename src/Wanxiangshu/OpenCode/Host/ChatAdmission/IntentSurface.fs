namespace Wanxiangshu.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona

module ChatAdmissionIntentSurface =

    let private optionalString (value: obj) : string option =
        if isNull value then
            None
        else
            let text = string value

            if String.IsNullOrWhiteSpace text then
                None
            else
                Some(text.Trim())

    let private requiredString (name: string) (value: obj) : string =
        optionalString value
        |> Option.defaultWith (fun () -> invalidArg name (name + " must be non-empty"))

    let private rootIdentity (agent: string) : ParticipantIdentityEvidence =
        ParticipantIdentity.resolveAtRoot agent
        |> Result.bind (fun identity ->
            match ParticipantIdentity.role identity with
            | Some _ -> Ok identity
            | None -> Error ParticipantIdentityError.OwnerRequired)
        |> Result.defaultWith (fun _ -> invalidArg "selectedAgent" "selectedAgent must name a managed public agent")

    let private originOf (label: string) : PromptAuthority.PromptOrigin =
        match label with
        | "HumanRoot" -> PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot
        | "AgentOwnerRoot" ->
            PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot
        | "HostInternal" -> PromptAuthority.PromptOrigin.HostInternal
        | "UnknownOrigin" -> PromptAuthority.PromptOrigin.UnknownOrigin
        | continuation ->
            PromptAuthority.tryParseContinuationKind continuation
            |> Option.map PromptAuthority.PromptOrigin.Continuation
            |> Option.defaultWith (fun () -> invalidArg "origin" ("unknown prompt origin: " + continuation))

    let private originName (origin: PromptAuthority.PromptOrigin) : string =
        match origin with
        | PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot -> "HumanRoot"
        | PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot ->
            "AgentOwnerRoot"
        | PromptAuthority.PromptOrigin.Continuation continuation ->
            PromptAuthority.originLabel (PromptAuthority.PromptOrigin.Continuation continuation)
        | PromptAuthority.PromptOrigin.HostInternal -> "HostInternal"
        | PromptAuthority.PromptOrigin.UnknownOrigin -> "UnknownOrigin"

    let private identitySeedName (identitySeed: PromptAuthority.IdentitySeed) : string =
        match identitySeed with
        | PromptAuthority.IdentitySeed.RootSelection _ -> "RootSelection"
        | PromptAuthority.IdentitySeed.InheritedFromOwner _ -> "InheritedFromOwner"

    let private activeProfile
        (snapshot: obj)
        (sessionId: SessionId)
        : PromptAuthority.AuthorityExecutionProfile option =
        optionalString snapshot?activeAgent
        |> Option.map (fun agent ->
            match optionalString snapshot?activeKind with
            | Some "AgentOwnerRoot" ->
                let owner =
                    PromptAuthority.createAuthorityExecutionProfileFromSeed
                        (SessionId.create "ses-surface-owner")
                        (LogicalRunId.create "run-surface-owner")
                        (AuthorityRootUserMessageId.create "root-surface-owner")
                        PromptAuthority.RootAuthorityKind.HumanRoot
                        (PromptAuthority.IdentitySeed.RootSelection(rootIdentity "fast-manager"))
                    |> Result.defaultWith invalidOp

                let inherited =
                    PromptAuthority.issueInheritedIdentitySeed agent owner
                    |> Result.defaultWith (fun _ -> invalidArg "activeAgent" "invalid owner-derived active agent")

                PromptAuthority.createAuthorityExecutionProfileFromSeed
                    sessionId
                    (LogicalRunId.create "run-surface-active")
                    (AuthorityRootUserMessageId.create "root-surface-active")
                    PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                    inherited
                |> Result.defaultWith invalidOp
            | Some "HumanRoot"
            | None ->
                PromptAuthority.createAuthorityExecutionProfileFromSeed
                    sessionId
                    (LogicalRunId.create "run-surface-active")
                    (AuthorityRootUserMessageId.create "root-surface-active")
                    PromptAuthority.RootAuthorityKind.HumanRoot
                    (PromptAuthority.IdentitySeed.RootSelection(rootIdentity agent))
                |> Result.defaultWith invalidOp
            | Some kind -> invalidArg "activeKind" ("unknown active authority kind: " + kind))

    let private claimOf (value: obj) : PromptAuthority.PromptClaim =
        let sessionId = requiredString "claim.sessionId" value?sessionId |> SessionId.create
        let promptKey = requiredString "claim.promptKey" value?promptKey |> PromptKey.create
        let selectedAgent = requiredString "claim.selectedAgent" value?selectedAgent

        let claim: PromptAuthority.PromptClaim =
            { PromptKey = promptKey
              SessionId = sessionId
              Origin = requiredString "claim.origin" value?origin |> originOf
              LogicalRunId = Some(LogicalRunId.create "run-surface-claim")
              AuthorityRootUserMessageId = Some(AuthorityRootUserMessageId.create "root-surface-claim")
              EffectiveAgent = optionalString value?effectiveAgent
              IdentitySeed = PromptAuthority.IdentitySeed.RootSelection(rootIdentity selectedAgent)
              PayloadDigest = "surface-payload"
              Receipt = None
              ClaimedAtRuntimeStartCount = 0 }

        claim

    let private acceptedContinuationOf (value: obj) : PhysicalUserMessageId * PromptAuthority.ContinuationKind =
        let physical =
            requiredString "accepted.physicalUserMessageId" value?physicalUserMessageId
            |> PhysicalUserMessageId.create

        let continuation =
            requiredString "accepted.origin" value?origin
            |> PromptAuthority.tryParseContinuationKind
            |> Option.defaultWith (fun () -> invalidArg "accepted.origin" "accepted origin must be a continuation")

        physical, continuation

    let private authoritySnapshot
        (decoded: ChatAdmissionIntent.DecodedMessage)
        (snapshot: obj)
        : ChatAdmissionIntent.DurableSnapshot =
        if isNull snapshot || snapshot?available = box false then
            { ChatAdmissionIntent.DurableSnapshot.Authority = None }
        else
            let sessionId =
                decoded.SessionId
                |> Option.defaultValue (SessionId.create "surface-missing-session")

            let claims: obj array =
                if isNull snapshot?claims then
                    [||]
                else
                    unbox<obj array> snapshot?claims

            let accepted: obj array =
                if isNull snapshot?acceptedContinuations then
                    [||]
                else
                    unbox<obj array> snapshot?acceptedContinuations

            let projection: PromptAuthority.PromptAuthorityProjection =
                { PromptAuthority.empty with
                    ActiveLogicalRun = activeProfile snapshot sessionId
                    PendingClaims =
                        claims
                        |> Array.map claimOf
                        |> Array.map (fun claim -> claim.PromptKey, claim)
                        |> Map.ofArray
                    AcceptedContinuationIds = accepted |> Array.map acceptedContinuationOf |> Map.ofArray }

            { ChatAdmissionIntent.DurableSnapshot.Authority = Some projection }

    let private decodedMessage (value: obj) : ChatAdmissionIntent.DecodedMessage =
        { SessionId = optionalString value?sessionId |> Option.map SessionId.create
          PhysicalUserMessageId =
            optionalString value?physicalUserMessageId
            |> Option.map PhysicalUserMessageId.create
          ExplicitAgent = optionalString value?explicitAgent
          PromptKey = optionalString value?promptKey |> Option.map PromptKey.create
          IsHostCompaction =
            if isNull value?hostCompaction then
                false
            else
                unbox<bool> value?hostCompaction
          IsHostSynthetic =
            if isNull value?hostSynthetic then
                false
            else
                unbox<bool> value?hostSynthetic
          Text = None }

    let private rejectionName (rejection: ChatAdmissionIntent.Rejection) : string =
        match rejection with
        | ChatAdmissionIntent.Rejection.ManagedIntentMissingSessionId -> "ManagedIntentMissingSessionId"
        | ChatAdmissionIntent.Rejection.ManagedIntentMissingPhysicalUserMessageId ->
            "ManagedIntentMissingPhysicalUserMessageId"
        | ChatAdmissionIntent.Rejection.DurableAuthorityUnavailable -> "DurableAuthorityUnavailable"
        | ChatAdmissionIntent.Rejection.InvalidExplicitAgent _ -> "InvalidExplicitAgent"
        | ChatAdmissionIntent.Rejection.PromptKeyNotClaimed _ -> "PromptKeyNotClaimed"
        | ChatAdmissionIntent.Rejection.AgentOwnerRootPromptNotClaimed _ -> "AgentOwnerRootPromptNotClaimed"
        | ChatAdmissionIntent.Rejection.PromptClaimSessionMismatch _ -> "PromptClaimSessionMismatch"
        | ChatAdmissionIntent.Rejection.PromptClaimMissingManagedEffectiveAgent _ ->
            "PromptClaimMissingManagedEffectiveAgent"
        | ChatAdmissionIntent.Rejection.PromptClaimOriginNotAdmissible _ -> "PromptClaimOriginNotAdmissible"
        | ChatAdmissionIntent.Rejection.UnknownOriginWhileActive -> "UnknownOriginWhileActive"

    let resolve (message: obj) (durableSnapshot: obj) : obj =
        let decoded = decodedMessage message

        match ChatAdmissionIntent.resolve decoded (authoritySnapshot decoded durableSnapshot) with
        | ChatAdmissionIntent.Decision.NoManagedExecution ChatAdmissionIntent.NoManagedExecutionReason.UnmanagedMessage ->
            box
                {| ``case`` = "NoManagedExecution"
                   reason = "UnmanagedMessage" |}
        | ChatAdmissionIntent.Decision.NoManagedExecution(ChatAdmissionIntent.NoManagedExecutionReason.AlreadyAcceptedHostMessage continuation) ->
            box
                {| ``case`` = "NoManagedExecution"
                   reason = "AlreadyAcceptedHostMessage"
                   origin = originName (PromptAuthority.PromptOrigin.Continuation continuation) |}
        | ChatAdmissionIntent.Decision.ExternalRootIntent evidence ->
            box
                {| ``case`` = "ExternalRootIntent"
                   sessionId = SessionId.value evidence.Key.SessionId
                   physicalUserMessageId = PhysicalUserMessageId.value evidence.Key.PhysicalUserMessageId
                   explicitAgent = evidence.ExplicitAgent
                   effectiveAgent = evidence.EffectiveAgent
                   origin = originName evidence.Origin
                   identitySeed = identitySeedName evidence.IdentitySeed
                   selectedAgent =
                    evidence.IdentitySeed
                    |> PromptAuthority.identitySeedParticipantIdentity
                    |> ParticipantIdentity.selectedAgent |}
        | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent evidence ->
            box
                {| ``case`` = "ActiveHumanContinuationIntent"
                   sessionId = SessionId.value evidence.Key.SessionId
                   physicalUserMessageId = PhysicalUserMessageId.value evidence.Key.PhysicalUserMessageId
                   effectiveAgent = evidence.EffectiveAgent
                   origin = originName evidence.Origin
                   selectedAgent = evidence.Authority.SelectedAgent |}
        | ChatAdmissionIntent.Decision.PendingPromptIntent evidence ->
            box
                {| ``case`` = "PendingPromptIntent"
                   sessionId = SessionId.value evidence.Key.SessionId
                   physicalUserMessageId = PhysicalUserMessageId.value evidence.Key.PhysicalUserMessageId
                   promptKey = PromptKey.value evidence.PromptKey
                   effectiveAgent = evidence.EffectiveAgent
                   origin = originName evidence.Origin
                   identitySeed = identitySeedName evidence.IdentitySeed
                   selectedAgent =
                    evidence.IdentitySeed
                    |> PromptAuthority.identitySeedParticipantIdentity
                    |> ParticipantIdentity.selectedAgent |}
        | ChatAdmissionIntent.Decision.HostInternal evidence ->
            box
                {| ``case`` = "HostInternal"
                   origin = originName evidence.Origin |}
        | ChatAdmissionIntent.Decision.Reject rejection ->
            box
                {| ``case`` = "Reject"
                   reason = rejectionName rejection |}
