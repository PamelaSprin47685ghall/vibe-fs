namespace Wanxiangshu.Sphinx

open System
open System.Collections.Generic
open Fable.Core

module private SessionInterop =

    [<Import("randomUUID", "node:crypto")>]
    let randomUUID () : string = jsNative

type SessionStore() =
    let sessions = Dictionary<string, EpistemicState>()

    member _.Count = sessions.Count

    member _.TryState(handle: string) =
        match sessions.TryGetValue handle with
        | true, state -> Some state
        | false, _ -> None

    member _.Start(question: string) : obj =
        let state, result = Policy.start question

        match result with
        | InquiryResult.Error _ -> Codec.inquiryResultObject None result
        | _ ->
            let handle = SessionInterop.randomUUID ()
            sessions[handle] <- state
            Codec.inquiryResultObject (Some handle) result

    member _.Resume(handle: string, rawObservation: obj) : obj =
        if String.IsNullOrWhiteSpace handle then
            Codec.inquiryResultObject None (InquiryResult.Error "missing handle")
        else
            match sessions.TryGetValue handle with
            | false, _ -> Codec.inquiryResultObject (Some handle) (InquiryResult.Error "unknown handle")
            | true, state ->
                match Codec.decodeObservation rawObservation with
                | Error error -> Codec.inquiryResultObject (Some handle) (InquiryResult.Error error)
                | Ok observation ->
                    let next, result = Policy.resume state observation

                    match result with
                    | InquiryResult.Error _ -> ()
                    | _ -> sessions[handle] <- next

                    Codec.inquiryResultObject (Some handle) result

module Session =

    let defaultStore = SessionStore()

    let start question = defaultStore.Start question

    let resume handle observation =
        defaultStore.Resume(handle, observation)
