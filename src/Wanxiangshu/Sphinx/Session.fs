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

[<RequireQualifiedAccess>]
type SessionLifecycle =
    | Active
    | Answered of CanonicalAnswer

type SessionEntry = {
    State: EpistemicState
    Lifecycle: SessionLifecycle
}

type SessionSuccess = {
    Handle: string
    State: EpistemicState
    Result: InquiryResult
}

/// DSL-state-combination: domain
type SessionFailureView = {
    Handle: string option
    State: EpistemicState option
    Failure: SessionFailure
}

[<RequireQualifiedAccess>]
type SessionOutcome =
    | Success of SessionSuccess
    | Failure of SessionFailureView

[<RequireQualifiedAccess>]
type StartOutcome =
    | Started of handle: string * state: EpistemicState * result: InquiryResult
    | Rejected of message: string

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
        | StartOutcome.Rejected message ->
            result None (InquiryResult.Error message)
        | StartOutcome.Started (handle, _, inquiryResult) ->
            result (Some handle) inquiryResult

    let failureMessage (f: SessionFailure) : string =
        match f with
        | SessionFailure.MissingHandle -> "missing handle"
        | SessionFailure.UnknownHandle -> "unknown handle"
        | SessionFailure.InvalidObservation message -> message
        | SessionFailure.KernelRejected message -> message
        | SessionFailure.AlreadyAnswered -> "already answered"

    let sessionOutcomeToObj (outcome: SessionOutcome) : obj =
        match outcome with
        | SessionOutcome.Success success ->
            result (Some success.Handle) success.Result
        | SessionOutcome.Failure failure ->
            result failure.Handle (InquiryResult.Error (failureMessage failure.Failure))

    let resumeActive (handle: string) (entry: SessionEntry) (observation: Observation) (sessions: Dictionary<string, SessionEntry>) : SessionOutcome =
        match entry.Lifecycle with
        | SessionLifecycle.Answered _ ->
            SessionOutcome.Failure { Handle = Some handle; State = Some entry.State; Failure = SessionFailure.AlreadyAnswered }
        | SessionLifecycle.Active ->
            let nextState, result = Policy.resume entry.State observation
            match result with
            | InquiryResult.Error message ->
                SessionOutcome.Failure { Handle = Some handle; State = Some entry.State; Failure = SessionFailure.KernelRejected message }
            | InquiryResult.Yield _ ->
                sessions[handle] <- { State = nextState; Lifecycle = SessionLifecycle.Active }
                SessionOutcome.Success { Handle = handle; State = nextState; Result = result }
            | InquiryResult.Answered answer ->
                sessions[handle] <- { State = nextState; Lifecycle = SessionLifecycle.Answered answer }
                SessionOutcome.Success { Handle = handle; State = nextState; Result = result }

    let decodeAndResume (resumeFn: string * Observation -> SessionOutcome) (handle: string) (rawObservation: obj) : obj =
        match Codec.decodeObservation rawObservation with
        | Error error ->
            result (Some handle) (InquiryResult.Error error)
        | Ok observation ->
            resumeFn (handle, observation) |> sessionOutcomeToObj

module private SessionInterop =

    [<Import("randomUUID", "node:crypto")>]
    let randomUUID () : string = jsNative

type SessionStore() =
    let sessions = Dictionary<string, SessionEntry>()

    member _.Count = sessions.Count

    member _.TryState(handle: string) =
        match sessions.TryGetValue handle with
        | true, entry -> Some entry.State
        | false, _ -> None

    member _.StartTyped(question: string) : StartOutcome =
        let state, result = Policy.start question

        match result with
        | InquiryResult.Error message ->
            StartOutcome.Rejected message
        | _ ->
            let handle = SessionInterop.randomUUID ()
            sessions[handle] <- { State = state; Lifecycle = SessionLifecycle.Active }
            StartOutcome.Started(handle, state, result)

    member _.ResumeObservation(handle: string, observation: Observation) : SessionOutcome =
        if String.IsNullOrWhiteSpace handle then
            SessionOutcome.Failure { Handle = None; State = None; Failure = SessionFailure.MissingHandle }
        else
            match sessions.TryGetValue handle with
            | false, _ ->
                SessionOutcome.Failure { Handle = Some handle; State = None; Failure = SessionFailure.UnknownHandle }
            | true, entry ->
                SessionWire.resumeActive handle entry observation sessions

    member this.Start(question: string) : obj =
        this.StartTyped(question) |> SessionWire.startOutcomeToObj

    member this.Resume(handle: string, rawObservation: obj) : obj =
        if String.IsNullOrWhiteSpace handle then
            SessionWire.result None (InquiryResult.Error "missing handle")
        else
            match sessions.TryGetValue handle with
            | false, _ ->
                SessionWire.result (Some handle) (InquiryResult.Error "unknown handle")
            | true, _ ->
                let resumeFn (h, obs) = this.ResumeObservation(h, obs)
                SessionWire.decodeAndResume resumeFn handle rawObservation

module Session =

    let defaultStore = SessionStore()

    let start question = defaultStore.Start question

    let resume handle observation =
        defaultStore.Resume(handle, observation)
