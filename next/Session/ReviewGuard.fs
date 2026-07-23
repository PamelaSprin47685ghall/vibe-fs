namespace Wanxiangshu.Next.Session

[<RequireQualifiedAccess>]
type ReviewVerdict =
    | Perfect
    | Revise

[<RequireQualifiedAccess>]
type ReviewFinishResult =
    | Confirmed
    | NeedsReview

type ReviewState =
    { LastGitTreeHash: string option
      ConsecutivePerfects: int }

module ReviewGuard =

    let empty: ReviewState =
        { LastGitTreeHash = None
          ConsecutivePerfects = 0 }

    let invalidate (state: ReviewState) : ReviewState =
        { LastGitTreeHash = None
          ConsecutivePerfects = 0 }

    let recordVerdict (verdict: ReviewVerdict) (gitTreeHash: string) (state: ReviewState) : ReviewState =
        match state.LastGitTreeHash with
        | Some lastHash when lastHash = gitTreeHash ->
            match verdict with
            | ReviewVerdict.Perfect ->
                { LastGitTreeHash = Some gitTreeHash
                  ConsecutivePerfects = state.ConsecutivePerfects + 1 }
            | ReviewVerdict.Revise ->
                { LastGitTreeHash = Some gitTreeHash
                  ConsecutivePerfects = 0 }
        | _ ->
            match verdict with
            | ReviewVerdict.Perfect ->
                { LastGitTreeHash = Some gitTreeHash
                  ConsecutivePerfects = 1 }
            | ReviewVerdict.Revise ->
                { LastGitTreeHash = Some gitTreeHash
                  ConsecutivePerfects = 0 }

    let tryFinish (state: ReviewState) : ReviewFinishResult =
        if state.ConsecutivePerfects >= 2 then
            ReviewFinishResult.Confirmed
        else
            ReviewFinishResult.NeedsReview
