namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Outcome

type TodoView =
    { Unfinished: bool
      ProgressStamp: int64 }

type ReviewView =
    { Required: bool
      Round: int
      MaxRound: int
      Verdict: Fact.ReviewVerdict option }

type SessionScript =
    { GetTodo: unit -> TodoView
      GetReview: unit -> ReviewView
      GetProgressStamp: unit -> int64
      Config: SessionScriptConfig
      ContinueWork: unit -> SessionFlow<unit>
      RequestReview: unit -> SessionFlow<unit>
      Finish: unit -> SessionFlow<SessionOutcome> }

and SessionScriptConfig =
    { FallbackModels: string list
      MaxRetriesPerModel: int
      MaxInvalidRetries: int }

and SessionFlow<'a> = Flow<SessionScript, SessionError, 'a>

module SessionFlows =

    let sessionProgress: ProgressGuard<SessionScript, SessionError> =
        { Stamp = fun s -> s.GetProgressStamp()
          NoProgress = fun msg -> SessionError.NoProgress msg }

    let session = FlowBuilder<SessionScript, SessionError>(Some sessionProgress)

    let finishTodo (s: SessionScript) : SessionFlow<unit> =
        session {
            while s.GetTodo().Unfinished do
                do! s.ContinueWork()
        }

    let passReview (s: SessionScript) : SessionFlow<unit> =
        session {
            do! finishTodo s

            while s.GetReview().Required do
                do! s.RequestReview()
                do! finishTodo s
        }

    let run (s: SessionScript) : SessionFlow<SessionOutcome> =
        session {
            do! passReview s
            return! s.Finish()
        }
