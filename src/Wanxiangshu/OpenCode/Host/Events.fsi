namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type TerminalStop =
    { Reason: string
      AuthorityRootUserMessageId: AuthorityRootUserMessageId option }

[<RequireQualifiedAccess>]
module TerminalStop =
    val session: reason: string -> TerminalStop
    val forAuthority: authorityRoot: AuthorityRootUserMessageId -> reason: string -> TerminalStop
    val belongsTo: authorityRoot: AuthorityRootUserMessageId -> stop: TerminalStop -> bool

type TerminalOutcome =
    | Completed of result: AgentRunResult
    | Aborted of stop: TerminalStop
    | Failed of stop: TerminalStop

type TerminalCompletionListener = SessionId -> TerminalOutcome -> unit

type IEventObservationPort =
    abstract SubscribeTerminalListener: listener: TerminalCompletionListener -> IDisposable
    abstract SubscribeFutureTerminalListener: listener: TerminalCompletionListener -> IDisposable
    abstract NotifyTerminal: sessionId: SessionId -> outcome: TerminalOutcome -> bool

module Events =
    type ListenerRegistration =
        { Listener: TerminalCompletionListener
          mutable Live: bool }

    type HostEventPort =
        new: unit -> HostEventPort
        interface IEventObservationPort
