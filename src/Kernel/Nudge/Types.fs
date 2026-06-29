module Wanxiangshu.Kernel.Nudge.Types

type FreshAssistantSnapshot =
    { lastAssistantMessage: string
      agentFromMessage: string option }

type NudgeShellState =
    { nudgedSessions: Set<string>
      stoppedSessions: Set<string>
      retryPendingSessions: Set<string>
      sessionAgents: Map<string, string>
      freshAssistantSnapshots: Map<string, FreshAssistantSnapshot>
      lastNudgedSession: string option
      compactionAnchorsIssued: Set<string> }

type SessionSnapshot =
    { todos: string list
      lastAssistantMessage: string
      alreadyNudged: bool
      agentFromMessage: string option
      lastAssistantIsCompaction: bool
      anchorPromptIssued: bool }

type NudgeDecision =
    | StandDown
    | Send of promptText: string * agentOpt: string option

type SendOutcome =
    | Delivered
    | Aborted
    | Busy
    | Failed

type MessageOutcome =
    | UpdateAborted
    | UpdateCompletedAssistant
    | UpdateNoChange

type PartOutcome =
    | PartRetry
    | PartAborted
    | PartRetryProgress
    | PartOther

type StepFailOutcome = StepFailAbort | StepFailOther
type ToolFailOutcome = ToolFailAbort | ToolFailOther
type SessionErrorOutcome = SessionErrorAbort | SessionErrorOther

type NudgeHostEvent =
    | StreamAbort
    | SessionDeleted
    | SessionNextPrompted of promptText: string
    | SessionNextRetried
    | MessageUpdated of outcome: MessageOutcome
    | MessagePartUpdated of outcome: PartOutcome
    | SessionNextStepFailed of outcome: StepFailOutcome
    | SessionNextToolFailed of outcome: ToolFailOutcome
    | SessionNextStepEnded of finish: string
    | SessionIdle
    | SessionError of outcome: SessionErrorOutcome
    | SessionStatusIdle
    | SessionStatusBusy
    | SessionStatusRetry
    | RetryProgress
    | Other

let emptyState =
    { nudgedSessions = Set.empty
      stoppedSessions = Set.empty
      retryPendingSessions = Set.empty
      sessionAgents = Map.empty
      freshAssistantSnapshots = Map.empty
      lastNudgedSession = None
      compactionAnchorsIssued = Set.empty }
