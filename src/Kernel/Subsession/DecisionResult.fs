namespace Wanxiangshu.Kernel.Subsession.Types

type IgnoreReason =
    | DuplicateIdleBeforeTurnMarker
    | DuplicateError
    | StaleTimer
    | StaleTurnMarker
    | UnattributedObservationBeforeStart
    | AbortInProgress
    | EvidenceBeforeRun
    | IdleBeforeAbortBarrier
    | UnattributableObservation

type Decision =
    { NextState: SubsessionState
      Events: SubsessionEvent list
      Effects: Effect list }

type DecisionResult =
    | Decided of Decision
    | NoChange of IgnoreReason

type DecisionError =
    | IllegalTransition of state: string * command: string
    | StaleTurnCommand of expected: TurnId * actual: TurnId
