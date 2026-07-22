namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Session.SessionFlows

module Fallback =

    type SendContinueFunction = string -> int -> SessionFlow<SendOutcome>

    let rec tryAttempts
        (s: SessionScript)
        (sendContinue: SendContinueFunction)
        (model: string)
        (attempt: int)
        : SessionFlow<SendOutcome option> =
        session {
            if attempt > s.Config.MaxRetriesPerModel then
                return None
            else
                let! outcome = sendContinue model attempt

                match outcome with
                | Delivered _ -> return Some outcome
                | Retryable _ -> return! tryAttempts s sendContinue model (attempt + 1)
                | Fatal reason -> return! Flow.fail (SessionError.Protocol reason)
                | AcceptanceUnknown(reason, _) -> return! Flow.fail SessionError.PromptUncertain
        }

    let rec tryModels
        (s: SessionScript)
        (sendContinue: SendContinueFunction)
        (models: string list)
        : SessionFlow<SendOutcome> =
        session {
            match models with
            | [] -> return! Flow.fail SessionError.FallbackExhausted
            | model :: remaining ->
                let! resultOpt = tryAttempts s sendContinue model 1

                match resultOpt with
                | Some outcome -> return outcome
                | None -> return! tryModels s sendContinue remaining
        }

    let continueWork (s: SessionScript) (sendContinue: SendContinueFunction) : SessionFlow<unit> =
        session {
            let! outcome = tryModels s sendContinue s.Config.FallbackModels
            do! s.CommitTodoFrom outcome
        }
