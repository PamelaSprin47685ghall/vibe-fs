namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// ENFORCER-045/050/051: typed material for one Blogger provider request.
///
/// Staged and consumed as a whole. Coverage advance on cycle commit reads this
/// context — never re-derives from the latest XTrace (fail closed if missing).
///
/// C5: RequestId + ObservedPrefixEpochId are frozen at materialization. Commit
/// must use the frozen epoch, not the live PrefixEpoch at tool-return time.
type BloggerMainRequestContext =
    { RequestId: BloggerRequestId
      MainSessionId: SessionId
      BloggerSessionId: SessionId
      Items: BloggerDeltaItem list
      Toml: string
      PreviousIngestedThroughSequence: int64
      NextIngestedThroughSequence: int64
      PreviousCoverableTurnCutoffExclusive: int
      NextCoverableTurnCutoffExclusive: int
      NextCoveredPrefixDigest: string
      FrameEpochId: FrameEpochId
      DeltaDigest: BlobDigest
      ObservedPrefixEpochId: PrefixEpochId }

type BloggerSquashRequestContext =
    { RequestId: BloggerRequestId
      MainSessionId: SessionId
      BloggerSessionId: SessionId
      FrameEpochId: FrameEpochId
      CoveredFrameCount: int
      FrameDigests: BlobDigest list
      ObservedPrefixEpochId: PrefixEpochId }

[<RequireQualifiedAccess>]
type BloggerRequestContext =
    | Main of BloggerMainRequestContext
    | Squash of BloggerSquashRequestContext

[<RequireQualifiedAccess>]
type BloggerTerminalRequestOwnership =
    | Current
    | Superseded
    | Unproven

type BloggerTerminalParentEvidence =
    { PromptKey: PromptKey
      IsRequestScopedRepair: bool }

[<RequireQualifiedAccess>]
module BloggerRequestOwnership =

    let decide
        (currentRequestId: BloggerRequestId)
        (durableOpenRequestId: BloggerRequestId option)
        (durableOpenPromptKey: PromptKey option)
        (parent: BloggerTerminalParentEvidence option)
        : BloggerTerminalRequestOwnership =
        match durableOpenRequestId, parent with
        | Some openRequestId, _ when openRequestId <> currentRequestId -> BloggerTerminalRequestOwnership.Superseded
        | None, _
        | Some _, None -> BloggerTerminalRequestOwnership.Unproven
        | Some _, Some evidence when durableOpenPromptKey = Some evidence.PromptKey -> BloggerTerminalRequestOwnership.Current
        | Some _, Some evidence when evidence.IsRequestScopedRepair -> BloggerTerminalRequestOwnership.Current
        | Some _, Some _ -> BloggerTerminalRequestOwnership.Superseded

[<RequireQualifiedAccess>]
module BloggerRequestContext =

    let toml (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> Some main.Toml
        | BloggerRequestContext.Squash _ -> None

    let isMain (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main _ -> true
        | BloggerRequestContext.Squash _ -> false

    let requestId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.RequestId
        | BloggerRequestContext.Squash squash -> squash.RequestId

    let observedPrefixEpoch (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.ObservedPrefixEpochId
        | BloggerRequestContext.Squash squash -> squash.ObservedPrefixEpochId

    let mainSessionId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.MainSessionId
        | BloggerRequestContext.Squash squash -> squash.MainSessionId

    let bloggerSessionId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.BloggerSessionId
        | BloggerRequestContext.Squash squash -> squash.BloggerSessionId

    let frameEpochId (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Main main -> main.FrameEpochId
        | BloggerRequestContext.Squash squash -> squash.FrameEpochId
