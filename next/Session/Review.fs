namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Session.SessionFlows

module Review =

    type ReviewOnceFunction = unit -> SessionFlow<Fact.ReviewVerdict>

    type ReviewReport =
        { Text: string
          Verdict: Fact.ReviewVerdict }

    type ReviewerChild =
        { Review: unit -> SessionFlow<ReviewReport> }

    type ReviewScript =
        { StartReviewer: unit -> SessionFlow<ReviewerChild>
          AcceptVerdict: ReviewReport -> SessionFlow<Fact.ReviewVerdict> }

    let acceptVerdict
        (commit: Fact.Fact -> SessionFlow<unit>)
        (currentRound: int)
        (report: ReviewReport)
        : SessionFlow<Fact.ReviewVerdict> =
        session {
            let verdict = report.Verdict

            let todoSnapshot =
                match verdict with
                | Fact.ReviewVerdict.NeedsChanges changes -> Some({ Items = changes }: Fact.TodoSnapshot)
                | Fact.ReviewVerdict.Passed
                | Fact.ReviewVerdict.Invalid _ -> None

            let fact =
                Fact.Review(
                    Fact.ReviewFact.ReviewApplied
                        {| Verdict = verdict
                           Round = currentRound + 1
                           ResultingTodo = todoSnapshot |}
                )

            do! commit fact
            return verdict
        }

    let reviewOnce (r: ReviewScript) : SessionFlow<Fact.ReviewVerdict> =
        session {
            let! reviewer = r.StartReviewer()
            let! report = reviewer.Review()
            return! r.AcceptVerdict(report)
        }

    let rec requestValidReview (reviewOnce: ReviewOnceFunction) (remaining: int) : SessionFlow<Fact.ReviewVerdict> =
        session {
            if remaining <= 0 then
                return! Flow.fail SessionError.ReviewExhausted
            else
                let! verdict = reviewOnce ()

                match verdict with
                | Fact.ReviewVerdict.Invalid _ -> return! requestValidReview reviewOnce (remaining - 1)
                | Fact.ReviewVerdict.Passed
                | Fact.ReviewVerdict.NeedsChanges _ -> return verdict
        }

    let requestReview (s: SessionScript) (reviewOnce: ReviewOnceFunction) : SessionFlow<unit> =
        session {
            let reviewView = s.GetReview()

            if
                reviewView.Round >= reviewView.MaxRound
                && reviewView.Verdict <> Some Fact.ReviewVerdict.Passed
            then
                return! Flow.fail SessionError.ReviewExhausted
            else
                let! _ = requestValidReview reviewOnce s.Config.MaxInvalidRetries
                return ()
        }
