namespace Wanxiangshu.Persistence.Journal

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Change
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona

/// JS-native owner surface for decode-only journal fact compatibility.
/// Decoded facts remain inside the production codec; callers observe bytes and
/// explicit decode outcomes only.
[<RequireQualifiedAccess>]
module FactCodecSurface =

    let pre050MigrationMessage = FactCodec.pre050MigrationMessage

    let private text (value: obj) =
        if isNull value then "" else string value

    let private optionalString (value: obj) =
        if isNull value then None else Some(text value)

    let private handleOf (value: obj) =
        HandleId.Agent(AgentHandleId.create (text value))

    let private completionKindOf (value: obj) =
        match text value with
        | "Terminal" -> HandleCompletionKind.Terminal
        | "SendFailure" -> HandleCompletionKind.SendFailure
        | "Cancelled" -> HandleCompletionKind.Cancelled
        | other -> failwith $"FactCodecSurface: unknown completion kind '{other}'"

    let private abandonReasonOf (value: obj) =
        match text value with
        | "ParentCancelled" -> HandleAbandonReason.ParentCancelled
        | "DeadlineExceeded" -> HandleAbandonReason.DeadlineExceeded
        | "HostSessionGone" -> HandleAbandonReason.HostSessionGone
        | other -> failwith $"FactCodecSurface: unknown abandon reason '{other}'"

    let private roleOf (value: obj) =
        AgentRoleIdentity.roleOfString (text value)
        |> Option.defaultWith (fun () -> failwith $"FactCodecSurface: unknown role '{text value}'")

    let private ownershipOf (value: obj) =
        match text value with
        | "HostOwnedHidden" -> HandleOwnership.HostOwnedHidden
        | "DurableParentHandle" -> HandleOwnership.DurableParentHandle
        | other -> failwith $"FactCodecSurface: unknown ownership '{other}'"

    let private tierOf (value: obj) =
        ManagedAgentCatalog.tryParseTier (text value)
        |> Option.defaultWith (fun () -> failwith $"FactCodecSurface: unknown tier '{text value}'")

    let private originOf (value: obj) =
        match text value with
        | "ResolvedAtRoot" -> PersonaOrigin.ResolvedAtRoot
        | "InheritedFromOwner" -> PersonaOrigin.InheritedFromOwner
        | other -> failwith $"FactCodecSurface: unknown persona origin '{other}'"

    let private identityInputOfJs (value: obj) =
        { SelectedAgent = text (value?selectedAgent)
          PeerAgent = text (value?peerAgent)
          Role =
            if text (value?canonicalRole) = "bookkeeper" then
                None
            else
                Some(roleOf (value?canonicalRole))
          InitialTier = tierOf (value?selectedTier)
          Persona = text (value?persona)
          PersonaCatalogVersion = unbox<int> (value?personaCatalogVersion)
          Origin = originOf (value?origin) }

    let private identitySeedOfJs (value: obj) =
        let identityInput = identityInputOfJs (value?participantIdentity)

        match text (value?kind) with
        | "RootSelection" -> PromptAuthority.IdentitySeedInput.RootSelectionInput identityInput
        | "InheritedFromOwner" ->
            PromptAuthority.IdentitySeedInput.InheritedFromOwnerInput
                { OwnerSessionId = SessionId.create (text (value?ownerSession))
                  OwnerLogicalRunId = LogicalRunId.create (text (value?ownerLogicalRun))
                  OwnerAuthorityRootUserMessageId = AuthorityRootUserMessageId.create (text (value?ownerAuthorityRoot))
                  ParticipantIdentity = identityInput }
        | other -> failwith $"FactCodecSurface: unknown identity seed '{other}'"
        |> PromptAuthority.rehydrateIdentitySeed
        |> Result.defaultWith (fun error -> failwith $"FactCodecSurface: invalid identity seed: {error}")

    let private identityToJs evidence =
        box
            {| selectedAgent = ParticipantIdentity.selectedAgent evidence
               peerAgent = ParticipantIdentity.peerAgent evidence
               canonicalRole = ParticipantIdentity.roleLabel evidence
               selectedTier = ParticipantIdentity.initialTier evidence |> Roles.wireTierLabel
               persona = ParticipantIdentity.persona evidence
               personaCatalogVersion = ParticipantIdentity.personaCatalogVersion evidence
               origin =
                match ParticipantIdentity.origin evidence with
                | PersonaOrigin.ResolvedAtRoot -> "ResolvedAtRoot"
                | PersonaOrigin.InheritedFromOwner -> "InheritedFromOwner" |}

    let private identitySeedToJs seed =
        let participantIdentity =
            PromptAuthority.identitySeedParticipantIdentity seed |> identityToJs

        match PromptAuthority.identitySeedOwner seed with
        | None ->
            box
                {| kind = "RootSelection"
                   ownerSession = null
                   ownerLogicalRun = null
                   ownerAuthorityRoot = null
                   participantIdentity = participantIdentity |}
        | Some(ownerSession, ownerLogicalRun, ownerAuthorityRoot) ->
            box
                {| kind = "InheritedFromOwner"
                   ownerSession = SessionId.value ownerSession
                   ownerLogicalRun = LogicalRunId.value ownerLogicalRun
                   ownerAuthorityRoot = AuthorityRootUserMessageId.value ownerAuthorityRoot
                   participantIdentity = participantIdentity |}

    let private factOfJs (value: obj) : Fact =
        let family = text (value?family)
        let case = text (value?case)
        let payload = unbox<obj> (value?payload)

        match family, case with
        | "Prompt", "AuthorityRootAccepted" ->
            Fact.Agent(
                AgentFact.Prompt(
                    PromptFactCases.AuthorityRootAccepted
                        { SchemaVersion = unbox<int> (payload?SchemaVersion)
                          SessionId = SessionId.create (text (payload?SessionId))
                          LogicalRunId = LogicalRunId.create (text (payload?LogicalRunId))
                          AuthorityRootUserMessageId =
                            AuthorityRootUserMessageId.create (text (payload?AuthorityRootUserMessageId))
                          AuthorityKind = text (payload?AuthorityKind)
                          IdentitySeed = identitySeedOfJs (payload?IdentitySeed) }
                )
            )
        | "Runtime", "RuntimeStarted" ->
            Fact.Runtime(
                RuntimeStarted
                    {| RuntimeId = RuntimeId.create (text (payload?RuntimeId))
                       ProcessId = unbox<int> (payload?ProcessId)
                       StartedAt = DateTimeOffset.Parse(text (payload?StartedAt)) |}
            )
        | "Execution", "HandleAbandoned" ->
            Fact.Agent(
                ExecutionFact.HandleAbandoned
                    {| ParentSessionId = SessionId.create (text (payload?ParentSessionId))
                       Handle = handleOf (payload?Handle)
                       Reason = abandonReasonOf (payload?Reason)
                       AbandonedAt = DateTimeOffset.Parse(text (payload?AbandonedAt)) |}
            )
        | "Execution", "HandleCompleted" ->
            Fact.Agent(
                ExecutionFact.HandleCompleted
                    {| ParentSessionId = SessionId.create (text (payload?ParentSessionId))
                       Handle = handleOf (payload?Handle)
                       Kind = completionKindOf (payload?Kind)
                       CompletionRef = optionalString (payload?CompletionRef) |> Option.map BlobRef.create
                       CompletionDigest = optionalString (payload?CompletionDigest) |> Option.map BlobDigest.create |}
            )
        | "Execution", "HandleLinked" ->
            Fact.Agent(
                ExecutionFact.HandleLinked
                    {| ParentSessionId = SessionId.create (text (payload?ParentSessionId))
                       ChildSessionId = SessionId.create (text (payload?ChildSessionId))
                       Handle = handleOf (payload?Handle)
                       TargetAgent = text (payload?TargetAgent)
                       Byname = text (payload?Byname)
                       CanonicalRole = roleOf (payload?CanonicalRole)
                       Ownership = ownershipOf (payload?Ownership) |}
            )
        | "Orchestrator", "WorktreeCreateRequested" ->
            Fact.Agent(
                AgentFact.Orchestrator(
                    OrchestratorFactCases.WorktreeCreateRequested
                        {| ManagerJobId = ManagerJobId.create (text (payload?ManagerJobId))
                           WorktreeIdentity = WorktreeIdentity.create (text (payload?WorktreeIdentity))
                           WorktreePath = WorktreePath.create (text (payload?WorktreePath)) |}
                )
            )
        | "Orchestrator", "WorktreeCreated" ->
            Fact.Agent(
                AgentFact.Orchestrator(
                    OrchestratorFactCases.WorktreeCreated
                        {| ManagerJobId = ManagerJobId.create (text (payload?ManagerJobId))
                           WorktreeIdentity = WorktreeIdentity.create (text (payload?WorktreeIdentity))
                           WorktreePath = WorktreePath.create (text (payload?WorktreePath)) |}
                )
            )
        | "Orchestrator", "PublishClaimed" ->
            Fact.Agent(
                AgentFact.Orchestrator(
                    OrchestratorFactCases.PublishClaimed
                        {| ManagerJobId = ManagerJobId.create (text (payload?ManagerJobId))
                           TargetRef = TargetRef.create (text (payload?TargetRef))
                           ExpectedHead = CommitHash.create (text (payload?ExpectedHead)) |}
                )
            )
        | "Orchestrator", "ManagerJobCreated" ->
            Fact.Agent(
                AgentFact.Orchestrator(
                    OrchestratorFactCases.ManagerJobCreated
                        {| ManagerJobId = ManagerJobId.create (text (payload?ManagerJobId))
                           ManagerSessionId = SessionId.create (text (payload?ManagerSessionId))
                           ManagerAgent = text (payload?ManagerAgent)
                           Byname = text (payload?Byname)
                           WorktreeIdentity = WorktreeIdentity.create (text (payload?WorktreeIdentity))
                           WorktreePath = WorktreePath.create (text (payload?WorktreePath))
                           TargetRef = TargetRef.create (text (payload?TargetRef))
                           TargetBranchFrozen = text (payload?TargetBranchFrozen) |}
                )
            )
        | "Orchestrator", "RebasedCandidateReady" ->
            Fact.Agent(
                AgentFact.Orchestrator(
                    OrchestratorFactCases.RebasedCandidateReady
                        {| ManagerJobId = ManagerJobId.create (text (payload?ManagerJobId))
                           RebasedCommit = CommitHash.create (text (payload?RebasedCommit))
                           TargetHeadSnapshot = CommitHash.create (text (payload?TargetHeadSnapshot))
                           PostRebaseReviewBarrierId = ReviewBarrierId.create (text (payload?PostRebaseReviewBarrierId)) |}
                )
            )
        | "Orchestrator", "Published" ->
            Fact.Agent(
                AgentFact.Orchestrator(
                    OrchestratorFactCases.Published
                        {| ManagerJobId = ManagerJobId.create (text (payload?ManagerJobId))
                           CandidateCommit = CommitHash.create (text (payload?CandidateCommit))
                           ResultingTargetHead = CommitHash.create (text (payload?ResultingTargetHead)) |}
                )
            )
        | "Orchestrator", "JobFailed" ->
            Fact.Agent(
                AgentFact.Orchestrator(
                    OrchestratorFactCases.JobFailed
                        {| ManagerJobId = ManagerJobId.create (text (payload?ManagerJobId))
                           Reason = text (payload?Reason) |}
                )
            )
        | "Orchestrator", "JobAbandoned" ->
            Fact.Agent(
                AgentFact.Orchestrator(
                    OrchestratorFactCases.JobAbandoned
                        {| ManagerJobId = ManagerJobId.create (text (payload?ManagerJobId)) |}
                )
            )
        | familyName, caseName -> failwith $"FactCodecSurface: unknown fact '{familyName}.{caseName}'"

    let private factToJs (fact: Fact) : obj =
        match fact with
        | Fact.Agent(AgentFact.Prompt(PromptFactCases.AuthorityRootAccepted payload)) ->
            box
                {| family = "Prompt"
                   case = "AuthorityRootAccepted"
                   payload =
                    {| SchemaVersion = payload.SchemaVersion
                       SessionId = SessionId.value payload.SessionId
                       LogicalRunId = LogicalRunId.value payload.LogicalRunId
                       AuthorityRootUserMessageId = AuthorityRootUserMessageId.value payload.AuthorityRootUserMessageId
                       AuthorityKind = payload.AuthorityKind
                       IdentitySeed = identitySeedToJs payload.IdentitySeed |} |}
        | _ ->
            box
                {| family = "Unknown"
                   case = "Unknown"
                   payload = box {| |} |}

    let private caseOfFact (fact: Fact) : string =
        match fact with
        | Fact.Agent(AgentFact.Prompt(PromptFactCases.AuthorityRootAccepted _)) -> "AuthorityRootAccepted"
        | Fact.Runtime(RuntimeStarted _) -> "RuntimeStarted"
        | Fact.Agent(AgentFact.Execution(ExecutionFactCases.HandleAbandoned _)) -> "HandleAbandoned"
        | Fact.Agent(AgentFact.Execution(ExecutionFactCases.HandleCompleted _)) -> "HandleCompleted"
        | Fact.Agent(AgentFact.Orchestrator(OrchestratorFactCases.WorktreeCreateRequested _)) ->
            "WorktreeCreateRequested"
        | Fact.Agent(AgentFact.Orchestrator(OrchestratorFactCases.WorktreeCreated _)) -> "WorktreeCreated"
        | Fact.Agent(AgentFact.Orchestrator(OrchestratorFactCases.PublishClaimed _)) -> "PublishClaimed"
        | Fact.Agent(AgentFact.Orchestrator(OrchestratorFactCases.ManagerJobCreated _)) -> "ManagerJobCreated"
        | Fact.Agent(AgentFact.Orchestrator(OrchestratorFactCases.RebasedCandidateReady _)) -> "RebasedCandidateReady"
        | Fact.Agent(AgentFact.Orchestrator(OrchestratorFactCases.Published _)) -> "Published"
        | Fact.Agent(AgentFact.Orchestrator(OrchestratorFactCases.JobFailed _)) -> "JobFailed"
        | Fact.Agent(AgentFact.Orchestrator(OrchestratorFactCases.JobAbandoned _)) -> "JobAbandoned"
        | _ -> "Unknown"

    let containsLegacyFallbackFields (line: string) =
        FactCodec.containsLegacyFallbackFields line

    let containsLegacyScoreVectorEntry (line: string) =
        FactCodec.containsLegacyScoreVectorEntry line

    /// Encode one JS-native fact to canonical fact bytes.
    let encode (fact: obj) : string =
        factOfJs fact |> FactCodec.serializeFact

    /// Decode one line and return normalized bytes plus its semantic case.
    let decode (line: string) : obj =
        match FactCodec.deserializeFact line with
        | Ok fact ->
            let descriptor = factToJs fact

            box
                {| ok = true
                   line = FactCodec.serializeFact fact
                   case = caseOfFact fact
                   payload = descriptor?payload |}
        | Error error -> box {| ok = false; error = error |}
