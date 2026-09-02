namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type TerminalStop =
    { Reason: string
      AuthorityRootUserMessageId: AuthorityRootUserMessageId option }

[<RequireQualifiedAccess>]
module TerminalStop =
    let session reason =
        { Reason = reason
          AuthorityRootUserMessageId = None }

    let forAuthority authorityRoot reason =
        { Reason = reason
          AuthorityRootUserMessageId = Some authorityRoot }

    let belongsTo authorityRoot stop =
        stop.AuthorityRootUserMessageId = Some authorityRoot

type TerminalOutcome =
    | Completed of result: AgentRunResult
    | Aborted of stop: TerminalStop
    | Failed of stop: TerminalStop

type TerminalCompletionListener = SessionId -> TerminalOutcome -> unit

type IEventObservationPort =
    abstract SubscribeTerminalListener: listener: TerminalCompletionListener -> IDisposable
    abstract SubscribeFutureTerminalListener: listener: TerminalCompletionListener -> IDisposable
    abstract NotifyTerminal: sessionId: SessionId -> outcome: TerminalOutcome -> bool
