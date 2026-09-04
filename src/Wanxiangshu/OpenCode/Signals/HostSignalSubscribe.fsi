namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks

module HostSignalSubscribe =
    [<RequireQualifiedAccess>]
    type HostSignalSubscriptionError =
        | InvalidInput
        | EventsListenUnavailable
        | EventsListenReturnedInvalidDisposer
        | EventsListenFailed of diagnostic: string

    [<Sealed>]
    type HostSignalSubscription =
        interface IDisposable

    [<RequireQualifiedAccess>]
    type HostSignalSubscriptionMode =
        | LocalEventHook
        | EventsListen of HostSignalSubscription

    val trySubscribe:
        input: obj ->
        onSignalEvent: (obj -> unit) ->
            Task<Result<HostSignalSubscriptionMode, HostSignalSubscriptionError>>
