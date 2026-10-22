namespace Wanxiangshu.Execution.Session

open System
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.TerminalSignal

/// The product's single holder of terminal facts and terminal subscriptions.
///
/// HNI A2 (owner ruling, 2026-10-22): the neutral host ports carry no
/// per-session terminal subscription, so the product owns it. The facts are the
/// existing exact ones — a `TerminalOutcome` with its `AgentRunResult` (and
/// therefore its exact `ProviderRunIdentity`) or its `TerminalStop` (and
/// therefore its exact `AuthorityRootUserMessageId`). The registry invents no
/// authority field of its own and applies no new matching law.
///
/// The sticky subscription replays the last proven terminal of a session; the
/// future-only subscription never replays one and only observes what arrives
/// after it subscribed.
type TerminalRegistry =
    new: unit -> TerminalRegistry

    /// A terminal fact proven by the product, published to the subscribers.
    /// Returns whether a subscriber was live when the fact landed.
    member NotifyTerminal: sessionId: SessionId -> outcome: TerminalOutcome -> bool

    /// The sticky subscription of one session: the last proven terminal of that
    /// session is replayed, then later terminals are delivered.
    member SubscribeTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable

    /// The future-only subscription of one session: nothing is replayed; only
    /// terminals that arrive after the subscription are delivered.
    member SubscribeFutureTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable

    /// Whether the session has a live subscriber.
    member HasSubscriber: sessionId: SessionId -> bool

    interface ISessionTerminalSource
