namespace Wanxiangshu.Mission.Relay.OpenCode

open System
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Mission.Relay
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

module SuicideTool =
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/suicide/relay-description"

        [<Literal>]
        let Blocked = "tool/suicide/relay-blocked"

        [<Literal>]
        let Retired = "tool/suicide/relay-retired"

    let private text path =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) path Map.empty

    let private currentState (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.Relay)

    let private currentView state roadId = state |> Option.bind (fun relay -> Fold.view relay roadId)

    let private incumbentFor sessionText rootAuthorityUserMessageId view =
        view
        |> Option.bind (fun road -> road.ActiveIncumbency)
        |> Option.defaultWith (fun () ->
            HostDigest.sha256Hex ("incumbency-v1\n" + sessionText + "\n" + rootAuthorityUserMessageId)
            |> fun digest -> IncumbencyId.create ("incumbency:" + digest))

    let private deterministicId prefix payload create =
        HostDigest.sha256Hex (prefix + "\n" + payload) |> fun digest -> create (prefix + ":" + digest)

    let private evidenceRefs view =
        match view |> Option.bind (fun road -> road.Certificate) with
        | Some certificate ->
            [ "narrative:" + certificate.Binding.NarrativeDigest
              "requirements:" + certificate.Binding.RequirementSetDigest
              "evidence:" + certificate.Binding.EvidenceFrontierDigest ]
        | None ->
            view
            |> Option.bind (fun road -> road.AcceptedAssessmentTransport)
            |> Option.map (fun (callId, payloadDigest) -> [ "tool-call:" + callId; "payload:" + payloadDigest ])
            |> Option.defaultValue []

    let private baton roadId incumbent authority snapshot view =
        { SchemaVersion = 1
          RoadId = RoadId.value roadId
          FromIncumbencyId = IncumbencyId.value incumbent
          AuthorityRevision = AuthorityRevision.value authority
          SnapshotId = WorkspaceSnapshotId.value snapshot
          OpenObligations =
            view
            |> Option.map (fun road -> road.OpenObligations |> List.map ScoreDimension.fieldName)
            |> Option.defaultValue []
          EvidenceRefs = evidenceRefs view }

    let private qualityCandidate view incumbent snapshot authority =
        view
        |> Option.bind (fun road -> road.Certificate)
        |> Option.filter (fun certificate ->
            certificate.Valid
            && certificate.IncumbencyId = incumbent
            && certificate.SnapshotId = snapshot
            && certificate.AuthorityRevision = authority)

    let private retirementTransaction
        state
        roadId
        incumbent
        providerRun
        toolCallId
        snapshot
        authority
        =
        let view = currentView state roadId
        let candidate = qualityCandidate view incumbent snapshot authority
        let qualityAccepted = candidate |> Option.isSome
        let payload =
            String.concat
                "\n"
                [ RoadId.value roadId
                  IncumbencyId.value incumbent
                  WorkspaceSnapshotId.value snapshot
                  ProviderRunIdentity.value providerRun
                  ToolCallId.value toolCallId ]

        let retirementId = deterministicId "retirement-v1" payload RetirementId.create
        let batonId = deterministicId "baton-v1" payload BatonId.create
        let cutId = deterministicId "projection-cut-v1" payload ProjectionCutId.create
        let envelope = baton roadId incumbent authority snapshot view
        let cut =
            { RetiredIncumbencyId = IncumbencyId.value incumbent
              ThroughProviderRunId = ProviderRunIdentity.value providerRun
              ThroughToolCallId = ToolCallId.value toolCallId
              StaleProviderRunIds = [ ProviderRunIdentity.value providerRun ] }

        let summary =
            { Id = retirementId
              IncumbencyId = incumbent
              SnapshotId = snapshot
              BatonId = batonId
              Baton = envelope
              ProjectionCutId = cutId
              ProjectionCut = cut
              SuccessorRequested = not qualityAccepted
              QualityCandidateAccepted = qualityAccepted }

        let opening =
            match view with
            | Some _ -> []
            | None ->
                [ RelayEvent.RoadOpened(roadId, authority)
                  RelayEvent.IncumbencyOpened(incumbent, snapshot, BatonSource.ExistingWorld) ]

        let invalidation =
            view
            |> Option.bind (fun road -> road.Certificate)
            |> Option.filter (fun certificate -> certificate.Valid && candidate.IsNone)
            |> Option.map (fun certificate ->
                RelayEvent.QualityCertificateInvalidated(certificate.Id, "RetirementBindingChanged"))
            |> Option.toList

        RelayTransaction.create (opening @ invalidation @ [ RelayEvent.RetirementCommitted summary ])

    let private blockedTransaction state roadId incumbent snapshot authority blockerDigest =
        let opening =
            match currentView state roadId with
            | Some _ -> []
            | None ->
                [ RelayEvent.RoadOpened(roadId, authority)
                  RelayEvent.IncumbencyOpened(incumbent, snapshot, BatonSource.ExistingWorld) ]

        RelayTransaction.create (opening @ [ RelayEvent.RetirementCleanupBlocked(incumbent, blockerDigest) ])

    let private append journal sessionId providerRun roadId transaction =
        let fact =
            AgentFact.Relay(
                RelayFactCases.TransactionCommitted
                    {| RoadId = roadId
                       Transaction = transaction |}
            )

        AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) fact journal

    let private blockedResult blockers =
        ToolHostCodec.tomlObjectWithInstructions
            [ text Path.Blocked ]
            [ "retired", ToolHostCodec.TBool false
              "blocker_count", ToolHostCodec.TInt(List.length blockers)
              "blockers",
              ToolHostCodec.TTable(
                  blockers
                  |> List.mapi (fun index blocker -> string index, ToolHostCodec.TString blocker)
              ) ]

    let private retiredResult qualityAccepted successorRequested retirementId =
        ToolHostCodec.tomlObjectWithInstructions
            [ text Path.Retired ]
            [ "retired", ToolHostCodec.TBool true
              "quality_candidate_accepted", ToolHostCodec.TBool qualityAccepted
              "successor_requested", ToolHostCodec.TBool successorRequested
              "retirement_id", ToolHostCodec.TString(RetirementId.value retirementId) ]

    let private execute (scope: ToolRuntimeScope) (_: HostToolArguments) (context: HostToolContext) =
        task {
            match
                scope.RoleFor context,
                context.ToolCallId,
                context.ProviderRunId,
                scope.CurrentPhysicalUserMessage context.SessionId,
                scope.WorkspaceDirectory,
                scope.Journal
            with
            | Some Role.Manager, Some toolCallId, Some providerRun, Some physicalUserMessageId, Some directory, Some journal
                when not (String.IsNullOrWhiteSpace context.SessionId) ->
                let sessionId = SessionId.create context.SessionId
                let roadId = RoadId.create context.SessionId

                try
                    scope.TryFreezeRetirement context.SessionId |> ignore
                    let snapshot = WorkspaceSnapshot.capture directory
                    let state = currentState journal sessionId
                    let view = currentView state roadId
                    let rootAuthorityUserMessageId =
                        scope.ActiveProfileFor sessionId
                        |> Option.map (fun profile ->
                            AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId)
                        |> Option.defaultValue physicalUserMessageId

                    let authority =
                        view
                        |> Option.map (fun road -> road.AuthorityRevision)
                        |> Option.defaultValue (AuthorityRevision.create rootAuthorityUserMessageId)

                    let incumbent = incumbentFor context.SessionId rootAuthorityUserMessageId view
                    let blockers = scope.RetirementBlockersFor context.SessionId

                    if not (List.isEmpty blockers) then
                        let blockerDigest = HostDigest.sha256Hex (String.concat "\n" blockers)

                        match blockedTransaction state roadId incumbent snapshot authority blockerDigest with
                        | Error error ->
                            scope.UnfreezeRetirement context.SessionId
                            return raise (InvalidOperationException error)
                        | Ok transaction ->
                            match! append journal sessionId providerRun roadId transaction with
                            | Ok _ -> return blockedResult blockers
                            | Error failure ->
                                scope.UnfreezeRetirement context.SessionId
                                return raise (InvalidOperationException(JournalAppendFailure.describe failure))
                    else
                        match
                            retirementTransaction
                                state
                                roadId
                                incumbent
                                providerRun
                                toolCallId
                                snapshot
                                authority
                        with
                        | Error error ->
                            scope.UnfreezeRetirement context.SessionId
                            return raise (InvalidOperationException error)
                        | Ok transaction ->
                            match! append journal sessionId providerRun roadId transaction with
                            | Error failure ->
                                scope.UnfreezeRetirement context.SessionId
                                return raise (InvalidOperationException(JournalAppendFailure.describe failure))
                            | Ok projection ->
                                let retirement =
                                    AgentProjection.tryFind sessionId projection.AgentProjections
                                    |> Option.bind (fun session -> session.Relay)
                                    |> Option.bind (fun relay -> Fold.view relay roadId)
                                    |> Option.bind (fun road -> road.LatestRetirement)
                                    |> Option.defaultWith (fun () -> invalidOp "retirement commit did not project")

                                return
                                    retiredResult
                                        retirement.QualityCandidateAccepted
                                        retirement.SuccessorRequested
                                        retirement.Id
                with ex ->
                    if not (scope.ManagerPhaseFor context.SessionId = ManagerCapabilityPhase.Retired) then
                        scope.UnfreezeRetirement context.SessionId

                    return raise ex
            | Some Role.Manager, _, _, _, _, _ ->
                return raise (InvalidOperationException "suicide requires exact session/run/tool/authority/workspace/journal binding")
            | Some _, _, _, _, _, _ -> return raise (InvalidOperationException "suicide is Manager-only")
            | None, _, _, _, _, _ -> return raise (InvalidOperationException "suicide requires an established Manager authority")
        }

    let admission =
        ToolAdmission.OfficeRole(fun _ role -> OfficeCapability.isAllowed role ToolPermission.Finality)

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "suicide"
          Description = text Path.Description
          Arguments = []
          Admission = admission
          Execute = execute scope }
