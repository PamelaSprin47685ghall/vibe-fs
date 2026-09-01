namespace Wanxiangshu.Enforcer.Cycle

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
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
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Resources
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

module EnforcerFrameRecovery =

    type FrameLoadError =
        | MissingAssociation
        | MissingBlogSession
        | MissingFrameBlob of digest: string
        | DigestMismatch of digest: string
        | EpochMismatch

    val loadEffectiveFrames:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
            System.Threading.Tasks.Task<
                Result<
                    ((Wanxiangshu.Foundation.Identity.BlobDigest * string) list *
                    Wanxiangshu.Foundation.Identity.FrameEpochId),
                    FrameLoadError
                 >
             >

    val tryRebuildFromContext:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext ->
            System.Threading.Tasks.Task<obj list option>

    val rebuildFromContext: AgentJournal -> SessionId -> BloggerRequestContext -> obj list -> Task<obj list>

    val lastCoveredCursor:
        Wanxiangshu.Context.Trace.XTraceProjectionState ->
        Wanxiangshu.Context.Companion.Blogger.SemanticCursor ->
            Wanxiangshu.Context.Trace.XTraceCursor option

    val coveredPrefixDigest:
        int ->
        string ->
        int ->
        Wanxiangshu.Participant.Provider.Projection.ProviderProjection.ProviderSemanticProjection ->
            string

    val tryReloadRequestContext:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Context.Companion.Blogger.Runtime.OpenBloggerRequest ->
            System.Threading.Tasks.Task<Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext option>

    val tryLiveCycleContext:
        Wanxiangshu.Context.Companion.Blogger.Runtime.IBloggerRuntimeHost ->
        Wanxiangshu.Foundation.Identity.SessionId ->
            Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext option

    val resolveCycleContext:
        Wanxiangshu.Context.Companion.Blogger.Runtime.IBloggerRuntimeHost ->
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.SessionId ->
            System.Threading.Tasks.Task<Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext option>
