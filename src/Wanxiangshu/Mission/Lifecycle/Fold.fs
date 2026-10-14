namespace Wanxiangshu.Mission.Lifecycle

type ReviewStamp =
    { Stage: StageElement
      Outcome: ReviewOutcome }

type ActiveIncumbency =
    { Id: string
      OpeningStage: StageElement
      OpeningCursor: int64
      Review: ReviewStamp option
      Confirmed: (string * string) option
      CleanupBlockerDigest: string option }

type RetirementRecord =
    { RetirementId: string
      IncumbencyId: string
      Outcome: RetirementOutcome
      ExpectedAdvance: string
      ProviderRunId: string option
      ToolCallId: string option
      OpeningCursor: int64
      RetirementCursor: int64 option
      ProjectionCut: ProjectionCut option
      ExecSessionId: string option
      Digest: string option
      Path: string option
      Stage: StageElement option
      Review: ReviewStamp option }

type LifecycleWork =
    { WorkId: string
      Root: string
      Sequence: WorkSequence
      Authority: string
      AdvanceHistory: string list
      Active: ActiveIncumbency option
      CurrentDelivered: bool
      CurrentRelayed: bool
      PendingRelay: bool
      DeliveredRecord: RetirementRecord option
      DevOps: (string * string option) option
      Retirements: RetirementRecord list }

[<RequireQualifiedAccess>]
type RecoveryPosition =
    | PairIncomplete
    | Rejected
    | Conflict
    | DeliveredIdempotent
    | ActiveRebound
    | Nothing

[<RequireQualifiedAccess>]
type RecoveryNext =
    | StopPairIncomplete
    | StopRejected
    | StopConflict
    | StopDelivered
    | StopNothing
    | WaitForUserPrompt
    | EnterSuccessorPrompt
    | EnterDispatch

type FoldedWork =
    { Work: LifecycleWork
      Projection: StageElement
      DeliveryAuthorized: bool }

[<RequireQualifiedAccess>]
module LifecycleFold =

    let fresh (workId: string) (root: string) (sequence: WorkSequence) (authority: string) : LifecycleWork =
        { WorkId = workId
          Root = root
          Sequence = sequence
          Authority = authority
          AdvanceHistory = []
          Active = None
          CurrentDelivered = false
          CurrentRelayed = false
          PendingRelay = false
          DeliveredRecord = None
          DevOps = None
          Retirements = [] }

    let private elements (work: LifecycleWork) = WorkSequence.elements work.Sequence

    let projection (work: LifecycleWork) : Result<StageElement * bool, string> =
        match elements work with
        | [] -> Error "sequence is empty"
        | head :: _ as stages ->
            match work.Active with
            | None -> Ok(head, false)
            | Some active ->
                match active.Review with
                | None -> Ok(active.OpeningStage, false)
                | Some review ->
                    match List.tryFindIndex ((=) review.Stage) stages with
                    | None -> Error "review stage is not in the sequence"
                    | Some index ->
                        let last = index = stages.Length - 1

                        match review.Outcome, last, work.Sequence with
                        | ReviewOutcome.Stayed, _, _ -> Ok(review.Stage, false)
                        | ReviewOutcome.Promoted, false, _ -> Ok(stages.[index + 1], false)
                        | ReviewOutcome.Promoted, true, WorkSequence.Plan -> Ok(review.Stage, true)
                        | ReviewOutcome.Promoted, true, WorkSequence.Execution -> Ok(review.Stage, false)

    let private retirementFromActive
        (work: LifecycleWork)
        (active: ActiveIncumbency)
        (retirementId: string)
        (outcome: RetirementOutcome)
        (expectedAdvance: string)
        (providerRunId: string option)
        (toolCallId: string option)
        (cursor: int64 option)
        (cut: ProjectionCut option)
        (execSessionId: string option)
        (digest: string option)
        (path: string option)
        =
        match projection work with
        | Error error -> Error error
        | Ok(stage, _) ->
            Ok
                { RetirementId = retirementId
                  IncumbencyId = active.Id
                  Outcome = outcome
                  ExpectedAdvance = expectedAdvance
                  ProviderRunId = providerRunId
                  ToolCallId = toolCallId
                  OpeningCursor = active.OpeningCursor
                  RetirementCursor = cursor
                  ProjectionCut = cut
                  ExecSessionId = execSessionId
                  Digest = digest
                  Path = path
                  Stage = Some stage
                  Review = active.Review }

    let private retire (work: LifecycleWork) (record: RetirementRecord) =
        let relayed = record.Outcome = RetirementOutcome.Relayed

        { work with
            Active = None
            PendingRelay = relayed
            CurrentDelivered = record.Outcome = RetirementOutcome.Delivered
            CurrentRelayed = false
            DeliveredRecord =
                if record.Outcome = RetirementOutcome.Delivered then
                    Some record
                else
                    work.DeliveredRecord
            Retirements = work.Retirements @ [ record ] }

    let apply (event: LifecycleEvent) (work: LifecycleWork) : Result<LifecycleWork, string> =
        match event with
        | LifecycleEvent.WorkOpened(workId, _, _, _) when work.WorkId = workId -> Error "second WorkOpened"
        | LifecycleEvent.WorkOpened _ -> Error "WorkOpened workId mismatch"
        | LifecycleEvent.DevOpsBound(workId, devopsId, target) ->
            if workId <> work.WorkId then
                Error "DevOpsBound workId mismatch"
            elif work.DevOps.IsSome then
                Ok work
            else
                Ok
                    { work with
                        DevOps = Some(devopsId, target) }
        | LifecycleEvent.IncumbencyOpened(workId, incumbencyId, stage, openingCursor) ->
            if workId <> work.WorkId then
                Error "IncumbencyOpened workId mismatch"
            else
                match work.Active with
                | Some active when active.Id = incumbencyId && active.OpeningCursor = openingCursor -> Ok work
                | Some _ -> Error "active incumbency already exists"
                | None when work.PendingRelay ->
                    Ok
                        { work with
                            PendingRelay = false
                            CurrentRelayed = true
                            CurrentDelivered = false
                            Active =
                                Some
                                    { Id = incumbencyId
                                      OpeningStage = stage
                                      OpeningCursor = openingCursor
                                      Review = None
                                      Confirmed = None
                                      CleanupBlockerDigest = None } }
                | None when work.Retirements.IsEmpty && not work.CurrentDelivered ->
                    match elements work with
                    | head :: _ when head = stage ->
                        Ok
                            { work with
                                Active =
                                    Some
                                        { Id = incumbencyId
                                          OpeningStage = stage
                                          OpeningCursor = openingCursor
                                          Review = None
                                          Confirmed = None
                                          CleanupBlockerDigest = None } }
                    | _ -> Error "first incumbency stage is not the first sequence element"
                | None -> Error "incumbency opening is not a first tenure or a relay successor"
        | LifecycleEvent.ConfirmationCommitted(incumbencyId, providerRunId, toolCallId) ->
            match work.Active with
            | Some active when active.Id = incumbencyId && active.Confirmed.IsNone ->
                Ok
                    { work with
                        Active =
                            Some
                                { active with
                                    Confirmed = Some(providerRunId, toolCallId) } }
            | Some active when active.Id = incumbencyId -> Ok work
            | _ -> Error "confirmation without this active incumbency"
        | LifecycleEvent.IncumbencyRetiredOneshot retired ->
            match work.Active with
            | Some active when active.Id = retired.IncumbencyId ->
                retirementFromActive
                    work
                    active
                    retired.RetirementId
                    retired.Outcome
                    retired.ExpectedAdvance
                    (Some retired.ProviderRunId)
                    (Some retired.ToolCallId)
                    (Some retired.RetirementCursor)
                    None
                    retired.ExecSessionId
                    retired.Digest
                    retired.Path
                |> Result.map (retire work)
            | _ -> Error "oneshot retirement without this active incumbency"
        | LifecycleEvent.IncumbencyRetiredExecution retired ->
            match work.Active with
            | Some active when active.Id = retired.IncumbencyId ->
                retirementFromActive
                    work
                    active
                    retired.RetirementId
                    retired.Outcome
                    retired.ExpectedAdvance
                    (Some retired.ProjectionCut.ProviderRunId)
                    (Some retired.ProjectionCut.ToolCallId)
                    None
                    (Some retired.ProjectionCut)
                    None
                    None
                    None
                |> Result.map (retire work)
            | _ -> Error "execution retirement without this active incumbency"
        | LifecycleEvent.Reviewed(incumbencyId, stage, outcome) ->
            match work.Active with
            | Some active when active.Id = incumbencyId && active.Review.IsNone ->
                if not (List.contains stage (elements work)) then
                    Error "review stage is not in the sequence"
                else
                    Ok
                        { work with
                            Active =
                                Some
                                    { active with
                                        Review = Some { Stage = stage; Outcome = outcome } } }
            | Some active when
                active.Id = incumbencyId
                && active.Review = Some { Stage = stage; Outcome = outcome }
                ->
                Ok work
            | Some _ -> Error "incumbency already reviewed"
            | None -> Error "review without active incumbency"
        | LifecycleEvent.Reopened incumbencyId ->
            match work.CurrentDelivered, work.Active, work.DeliveredRecord with
            | true, None, Some record when record.IncumbencyId = incumbencyId ->
                match record.Stage, record.Review with
                | Some stage, review ->
                    Ok
                        { work with
                            CurrentDelivered = false
                            CurrentRelayed = false
                            Active =
                                Some
                                    { Id = incumbencyId
                                      OpeningStage = stage
                                      OpeningCursor = 0L
                                      Review = review
                                      Confirmed = None
                                      CleanupBlockerDigest = None } }
                | None, _ -> Error "reopen authorization cannot be read"
            | _ -> Error "reopen without a matching delivered retirement and no active tenure"
        | LifecycleEvent.AdvanceCommitted(incumbencyId, expected, next) ->
            match work.Active with
            | None -> Error "advance without active incumbency"
            | Some active when active.Id <> incumbencyId -> Error "advance incumbency mismatch"
            | Some active when active.Review.IsNone -> Error "advance without review"
            | Some _ when expected <> work.Authority -> Error "advance expected mismatch"
            | Some _ when next = "" || next = expected -> Error "advance does not move"
            | Some _ when List.contains next work.AdvanceHistory || next = work.Authority ->
                Error "advance value already used"
            | Some _ ->
                Ok
                    { work with
                        Authority = next
                        AdvanceHistory = work.AdvanceHistory @ [ next ] }
        | LifecycleEvent.RetirementBlocked(incumbencyId, blockerDigest) ->
            match work.Active with
            | Some active when active.Id = incumbencyId ->
                Ok
                    { work with
                        Active =
                            Some
                                { active with
                                    CleanupBlockerDigest = Some blockerDigest } }
            | _ -> Error "blocked retirement without this active incumbency"

    let applyAll (events: LifecycleEvent list) : Result<LifecycleWork, string> =
        let folder (state: Result<LifecycleWork option, string>) (event: LifecycleEvent) =
            match state with
            | Error _ -> state
            | Ok None ->
                match event with
                | LifecycleEvent.WorkOpened(workId, root, sequence, authority) ->
                    if authority = "" then
                        Error "WorkOpened is missing the authority initial"
                    else
                        Ok(Some(fresh workId root sequence authority))
                | _ -> Error "lifecycle events before WorkOpened"
            | Ok(Some work) -> apply event work |> Result.map Some

        List.fold folder (Ok None) events
        |> Result.bind (function
            | Some work -> Ok work
            | None -> Error "no work")

    let folded (work: LifecycleWork) : Result<FoldedWork, string> =
        projection work
        |> Result.map (fun (stage, authorized) ->
            { Work = work
              Projection = stage
              DeliveryAuthorized = authorized })

    let recover (work: LifecycleWork) : Result<RecoveryPosition * RecoveryNext, string> =
        if work.PendingRelay then
            Ok(RecoveryPosition.Rejected, RecoveryNext.StopRejected)
        elif work.CurrentDelivered && work.Active.IsSome then
            Ok(RecoveryPosition.Conflict, RecoveryNext.StopConflict)
        elif work.CurrentDelivered && work.Active.IsNone then
            match work.Sequence, work.DeliveredRecord with
            | WorkSequence.Plan, Some record when
                record.Outcome = RetirementOutcome.Delivered
                && record.RetirementCursor.IsSome
                && record.ExpectedAdvance <> ""
                && record.ExecSessionId.IsSome
                && record.Digest.IsSome
                && record.Path.IsSome
                && record.ProjectionCut.IsNone
                ->
                Ok(RecoveryPosition.DeliveredIdempotent, RecoveryNext.StopDelivered)
            | WorkSequence.Execution, Some record when
                record.Outcome = RetirementOutcome.Delivered
                && record.ProjectionCut.IsSome
                && record.ExpectedAdvance <> ""
                && record.RetirementCursor.IsNone
                ->
                Ok(RecoveryPosition.DeliveredIdempotent, RecoveryNext.StopDelivered)
            | _ -> Ok(RecoveryPosition.Conflict, RecoveryNext.StopConflict)
        elif work.Active.IsSome then
            match projection work with
            | Error _ -> Ok(RecoveryPosition.Conflict, RecoveryNext.StopConflict)
            | Ok _ ->
                match work.Sequence, work.CurrentRelayed, work.Active with
                | WorkSequence.Plan, _, _ -> Ok(RecoveryPosition.ActiveRebound, RecoveryNext.WaitForUserPrompt)
                | WorkSequence.Execution, true, Some active when active.Review.IsNone ->
                    Ok(RecoveryPosition.ActiveRebound, RecoveryNext.EnterSuccessorPrompt)
                | WorkSequence.Execution, _, _ -> Ok(RecoveryPosition.ActiveRebound, RecoveryNext.EnterDispatch)
        elif not work.CurrentRelayed then
            Ok(RecoveryPosition.Nothing, RecoveryNext.StopNothing)
        else
            Ok(RecoveryPosition.Rejected, RecoveryNext.StopRejected)
