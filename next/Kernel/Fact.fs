namespace Wanxiangshu.Next.Kernel

open System
open Wanxiangshu.Next.Kernel.Identity

module Fact =

    type RuntimeFact =
        | RuntimeStarted of
            {| RuntimeId: RuntimeId
               ProcessId: int
               StartedAt: DateTimeOffset |}

    type SessionResult =
        | Completed of string
        | Cancelled of string
        | Failed of string

    type SessionFact =
        | HumanTurnStarted of {| TurnId: TurnId |}
        | SessionSettled of {| Result: SessionResult |}

    type TodoSnapshot = { Items: string list }

    type TodoFact = TodoChanged of {| Snapshot: TodoSnapshot |}

    type PromptOutcome =
        | Delivered of messageId: MessageId
        | RetryableFailure of reason: string
        | AcceptanceUnknown of reason: string * messageId: MessageId option
        | FatalFailure of reason: string

    type PromptFact =
        | PromptRequested of
            {| PromptKey: string
               TurnId: TurnId
               Purpose: string |}
        | PromptSubmitted of
            {| PromptKey: string
               MessageId: MessageId |}
        | PromptTerminal of
            {| PromptKey: string
               Outcome: PromptOutcome
               AssistantMessageId: MessageId option |}

    [<RequireQualifiedAccess>]
    type ReviewVerdict =
        | Passed
        | NeedsChanges of changeRequests: string list
        | Invalid of reason: string

    type ReviewFact =
        | ReviewApplied of
            {| Verdict: ReviewVerdict
               Round: int
               ResultingTodo: TodoSnapshot option |}

    type ChildResult =
        | ChildCompleted of summary: string
        | ChildCancelled of reason: string
        | ChildFailed of error: string

    type ChildFact =
        | ChildCreated of
            {| ChildId: ChildId
               TargetAgent: string |}
        | ChildCompletedFact of
            {| ChildId: ChildId
               Result: ChildResult |}

    type ProcessResult =
        { ExitCode: int
          Stdout: string
          Stderr: string
          StdoutTruncated: bool
          StderrTruncated: bool }

    type ProcessFact =
        | ProcessSpawned of
            {| ProcessId: ProcessId
               Command: string |}
        | ProcessExited of
            {| ProcessId: ProcessId
               Result: ProcessResult |}

    type SquadTaskResult =
        | TaskVerified of summary: string
        | TaskFailed of error: string

    type SquadFact =
        | TaskVerifiedFact of
            {| TaskId: string
               Result: SquadTaskResult |}
        | WaveAccepted of
            {| WaveIndex: int
               AcceptedTaskIds: string list |}
        | FastForwardCompleted of {| TaskId: string; TargetRef: string |}

    type Fact =
        | Runtime of RuntimeFact
        | Session of SessionFact
        | Todo of TodoFact
        | Prompt of PromptFact
        | Review of ReviewFact
        | Child of ChildFact
        | Process of ProcessFact
        | Squad of SquadFact
