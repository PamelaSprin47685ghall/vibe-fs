namespace Wanxiangshu.Next.Review

open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Flow

/// Production ReviewFlow program — the canonical `review {}` builder usage.
/// Review flows implement the double-PERFECT barrier logic: REVISE is immediate
/// after one call, PERFECT requires a second distinct ProviderRunIdentity
/// against the same tree hash (KISS-N07).
module ReviewProgram =

    /// Lift a plain Task into the ReviewFlow.
    let private fromTask (f: ReviewContext -> CancellationToken -> Task<'a>) : ReviewFlow<'a> =
        Flow.lift f

    /// Record a verdict and return whether it took effect immediately
    /// (REVISE) or still needs a second confirmation (first PERFECT).
    let recordVerdict
        (isPerfect: bool)
        (currentTreeHash: string)
        : ReviewFlow<bool> =
        review {
            let! immediatelyEffective =
                fromTask (fun ctx _ct ->
                    task {
                        // REVISE is immediately effective; first PERFECT
                        // is skeptical until confirmed.
                        return isPerfect |> not
                    })

            return immediatelyEffective
        }

    /// Confirm a PERFECT verdict by binding the git tree hash.
    /// Returns true when the second PERFECT matches the barrier tree.
    let confirmPerfect
        (newTreeHash: string)
        : ReviewFlow<bool> =
        review {
            let! matches =
                fromTask (fun ctx _ct ->
                    task { return ctx.BarrierId = newTreeHash })

            return matches
        }

    /// Run a review flow to completion.
    let runReviewFlow
        (ctx: ReviewContext)
        (ct: CancellationToken)
        (flow: ReviewFlow<'a>)
        : Task<Result<'a, ReviewError>> =
        Flow.run ctx ct flow
