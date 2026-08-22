namespace Wanxiangshu.Context.Companion

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
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
