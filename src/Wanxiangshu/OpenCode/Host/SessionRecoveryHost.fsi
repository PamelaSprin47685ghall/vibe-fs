namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// Optional Host capability bound to exact already-accepted physical material.
/// Absence is not permission to create a replacement PromptClaim or resend text.
type ExactAcceptedMessageRecoveryPort =
    { ResumeAccepted: PreProviderResumeRequest -> Task<bool>
      RequeueAuthorized: ProviderRequeueRequest -> Task<bool> }

type SessionRecoveryHost =
    new:
        journal: AgentJournal *
        snapshot: ISessionSnapshotPort *
        scope: PluginRecoveryScope *
        acceptedMessageRecovery: ExactAcceptedMessageRecoveryPort option ->
            SessionRecoveryHost

    member Signal: event: ChatExecutionRecoveryLifecycleEvent -> Task

    member SignalSession:
        sessionId: SessionId * eventOf: (ChatExecutionKey -> ChatExecutionRecoveryLifecycleEvent) -> Task

    member Drain: sessionId: SessionId -> Task
