namespace Wanxiangshu.Enforcer

open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// Session termination capability — same signature as PluginTransforms.fs private type.
type SessionTermination = SessionId -> string -> Task<Result<unit, string>>

/// The Blogger continuation transform (ENFORCER-044).
///
/// A provider step answers the latest physical user message (Host
/// `MessageV2.latest`), and the transform input never holds the step being
/// built. The step position decides what the continuation may do.
///
/// First step of a physical message: nothing answers it yet, and the history
/// tail answers an older physical message, so nothing is judged. The step only
/// receives the live request's canonical view (COMPANION-005), followed by the
/// physical repair prompt when that prompt is a repair scoped to the request.
///
/// Later step of the same Host loop: the previous step answers this physical
/// message. Only when its durable landing proves the physical message belongs
/// to the live request (context-compression-024) may the continuation commit,
/// hand the step to the single repair owner, or stop the physical run.
/// Superseded and unproven steps pass through untouched.
module EnforcerContinuation =

    /// Continuation transform result. Empty message lists are forbidden: Host
    /// forwards them as provider `messages` and rejects with 400.
    /// StopPhysicalRun asks the plugin to interrupt only the current physical attempt after projecting messages.
    [<RequireQualifiedAccess>]
    type ContinuationOutcome =
        | ProjectMessages of obj list
        | StopPhysicalRun of messages: obj list * reason: string

    /// Decode the step position, prove ownership, then dispatch.
    val handleContinuation:
        scope: IBloggerRuntimeHost ->
        journal: AgentJournal option ->
        bloggerSessionId: SessionId ->
        rawMessages: obj list ->
            Task<ContinuationOutcome>

    val applyContinuation:
        scope: IBloggerRuntimeHost ->
        journal: AgentJournal option ->
        terminateSession: SessionTermination ->
        projectionSessionIdOpt: string option ->
        outObj: obj ->
            Task

    /// Stop-ordering boundary: the admission barrier lands before the detached
    /// physical abort is requested; the abort outcome never reopens admission.
    val internal applyPhysicalStop:
        terminateSession: SessionTermination ->
        sid: SessionId ->
        sessionId: string ->
        physicalUserMessageId: PhysicalUserMessageId option ->
        reason: string ->
            unit
