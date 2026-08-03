namespace Wanxiangshu.Session

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Flow
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection

/// Production CompanionFlow program — the canonical `companion {}` builder usage.
/// Companion flows manage projection delta computation and blogger step
/// scheduling. Prefix-epoch lifecycle is NOT here: CTX-002 puts it after a failed
/// attempt, where `Domain.AttemptPlanner` decides it from the attempt outcome.
module CompanionProgram =

    /// Lift a plain Task into the CompanionFlow.
    let private fromTask (f: CompanionContext -> CancellationToken -> Task<'a>) : CompanionFlow<'a> = Flow.lift f

    /// Build the next delta between the last successful projection and the
    /// current canonical projection.  Returns None when there is no delta.
    /// `previous` is the last snapshot that was successfully blogged (None on
    /// first run).
    let buildDelta
        (cursor: SemanticCursor)
        (previousCutoff: int)
        (current: ProviderSemanticProjection)
        : CompanionFlow<BloggerDeltaChunk option> =
        companion {
            let! delta =
                fromTask (fun _ _ct ->
                    task {
                        return
                            BloggerDelta.nextChunk BloggerDelta.DeltaLimitBytes cursor previousCutoff current.Messages
                    })

            return delta
        }

    /// Run a companion flow to completion and return its result.
    let runCompanionFlow
        (ctx: CompanionContext)
        (ct: CancellationToken)
        (flow: CompanionFlow<'a>)
        : Task<Result<'a, CompanionError>> =
        Flow.run ctx ct flow
