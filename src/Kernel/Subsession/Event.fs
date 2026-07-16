namespace Wanxiangshu.Kernel.Subsession.Types

open Wanxiangshu.Kernel.FallbackKernel.Types

type RunStartedData =
    { RunId: RunId
      ParentSessionId: SessionId
      SessionId: SessionId }

type TurnData =
    { RunId: RunId
      TurnId: TurnId
      Ordinal: TurnOrdinal
      Model: FallbackModel
      Prompt: string }

type TurnStartedData =
    { RunId: RunId
      TurnId: TurnId
      Receipt: HostStartReceipt }

type TurnFinishOutcome =
    | TurnCompleted of output: string
    | TurnFailed of ErrorInput
    | TurnCancelled
    | TurnRecovering
    | TurnInfrastructureFailed of reason: string

type SubsessionEvent =
    | RunStarted of RunStartedData
    | TurnDispatchRequested of TurnData
    | TurnStarted of TurnStartedData
    | TurnFinished of TurnId * TurnFinishOutcome
    | AbortRequested of RunId * TurnId
    | RunFinished of RunId * RunResult
    | SessionPoisoned of SessionId * PoisonReason
    | PhysicalSessionClosed of SessionId
