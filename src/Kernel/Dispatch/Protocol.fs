module Wanxiangshu.Kernel.Dispatch.Protocol

open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.FallbackKernel.Types

/// Single state machine that all dispatch paths must pass through.
/// Replaces ad-hoc booleans like `EventHandlingActive` and string "Dispatched"/
/// "Cancelled" / "Delivered" labels that have caused host-state confusion.
type DispatchPhase =
    /// Domain decided to send; event log appended; no host call yet.
    | Requested
    /// Effect runner is currently invoking the host. Cancellation here is
    /// `CancelRequested` — we mark this phase but do not abort the host call.
    | TransportStarted
    /// Host confirmed it accepted the dispatch and a real user message
    /// (or a stable opaque marker with sufficient correlation) is in scope.
    /// ONLY here may we attribute host side effects (busy, assistant, idle)
    /// to this dispatch.
    | HostAccepted
    /// Run observed: we have a strong host signal that ties to this
    /// dispatch (busy for this session, or assistant message whose parent
    /// matches HostUserMessageId, or host run id seen).
    | RunObserved
    /// One of the terminal states. The mapping is exhaustive; anything else
    /// is a programming error.
    | Terminal of DispatchTerminal

and DispatchTerminal =
    /// Host completed the run; we have an assistant message observed.
    | Completed
    /// Host returned a hard error.
    | Failed of ErrorInput
    /// Cancellation succeeded: we sent a real abort and the host confirmed.
    | Cancelled
    /// Newer dispatch superseded us; do not retry.
    | Superseded
    /// Session closed: parent session is gone.
    | SessionClosed
    /// Reject from host: re-dispatch may be possible.
    | RejectedBeforeSend of ErrorInput
    /// Host API not available (session API missing or method not implemented).
    /// Must NOT be silently treated as a successful delivery.
    | TransportUnavailable of ErrorInput
    /// Prompt transport returned but we cannot tell if the host accepted.
    /// Re-dispatch MUST NOT happen — host may have already created the message.
    | AcceptanceUnknown of ErrorInput
    /// Cancellation sent; abort API is unavailable. Caller may decide
    /// retry / wait / give up.
    | AbortUnknown of ErrorInput
    /// Turn deadline exceeded before HostAccepted.
    | TimedOut of ErrorInput
    /// Internal poison: any further interaction with this dispatch is
    /// considered undefined; session should also be poisoned.
    | Poisoned of string

/// A proof returned to the caller once the dispatch reaches HostAccepted.
type DispatchAcceptance =
    | UserMessageAccepted of messageId: string
    | RunAccepted of runId: string
    /// Host accepted the prompt but did not surface a stable message/run id.
    /// This receipt is the lowest acceptable correlation: it proves the host
    /// took the bytes, nothing more. Used by OMP where the ordered-stream
    /// contract is the only signal available.
    | OpaqueAccepted of marker: string

/// What the caller of `cancel` should expect.
type DispatchCancelResult =
    /// Cancellation propagated before the dispatch reached HostAccepted.
    /// No host abort was issued; the dispatch is terminal.
    | CancelledBeforeAcceptance
    /// Cancellation propagated after HostAccepted; a physical abort was sent.
    | AbortSent
    /// Cancellation request reached the actor but the host abort API was
    /// unavailable. Caller decides what to do (re-poll, give up, etc.).
    | AbortUnavailable
    /// Dispatch was already terminal before cancellation arrived.
    | AlreadyTerminal of DispatchTerminal
