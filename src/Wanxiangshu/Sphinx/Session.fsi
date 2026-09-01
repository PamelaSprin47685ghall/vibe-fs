namespace Wanxiangshu.Sphinx

[<RequireQualifiedAccess>]
type SessionFailure =
    | MissingHandle
    | UnknownHandle
    | InvalidObservation of message: string
    | KernelRejected of message: string
    | AlreadyAnswered

type SessionEntry =
    { State: EpistemicState
      LastResult: InquiryResult }

type SessionSuccess =
    { Handle: string
      State: EpistemicState
      Result: InquiryResult }

type SessionFailureView =
    { Handle: string option
      State: EpistemicState option
      Failure: SessionFailure }

[<RequireQualifiedAccess>]
type SessionOutcome =
    | Success of SessionSuccess
    | Failure of SessionFailureView

[<RequireQualifiedAccess>]
type StartOutcome =
    | Started of handle: string * state: EpistemicState * result: InquiryResult
    | Rejected of message: string

[<RequireQualifiedAccess>]
type SessionStatus =
    | Active of state: EpistemicState
    | Answered of answer: CanonicalAnswer * state: EpistemicState

[<RequireQualifiedAccess>]
type LookupOutcome<'Value> =
    | Found of handle: string * value: 'Value
    | MissingHandle
    | UnknownHandle of handle: string

[<Sealed>]
type SessionStore =
    new: unit -> SessionStore
    member Count: int
    member TryState: handle: string -> EpistemicState option
    member StartTyped: question: string -> StartOutcome
    member ResumeObservation: handle: string * observation: Observation -> SessionOutcome
    member Status: handle: string -> LookupOutcome<SessionStatus>
    member Cancel: handle: string -> LookupOutcome<unit>
    member Start: question: string -> obj
    member Resume: handle: string * rawObservation: obj -> obj

module Session =
    val defaultStore: SessionStore
    val start: question: string -> obj
    val resume: handle: string -> observation: obj -> obj
