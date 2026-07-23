namespace Wanxiangshu.Next.Session

open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Outcome

module SessionFlows =

    let sessionProgress: ProgressGuard<SessionScript, SessionError> =
        { Stamp = fun s -> s.GetProgressStamp()
          NoProgress = fun msg -> SessionError.NoProgress msg }

    let session = FlowBuilder<SessionScript, SessionError>(Some sessionProgress)

    let finishTodo (s: SessionScript) : SessionFlow<unit> =
        let scriptFlow =
            session {
                while s.GetTodo().Unfinished do
                    do! s.ContinueWork()
            }
        scriptFlow

    let passReview (s: SessionScript) : SessionFlow<unit> =
        session {
            let! () = finishTodo s

            while s.GetReview().Required do
                do! s.RequestReview()
                do! finishTodo s
        }

    let run (s: SessionScript) : SessionFlow<SessionOutcome> =
        session {
            do! passReview s
            let! outcome = s.Finish()
            return outcome
        }

    let runFlow
        (s: SessionScript)
        (ct: CancellationToken)
        (flow: SessionFlow<'a>)
        : Task<Result<'a, SessionError>> =
        task {
            try
                return! Flow.run s ct flow
            with ex ->
                let msg = if isNull (box ex) then "" else string ex

                if msg.Contains("cancel") || msg.Contains("Cancel") || msg.Contains("Operation") then
                    return Error SessionError.SessionCancelled
                else
                    return Error(SessionError.Protocol msg)
        }
