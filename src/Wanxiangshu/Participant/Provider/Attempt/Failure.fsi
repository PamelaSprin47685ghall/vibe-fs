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
    val classify: evidence: ProviderFailureEvidence -> ProviderFailureObservation
