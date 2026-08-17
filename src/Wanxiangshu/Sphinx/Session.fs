namespace Wanxiangshu.Sphinx

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop

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

module private SessionInterop =

    [<Import("randomUUID", "node:crypto")>]
    let randomUUID () : string = jsNative

type SessionStore() =
    let sessions = Dictionary<string, EpistemicState>()

    let persistResumeResult handle next result =
        match result with
        | InquiryResult.Error _ -> ()
        | _ -> sessions[handle] <- next

        SessionWire.result (Some handle) result

    let decodeAndResume handle state rawObservation =
        match Codec.decodeObservation rawObservation with
        | Error error -> SessionWire.result (Some handle) (InquiryResult.Error error)
        | Ok observation ->
            let next, result = Policy.resume state observation
            persistResumeResult handle next result

    let resumeKnownHandle handle rawObservation =
        match sessions.TryGetValue handle with
        | false, _ -> SessionWire.result (Some handle) (InquiryResult.Error "unknown handle")
        | true, state -> decodeAndResume handle state rawObservation

    member _.Count = sessions.Count

    member _.TryState(handle: string) =
        match sessions.TryGetValue handle with
        | true, state -> Some state
        | false, _ -> None

    member _.Start(question: string) : obj =
        let state, result = Policy.start question

        match result with
        | InquiryResult.Error _ -> SessionWire.result None result
        | _ ->
            let handle = SessionInterop.randomUUID ()
            sessions[handle] <- state
            SessionWire.result (Some handle) result

    member _.Resume(handle: string, rawObservation: obj) : obj =
        if String.IsNullOrWhiteSpace handle then
            SessionWire.result None (InquiryResult.Error "missing handle")
        else
            resumeKnownHandle handle rawObservation

module Session =

    let defaultStore = SessionStore()

    let start question = defaultStore.Start question

    let resume handle observation =
        defaultStore.Resume(handle, observation)
