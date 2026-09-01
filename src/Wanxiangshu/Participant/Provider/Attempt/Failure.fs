namespace Wanxiangshu.Participant.Provider.Attempt

open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type ProviderFailureStatus =
    | Transient
    | Permanent

type ProviderFailureEvidence =
    { ProviderRun: ProviderRunIdentity
      RequestKind: ProviderRequestKind
      Status: ProviderFailureStatus
      FirstTokenObserved: bool
      Diagnostic: string }

type ProviderFailureObservation =
    { Failure: ExecutionFailure
      ProviderRun: ProviderRunIdentity
      RequestKind: ProviderRequestKind
      FirstTokenObserved: bool
      Diagnostic: string }

[<RequireQualifiedAccess>]
module ProviderFailure =

    let classify (evidence: ProviderFailureEvidence) : ProviderFailureObservation =
        let failure =
            match evidence.FirstTokenObserved, evidence.Status with
            | true, ProviderFailureStatus.Transient
            | true, ProviderFailureStatus.Permanent -> ExecutionFailure.StreamInterruptedAfterFirstToken
            | false, ProviderFailureStatus.Transient -> ExecutionFailure.ProviderTransient
            | false, ProviderFailureStatus.Permanent -> ExecutionFailure.ProviderPermanent

        { Failure = failure
          ProviderRun = evidence.ProviderRun
          RequestKind = evidence.RequestKind
          FirstTokenObserved = evidence.FirstTokenObserved
          Diagnostic = evidence.Diagnostic }
