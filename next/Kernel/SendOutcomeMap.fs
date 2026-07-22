namespace Wanxiangshu.Next.Kernel

open Outcome

module SendOutcomeMap =

    let toPromptOutcome (sendOutcome: SendOutcome) : Fact.PromptOutcome =
        match sendOutcome with
        | Delivered msgId -> Fact.Delivered msgId
        | Retryable reason -> Fact.RetryableFailure reason
        | AcceptanceUnknown(reason, msgIdOpt) -> Fact.AcceptanceUnknown(reason, msgIdOpt)
        | Fatal reason -> Fact.FatalFailure reason
