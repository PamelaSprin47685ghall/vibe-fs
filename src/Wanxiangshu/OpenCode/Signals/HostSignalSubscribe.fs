namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

module HostSignalSubscribe =

    [<RequireQualifiedAccess>]
    type HostSignalSubscriptionError =
        | InvalidInput
        | EventsListenUnavailable
        | EventsListenReturnedInvalidDisposer
        | EventsListenFailed of diagnostic: string

    [<Sealed>]
    type HostSignalSubscription internal (dispose: unit -> unit) =
        interface IDisposable with
            member _.Dispose() = dispose ()

    [<RequireQualifiedAccess>]
    type HostSignalSubscriptionMode =
        | LocalEventHook
        | EventsListen of HostSignalSubscription

    [<Emit("typeof $0 === 'function'")>]
    let private isFunction (value: obj) : bool = jsNative

    [<Emit("(() => { try { if ($0 === null || typeof $0 !== 'object' || Array.isArray($0)) return false; const p = Object.getPrototypeOf($0); return p === Object.prototype || p === null; } catch { return false; } })()")>]
    let private isPlainRecord (value: obj) : bool = jsNative

    [<Emit("(() => { try { const message = $0?.message; return typeof message === 'string' ? message : String($0); } catch { return 'unknown JavaScript exception'; } })()")>]
    let private exceptionDiagnostic (value: obj) : string = jsNative

    [<Emit("$0()")>]
    let private invokeDisposer (value: obj) : unit = jsNative

    let private subscribeValue events listen onSignalEvent =
        let subscription = listen?call (events, box onSignalEvent)

        if isFunction subscription then
            new HostSignalSubscription(fun () -> invokeDisposer subscription)
            |> HostSignalSubscriptionMode.EventsListen
            |> Ok
        else
            Error HostSignalSubscriptionError.EventsListenReturnedInvalidDisposer

    let private invokeListen events listen onSignalEvent =
        try
            subscribeValue events listen onSignalEvent
        with ex ->
            Error(HostSignalSubscriptionError.EventsListenFailed(exceptionDiagnostic (box ex)))

    let private readListen events =
        try
            Ok events?listen
        with ex ->
            Error(HostSignalSubscriptionError.EventsListenFailed(exceptionDiagnostic (box ex)))

    let private useListen events onSignalEvent listen =
        if isFunction listen then
            invokeListen events listen onSignalEvent
        else
            Error HostSignalSubscriptionError.EventsListenUnavailable

    let private subscribeListen events onSignalEvent =
        readListen events |> Result.bind (useListen events onSignalEvent)

    let private optionalPlainRecord value =
        if isNull value then Ok None
        elif isPlainRecord value then Ok(Some value)
        else Error HostSignalSubscriptionError.InvalidInput

    let private readClientEvents client =
        try
            Ok client?events
        with _ ->
            Error HostSignalSubscriptionError.InvalidInput

    let private clientEvents =
        function
        | None -> Ok None
        | Some client -> readClientEvents client |> Result.bind optionalPlainRecord

    let private readInputEvents input =
        try
            Ok input?events
        with _ ->
            Error HostSignalSubscriptionError.InvalidInput

    let private readInputClient input =
        try
            Ok input?client
        with _ ->
            Error HostSignalSubscriptionError.InvalidInput

    let private listenTargetFromInput input =
        readInputEvents input
        |> Result.bind optionalPlainRecord
        |> Result.bind (function
            | Some events -> Ok(Some events)
            | None ->
                readInputClient input
                |> Result.bind optionalPlainRecord
                |> Result.bind clientEvents)

    let private listenTarget input =
        if isPlainRecord input then
            listenTargetFromInput input
        else
            Error HostSignalSubscriptionError.InvalidInput

    let trySubscribe (input: obj) (onSignalEvent: obj -> unit) =
        task {
            return
                match listenTarget input with
                | Error error -> Error error
                | Ok(Some events) -> subscribeListen events onSignalEvent
                | Ok None -> Ok HostSignalSubscriptionMode.LocalEventHook
        }
