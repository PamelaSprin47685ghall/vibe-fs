namespace Wanxiangshu.Next.OpenCode

open Wanxiangshu.Next.Kernel.Identity

type RetrySignal =
    { SessionId: SessionId
      Attempt: string
      Reason: string
      MessageId: MessageId option }

type HostSignal =
    | SessionIdle of SessionId
    | ProviderRetry of RetrySignal
    | SessionDeleted of SessionId
    | SessionAbort of SessionId

type SessionSignalSource =
    | LocalPluginEvent
    | GlobalForeignDirectoryEvent
