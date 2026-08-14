namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// ENFORCER-064: Blogger missing-tool recovery (NoRecovery | InteractionNudgeIssued | Aabb).
/// Type name avoids dsl-ownership Stage/Spent suffixes; cases match the clause.
/// Derived from durable claim + provider-visible transcript — never stored on a cell.
[<RequireQualifiedAccess>]
type BloggerToolRecovery =
    | NoRecovery
    | InteractionNudgeIssued of ProviderRunIdentity
    | AabbRepairConsumed

/// One drain-window opening. Module-private constructor: only the reactivation
/// path (a new Authority Root arriving on the main) can mint it, so no caller
/// can forge an open window for an arbitrary root.
type DrainPermit = private DrainPermit of AuthorityRootUserMessageId

/// After a durable handle seal, whether a new Authority Root reopened a drain
/// window. The handle lifecycle NEVER unseals (CompletedAwaitingJoin/Abandoned/
/// Retired stay sealed), so a reactivation can only be observed in-process — the
/// window carries the root that opened it (as an unforgeable permit). Closed =
/// seal blocks new Y work.
[<RequireQualifiedAccess>]
type DrainWindow =
    | Closed
    | Open of DrainPermit

/// Pure routing + physical drain helpers. Busy ownership is the host flight
/// registry (`IParkedTransformHost.HasFlight`); drain is the physical drain slot
/// (`GetDrainWindow` / `SetDrainWindow` / `IsDrainOpen`). No runtime State DU.
[<RequireQualifiedAccess>]
module BloggerRuntime =

    type Decision =
        | Start of BloggerRequestContext
        | Skip
        | Offer of BloggerRequestContext

    /// Mint an open drain window for a new Authority Root. `DrainPermit` is
    /// module-private so only this factory can construct `DrainWindow.Open`.
    let openDrain (root: AuthorityRootUserMessageId) : DrainWindow = DrainWindow.Open(DrainPermit root)

    /// Pure material routing from physical facts (parked waiter + flight ownership).
    let decideMaterial (hasParked: bool) (hasFlight: bool) (ctx: BloggerRequestContext) : Decision =
        if hasFlight then Decision.Skip
        elif hasParked then Decision.Offer ctx
        else Decision.Start ctx

    /// Durable handle seal blocks new work unless the drain window is open.
    /// `durableHandleSealed` is the journal truth (AgentProjection.mainSealedForBlogger).
    /// Busy is physical flight ownership (`hasFlight`):
    /// hasFlight → do not block via this gate (SkippedInFlight is the other path);
    /// otherwise block when durable-sealed and drain is closed.
    let blocksNewRequest (durableHandleSealed: bool) (hasFlight: bool) (drainOpen: bool) : bool =
        not hasFlight && durableHandleSealed && not drainOpen
