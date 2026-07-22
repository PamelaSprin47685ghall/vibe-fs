module Wanxiangshu.Kernel.ReviewSession.Types

[<RequireQualifiedAccess>]
type ReviewState =
    | Inactive
    | Active of task: string
    | Locked of task: string * reviewerId: string
    | Accepted
    | NeedsRevision of feedback: string

[<RequireQualifiedAccess>]
type ReviewChallengeState =
    | NotRequested
    | Requested

type ReviewCommand =
    | Activate of task: string
    | Submit
    | Lock of reviewerId: string
    | Unlock
    | Accept
    | RequestRevision of feedback: string
    | RequestChallenge

[<RequireQualifiedAccess>]
type ReviewEvent =
    | Activated of task: string
    | Submitted
    | LockAcquired of reviewerId: string
    | LockReleased
    | Accepted
    | NeedsRevision of feedback: string
    | ChallengeRequested

type ReviewResult =
    | Accepted of feedback: string list
    | NeedsRevision of feedback: string list
    | Terminated

/// Boundary helper: host/wire strings become structured feedback items once.
let feedbackItems (feedback: string) : string list =
    let t = if isNull feedback then "" else feedback.Trim()

    if t = "" then
        []
    else
        t.Split([| '\n' |], System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun s -> s.Trim())
        |> Array.filter (fun s -> s <> "")
        |> Array.toList

type ReviewSession =
    { id: string
      version: int
      state: ReviewState
      createdAt: int64
      originalTask: string option
      lastFeedback: string option
      parentId: string option
      childIds: string list
      challengeState: ReviewChallengeState }

let empty id createdAt : ReviewSession =
    { id = id
      version = 0
      state = ReviewState.Inactive
      createdAt = createdAt
      originalTask = None
      lastFeedback = None
      parentId = None
      childIds = []
      challengeState = ReviewChallengeState.NotRequested }

let withTask task session =
    if session.originalTask = Some task then
        session
    else
        { session with
            originalTask = Some task
            version = session.version + 1 }

let withFeedback session feedback =
    if session.lastFeedback = Some feedback then
        session
    else
        { session with
            lastFeedback = Some feedback
            version = session.version + 1 }

let addChild session childId =
    if List.contains childId session.childIds then
        session
    else
        { session with
            childIds = session.childIds @ [ childId ]
            version = session.version + 1 }

type RoundOutcome =
    | Resolved of result: ReviewResult
    | PromptFailed
    | NoResult

type LoopDecision =
    | Finish of result: ReviewResult
    | Nudge of nudgeCount: int
