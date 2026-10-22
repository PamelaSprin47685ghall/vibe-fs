namespace Wanxiangshu.Foundation.TerminalSignal

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type TerminalStop =
    {
        Reason: string
        AuthorityRootUserMessageId: AuthorityRootUserMessageId option
        /// The provider run that was interrupted, when this stop IS a designed-in
        /// delivery interrupt. `None` for every abort unrelated to a delivery
        /// interrupt — never inferred. This carries an existing identity to the
        /// settlement point; it is NOT a terminal fact source and never decides
        /// whether a run is closed (authority closure is decided only by durable
        /// `Authority*Terminal` facts).
        ProviderRun: ProviderRunIdentity option
    }

[<RequireQualifiedAccess>]
module TerminalStop =
    let session reason =
        { Reason = reason
          AuthorityRootUserMessageId = None
          ProviderRun = None }

    let forAuthority authorityRoot reason =
        { Reason = reason
          AuthorityRootUserMessageId = Some authorityRoot
          ProviderRun = None }

    /// A designed-in delivery interrupt: the stop carries the provider run of
    /// the turn that was interrupted, so settlement can match the durable
    /// delivery witness by exact fence. Only the interrupt path uses this.
    let forAuthorityRun authorityRoot providerRun reason =
        { Reason = reason
          AuthorityRootUserMessageId = Some authorityRoot
          ProviderRun = Some providerRun }

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

/// The session-scoped terminal subscription contract, shared by the product and
/// the host adapter.
///
/// The neutral host ports carry no per-session terminal subscription (HNI A2,
/// owner ruling 2026-10-22), so the product owns the terminal facts and their
/// subscribers. The host adapter implements this contract over the product's
/// registry; it never holds a second copy of the facts.
type ISessionTerminalSource =
    inherit IEventObservationPort

    abstract SubscribeTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable
    abstract SubscribeFutureTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable
    abstract HasSubscriber: sessionId: SessionId -> bool
