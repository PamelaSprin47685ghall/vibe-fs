// primary_owner: epistemic-reasoning — EpistemicReasoning.SurfaceSurface — KEEP — epistemic-reasoning-surface verified
namespace Wanxiangshu.Sphinx

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop

[<RequireQualifiedAccess>]
type SessionFailure =
    | MissingHandle
    | UnknownHandle
    | InvalidObservation of message: string
    | KernelRejected of message: string
    | AlreadyAnswered

/// Session entry stores the domain fact (last InquiryResult) directly.
/// The lifecycle projection (active vs answered) is a pure fold of
/// LastResult, not a separate mutable state machine — exorcised from
/// the former SessionLifecycle DU that duplicated InquiryResult's shape.
type SessionEntry =
    { State: EpistemicState
      LastResult: InquiryResult }

type SessionSuccess =
    { Handle: string
      State: EpistemicState
      Result: InquiryResult }

/// DSL-state-combination: domain
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

module private SessionWire =

    let result (handle: string option) (inquiryResult: InquiryResult) =
        let handleField =
            match handle with
            | Some value -> [ "handle" ==> value ]
            | None -> []

        match inquiryResult with
        | InquiryResult.Yield request ->
            createObj (
                handleField
                @ [ "status" ==> "yield"; "request" ==> Codec.requestObject request ]
            )
        | InquiryResult.Answered answer ->
            createObj (
                handleField
                @ [ "status" ==> "answered"; "answer" ==> Codec.answerObject answer ]
            )
        | InquiryResult.Error error -> createObj (handleField @ [ "status" ==> "error"; "error" ==> error ])

    let startOutcomeToObj (outcome: StartOutcome) : obj =
        match outcome with
        | StartOutcome.Rejected message -> result None (InquiryResult.Error message)
        | StartOutcome.Started(handle, _, inquiryResult) -> result (Some handle) inquiryResult

    let failureMessage (f: SessionFailure) : string =
        match f with
        | SessionFailure.MissingHandle -> "missing handle"
        | SessionFailure.UnknownHandle -> "unknown handle"
        | SessionFailure.InvalidObservation message -> message
        | SessionFailure.KernelRejected message -> message
        | SessionFailure.AlreadyAnswered -> "already answered"

    let sessionOutcomeToObj (outcome: SessionOutcome) : obj =
        match outcome with
        | SessionOutcome.Success success -> result (Some success.Handle) success.Result
        | SessionOutcome.Failure failure -> result failure.Handle (InquiryResult.Error(failureMessage failure.Failure))

    let applyResumeResult
        (handle: string)
        (entry: SessionEntry)
        (nextState: EpistemicState)
        (result: InquiryResult)
        (sessions: Dictionary<string, SessionEntry>)
        : SessionOutcome =
        match result with
        | InquiryResult.Error message ->
            SessionOutcome.Failure
                { Handle = Some handle
                  State = Some entry.State
                  Failure = SessionFailure.KernelRejected message }
        | InquiryResult.Yield _ ->
            sessions[handle] <-
                { State = nextState
                  LastResult = result }

            SessionOutcome.Success
                { Handle = handle
                  State = nextState
                  Result = result }
        | InquiryResult.Answered answer ->
            sessions[handle] <-
                { State = nextState
                  LastResult = result }

            SessionOutcome.Success
                { Handle = handle
                  State = nextState
                  Result = result }

    let resumeActive
        (handle: string)
        (entry: SessionEntry)
        (observation: Observation)
        (sessions: Dictionary<string, SessionEntry>)
        : SessionOutcome =
        match entry.LastResult with
        | InquiryResult.Answered _ ->
            SessionOutcome.Failure
                { Handle = Some handle
                  State = Some entry.State
                  Failure = SessionFailure.AlreadyAnswered }
        | _ ->
            let nextState, result = Policy.resume entry.State observation
            applyResumeResult handle entry nextState result sessions

    let decodeAndResume
        (resumeFn: string * Observation -> SessionOutcome)
        (handle: string)
        (rawObservation: obj)
        : obj =
        match Codec.decodeObservation rawObservation with
        | Error error -> result (Some handle) (InquiryResult.Error error)
        | Ok observation -> resumeFn (handle, observation) |> sessionOutcomeToObj

module private SessionInterop =

    [<Import("randomUUID", "node:crypto")>]
    let randomUUID () : string = jsNative

    let statusOfEntry (handle: string) (entry: SessionEntry) : LookupOutcome<SessionStatus> =
        match entry.LastResult with
        | InquiryResult.Answered answer -> LookupOutcome.Found(handle, SessionStatus.Answered(answer, entry.State))
        | _ -> LookupOutcome.Found(handle, SessionStatus.Active entry.State)

type SessionStore() =
    // DSL-MUTABLE: resource — Sphinx session registry by handle.
    let sessions = Dictionary<string, SessionEntry>()

    member _.Count = sessions.Count

    member _.TryState(handle: string) =
        match sessions.TryGetValue handle with
        | true, entry -> Some entry.State
        | false, _ -> None

    member _.StartTyped(question: string) : StartOutcome =
        let state, result = Policy.start question

        match result with
        | InquiryResult.Error message -> StartOutcome.Rejected message
        | _ ->
            let handle = SessionInterop.randomUUID ()

            sessions[handle] <- { State = state; LastResult = result }

            StartOutcome.Started(handle, state, result)

    member _.ResumeObservation(handle: string, observation: Observation) : SessionOutcome =
        let isBlank = String.IsNullOrWhiteSpace handle

        match isBlank, sessions.TryGetValue handle with
        | true, _ ->
            SessionOutcome.Failure
                { Handle = None
                  State = None
                  Failure = SessionFailure.MissingHandle }
        | false, (false, _) ->
            SessionOutcome.Failure
                { Handle = Some handle
                  State = None
                  Failure = SessionFailure.UnknownHandle }
        | false, (true, entry) -> SessionWire.resumeActive handle entry observation sessions

    member _.Status(handle: string) : LookupOutcome<SessionStatus> =
        match String.IsNullOrWhiteSpace handle, sessions.TryGetValue handle with
        | true, _ -> LookupOutcome.MissingHandle
        | _, (false, _) -> LookupOutcome.UnknownHandle handle
        | _, (true, entry) -> SessionInterop.statusOfEntry handle entry

    member _.Cancel(handle: string) : LookupOutcome<unit> =
        match String.IsNullOrWhiteSpace handle, sessions.TryGetValue handle with
        | true, _ -> LookupOutcome.MissingHandle
        | _, (false, _) -> LookupOutcome.UnknownHandle handle
        | _, (true, _) ->
            sessions.Remove handle |> ignore
            LookupOutcome.Found(handle, ())

    member this.Start(question: string) : obj =
        this.StartTyped(question) |> SessionWire.startOutcomeToObj

    member this.Resume(handle: string, rawObservation: obj) : obj =
        let isBlank = String.IsNullOrWhiteSpace handle

        match isBlank, sessions.TryGetValue handle with
        | true, _ -> SessionWire.result None (InquiryResult.Error "missing handle")
        | false, (false, _) -> SessionWire.result (Some handle) (InquiryResult.Error "unknown handle")
        | false, (true, _) ->
            let resumeFn (h, obs) = this.ResumeObservation(h, obs)
            SessionWire.decodeAndResume resumeFn handle rawObservation

module Session =

    let defaultStore = SessionStore()

    let start question = defaultStore.Start question

    let resume handle observation =
        defaultStore.Resume(handle, observation)
