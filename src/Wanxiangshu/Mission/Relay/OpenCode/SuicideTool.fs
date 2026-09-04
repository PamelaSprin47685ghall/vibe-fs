namespace Wanxiangshu.Mission.Relay.OpenCode

open System
open System.Threading.Tasks
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

        [<Literal>]
        let NonManagerRole = "tool/suicide/non-manager-role"

        [<Literal>]
        let NoAuthority = "tool/suicide/no-authority"

        [<Literal>]
        let NoRetirementProjection = "tool/suicide/no-retirement-projection"

    let private text path =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) path Map.empty

    let private currentState (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.Relay)

    let private currentView state roadId =
        state |> Option.bind (fun relay -> Fold.view relay roadId)

    let private incumbentFor sessionText rootAuthorityUserMessageId view =
        view
        |> Option.bind (fun road -> road.ActiveIncumbency)
        |> Option.defaultWith (fun () ->
            HostDigest.sha256Hex ("incumbency-v1\n" + sessionText + "\n" + rootAuthorityUserMessageId)
            |> fun digest -> IncumbencyId.create ("incumbency:" + digest))

    let private deterministicId prefix payload create =
        HostDigest.sha256Hex (prefix + "\n" + payload)
        |> fun digest -> create (prefix + ":" + digest)

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
        authorityMessageId
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
                [ RelayEvent.RoadOpened(roadId, authority, authorityMessageId)
                  RelayEvent.IncumbencyOpened(incumbent, snapshot, BatonSource.ExistingWorld) ]

        let invalidation =
            view
            |> Option.bind (fun road -> road.Certificate)
            |> Option.filter (fun certificate -> certificate.Valid && candidate.IsNone)
            |> Option.map (fun certificate ->
                RelayEvent.QualityCertificateInvalidated(certificate.Id, "RetirementBindingChanged"))
            |> Option.toList

        RelayTransaction.create (opening @ invalidation @ [ RelayEvent.RetirementCommitted summary ])

    let private blockedTransaction state roadId incumbent snapshot authority authorityMessageId blockerDigest =
        let opening =
            match currentView state roadId with
            | Some _ -> []
            | None ->
                [ RelayEvent.RoadOpened(roadId, authority, authorityMessageId)
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

    let private requireSome error =
        function
        | Some value -> Ok value
        | None -> Error error

    let private bindTaskResult binder pending =
        task {
            let! outcome = pending

            match outcome with
            | Ok value -> return! binder value
            | Error error -> return Error error
        }

    let private bindResultTask binder result =
        match result with
        | Ok value -> binder value
        | Error error -> Task.FromResult(Error error)

    type private BoundRetirement =
        { ToolCallId: ToolCallId
          ProviderRun: ProviderRunIdentity
          PhysicalUserMessageId: string
          Directory: string
          Journal: AgentJournal }

    type private PreparedRetirement =
        { Bound: BoundRetirement
          SessionId: SessionId
          RoadId: RoadId
          Snapshot: WorkspaceSnapshotId
          State: RelayState option
          View: RoadView option
          Authority: AuthorityRevision
          AuthorityMessageId: PhysicalUserMessageId
          Incumbent: IncumbencyId }

    let private bindInvocation (scope: ToolRuntimeScope) (context: HostToolContext) =
        match
            scope.RoleFor context,
            context.ToolCallId,
            context.ProviderRunId,
            scope.CurrentPhysicalUserMessage context.SessionId,
            scope.WorkspaceDirectory,
            scope.Journal
        with
        | Some Role.Manager, Some toolCallId, Some providerRun, Some physicalUserMessageId, Some directory, Some journal when
            not (String.IsNullOrWhiteSpace context.SessionId)
            ->
            Ok
                { ToolCallId = toolCallId
                  ProviderRun = providerRun
                  PhysicalUserMessageId = physicalUserMessageId
                  Directory = directory
                  Journal = journal }
        | Some Role.Manager, _, _, _, _, _ ->
            Error "suicide requires exact session/run/tool/authority/workspace/journal binding"
        | Some _, _, _, _, _, _ -> Error(text Path.NonManagerRole)
        | None, _, _, _, _, _ -> Error(text Path.NoAuthority)

    let private prepareRetirement (scope: ToolRuntimeScope) (context: HostToolContext) (bound: BoundRetirement) =
        let sessionId = SessionId.create context.SessionId
        let roadId = RoadId.create context.SessionId
        let snapshot = WorkspaceSnapshot.capture bound.Directory
        let state = currentState bound.Journal sessionId
        let view = currentView state roadId

        let rootAuthorityUserMessageId =
            scope.ActiveProfileFor sessionId
            |> Option.map (fun profile -> AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId)
            |> Option.defaultValue bound.PhysicalUserMessageId

        let authority =
            view
            |> Option.map (fun road -> road.AuthorityRevision)
            |> Option.defaultValue (AuthorityRevision.create rootAuthorityUserMessageId)

        { Bound = bound
          SessionId = sessionId
          RoadId = roadId
          Snapshot = snapshot
          State = state
          View = view
          Authority = authority
          AuthorityMessageId = PhysicalUserMessageId.create rootAuthorityUserMessageId
          Incumbent = incumbentFor context.SessionId rootAuthorityUserMessageId view }

    let private appendPrepared (prepared: PreparedRetirement) transaction =
        task {
            let! outcome =
                append prepared.Bound.Journal prepared.SessionId prepared.Bound.ProviderRun prepared.RoadId transaction

            return outcome |> Result.mapError JournalAppendFailure.describe
        }

    let private retirementFromProjection (prepared: PreparedRetirement) projection =
        AgentProjection.tryFind prepared.SessionId projection.AgentProjections
        |> Option.bind (fun session -> session.Relay)
        |> Option.bind (fun relay -> Fold.view relay prepared.RoadId)
        |> Option.bind (fun road -> road.LatestRetirement)
        |> requireSome (text Path.NoRetirementProjection)

    let private runBlocked (prepared: PreparedRetirement) blockers =
        let blockerDigest = HostDigest.sha256Hex (String.concat "\n" blockers)

        blockedTransaction
            prepared.State
            prepared.RoadId
            prepared.Incumbent
            prepared.Snapshot
            prepared.Authority
            prepared.AuthorityMessageId
            blockerDigest
        |> bindResultTask (fun transaction ->
            appendPrepared prepared transaction
            |> bindTaskResult (fun _ -> Task.FromResult(Ok(blockedResult blockers))))

    let private runRetirement (prepared: PreparedRetirement) =
        retirementTransaction
            prepared.State
            prepared.RoadId
            prepared.Incumbent
            prepared.Bound.ProviderRun
            prepared.Bound.ToolCallId
            prepared.Snapshot
            prepared.Authority
            prepared.AuthorityMessageId
        |> bindResultTask (fun transaction ->
            appendPrepared prepared transaction
            |> bindTaskResult (fun projection ->
                retirementFromProjection prepared projection
                |> Result.map (fun retirement ->
                    retiredResult retirement.QualityCandidateAccepted retirement.SuccessorRequested retirement.Id)
                |> Task.FromResult))

    let private runFrozen (scope: ToolRuntimeScope) (context: HostToolContext) (prepared: PreparedRetirement) =
        let blockers = scope.RetirementBlockersFor context.SessionId

        if List.isEmpty blockers then
            runRetirement prepared
        else
            runBlocked prepared blockers

    let private unfreezeUnlessRetired (scope: ToolRuntimeScope) (context: HostToolContext) =
        if scope.ManagerPhaseFor context.SessionId <> ManagerCapabilityPhase.Retired then
            scope.UnfreezeRetirement context.SessionId

    let private finishOutcome (scope: ToolRuntimeScope) (context: HostToolContext) outcome =
        match outcome with
        | Ok value -> value
        | Error error ->
            scope.UnfreezeRetirement context.SessionId
            raise (InvalidOperationException error)

    let private executePrepared (scope: ToolRuntimeScope) (context: HostToolContext) prepared =
        task {
            try
                scope.TryFreezeRetirement context.SessionId |> ignore
                let! outcome = runFrozen scope context prepared
                return finishOutcome scope context outcome
            with ex ->
                unfreezeUnlessRetired scope context
                return raise ex
        }

    let private execute (scope: ToolRuntimeScope) (_: HostToolArguments) (context: HostToolContext) =
        match bindInvocation scope context |> Result.map (prepareRetirement scope context) with
        | Ok prepared -> executePrepared scope context prepared
        | Error error -> task { return raise (InvalidOperationException error) }

    let admission =
        ToolAdmission.OfficeRole(fun _ role -> OfficeCapability.isAllowed role ToolPermission.Finality)

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "suicide"
          Description = text Path.Description
          Arguments = []
          Admission = admission
          Execute = execute scope }
