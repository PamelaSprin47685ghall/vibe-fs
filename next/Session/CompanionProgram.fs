namespace Wanxiangshu.Next.Session

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Flow

/// Production CompanionFlow program — the canonical `companion {}` builder usage.
/// Companion flows manage projection delta computation, blogger step scheduling,
/// and prefix-epoch lifecycle for Session X (KISS-N02).
module CompanionProgram =

    /// Lift a plain Task into the CompanionFlow.
    let private fromTask (f: CompanionContext -> CancellationToken -> Task<'a>) : CompanionFlow<'a> =
        Flow.lift f

    /// Build the next delta between the last successful projection and the
    /// current canonical projection.  Returns None when there is no delta.
    /// `previous` is the last snapshot that was successfully blogged (None on
    /// first run).
    let buildDelta
        (previous: ProjectionSnapshot option)
        (current: ProjectionSnapshot)
        : CompanionFlow<ProjectionSnapshot option> =
        companion {
            let! delta =
                fromTask (fun _ _ct ->
                    task { return Companion.jsonDelta previous current })

            return delta
        }

    /// Run a companion flow to completion and return its result.
    let runCompanionFlow
        (ctx: CompanionContext)
        (ct: CancellationToken)
        (flow: CompanionFlow<'a>)
        : Task<Result<'a, CompanionError>> =
        Flow.run ctx ct flow

    /// Check whether prefix replacement should be enabled for this session,
    /// given the context-limit budget and frozen-B coverage proof.
    let shouldReplacePrefix
        (projectedInputTokens: int)
        (reservedOutputTokens: int)
        (contextLimit: int)
        : CompanionFlow<bool> =
        companion {
            let! decision =
                fromTask (fun _ _ct ->
                    task {
                        let threshold = projectedInputTokens + reservedOutputTokens
                        return threshold > contextLimit
                    })

            return decision
        }
