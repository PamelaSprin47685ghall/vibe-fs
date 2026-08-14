namespace Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Turn
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
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

/// Production Companion program — now expressed as functions, not a Flow AST.
///
/// ARCH-001: control flow is plain `let!/do!/match/task`, not a state machine.
/// The run helper keeps the same boundary so callers can be migrated one at a time.
module CompanionProgram =

    /// Build the next delta between the last successful projection and the
    /// current canonical projection.  Returns None when there is no delta.
    /// `previous` is the last snapshot that was successfully blogged (None on
    /// first run).
    let buildDelta
        (cursor: SemanticCursor)
        (previousCutoff: int)
        (current: ProviderSemanticProjection)
        (_ctx: CompanionContext)
        (_ct: CancellationToken)
        : Task<BloggerDeltaChunk option> =
        task { return BloggerDelta.nextChunk BloggerDelta.DeltaLimitBytes cursor previousCutoff current.Messages }

    /// Run a companion action to completion and return its result.
    let runCompanionFlow
        (ctx: CompanionContext)
        (ct: CancellationToken)
        (action: CompanionContext -> CancellationToken -> Task<'a>)
        : Task<Result<'a, CompanionError>> =
        task {
            try
                let! value = action ctx ct
                return Ok value
            with
            | :? OperationCanceledException when ct.IsCancellationRequested ->
                return Error(CompanionError.BloggerFailed "cancelled")
            | ex -> return Error(CompanionError.ProjectionFailed ex.Message)
        }
