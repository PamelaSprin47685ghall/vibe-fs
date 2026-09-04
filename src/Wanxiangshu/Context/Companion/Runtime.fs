namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open Fable.Core
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

/// Companion state wrapper for the physical caches used at Host boundaries.
type Companion(?initialMemory: CompanionMemory, ?durable: ICompanionDurablePort, ?sessionId: SessionId) =
    let lockObj = obj ()

    let restoredMemory = initialMemory

    let blogProjection: BlogProjectionState =
        restoredMemory
        |> Option.map (fun m -> m.Blog)
        |> Option.defaultValue BlogProjection.empty

    // DSL-MUTABLE: resource — in-memory X-trace projection mirror (durable-backed)
    let mutable xTraceProjection: XTraceProjectionState =
        restoredMemory
        |> Option.map (fun m -> m.XTrace)
        |> Option.defaultValue XTraceProjection.empty

    let latestB: BlogText option =
        restoredMemory |> Option.bind (fun m -> m.EffectiveFrames)

    /// COMPANION-002: the companion Blogger Session Y for this X.
    ///
    /// A runtime mirror of the durable link, not a second writer: `CompanionHost`
    /// owns the create/abort decision and writes the fact through
    /// `ICompanionDurablePort`, then tells this cache. Restored on construction so a
    /// restart reuses the same Y instead of creating a second one.
    // DSL-MUTABLE: resource — cached companion Blogger session id
    let mutable bloggerSessionId: SessionId option =
        restoredMemory |> Option.bind (fun m -> m.BloggerSessionId)

    member _.Memory: CompanionMemory =
        lock lockObj (fun () ->
            { Blog = blogProjection
              EffectiveFrames = latestB
              BloggerSessionId = bloggerSessionId
              XTrace = xTraceProjection })

    /// Mirror a durable Blogger link that `CompanionHost` already recorded.
    member _.RecordBloggerLinked(bloggerId: SessionId) : unit =
        lock lockObj (fun () -> bloggerSessionId <- Some bloggerId)

    /// Mirror a durable Blogger unlink. `None` again, so the next transform creates
    /// a fresh Y rather than prompting an aborted session forever.
    member _.RecordBloggerClosed() : unit =
        lock lockObj (fun () -> bloggerSessionId <- None)

    /// COMPANION-007: refresh the in-memory XTrace mirror after the transform
    /// boundary captured new parts. Without this the chunker keeps mapping the
    /// ingest cursor against the trace captured at construction, which is empty
    /// at first and re-reads the projection head every round.
    member _.RefreshXTrace(state: XTraceProjectionState) : unit =
        lock lockObj (fun () -> xTraceProjection <- state)
