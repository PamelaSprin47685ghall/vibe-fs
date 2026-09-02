namespace Wanxiangshu.Repository.Knowledge.Casebook

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Execution.Session
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider

/// Physical Bookkeeper request kind.
[<RequireQualifiedAccess>]
type BookkeeperRequest =
    | CaseRefresh
    | CaseFinalize

/// Physical Bookkeeper leaf: one CreateChildSession per transaction, js-bookkeeper
/// against process-local staging, then AbortSession.
module BookkeeperRuntime =

    /// Provide the runtime with the session host port and a resolver for the
    /// active authority profile of a session.
    val setRuntime:
        sessions: ISessionHostPort ->
        resolveActiveOwner: (SessionId -> PromptAuthority.AuthorityExecutionProfile option) ->
            unit

    /// Reset the runtime, clearing all bindings and pending completions.
    val resetRuntime: unit -> unit

    /// Bind a child session to a Bookkeeper transaction and owner.
    val bindSession: sessionId: string -> txId: string -> ownerSessionId: string -> unit

    /// Unbind a child session and clear its prompt authorization / completion.
    val unbindSession: sessionId: string -> unit

    /// Consume a pending prompt authorization for the given session if the
    /// explicit agent and text match the authorized values.
    val tryConsumePromptAuthorization:
        sessionId: SessionId -> explicitAgent: string option -> text: string option -> bool

    /// Complete the physical Bookkeeper session with the given outcome.
    val completePhysical: sessionId: SessionId -> outcome: Result<unit, string> -> unit

    /// Try to find the active Bookkeeper transaction id for a session.
    val tryTxId: sessionId: string -> string option

    /// Active Bookkeeper transaction id for a session, or empty string.
    val txIdFor: sessionId: string -> string

    /// True if the session is currently bound to a Bookkeeper transaction.
    val isAttached: sessionId: string -> bool

    /// Run a Bookkeeper transaction (CaseRefresh or CaseFinalize) for the
    /// given owner session, question, answer, observations and optional
    /// extra transcript. Returns the resulting question/answer pair on success.
    val runTransaction:
        kind: BookkeeperRequest ->
        ownerSessionId: SessionId ->
        q: string ->
        a: string ->
        observations: Observation list ->
        extraTranscript: string option ->
            Task<Result<string * string, string>>
