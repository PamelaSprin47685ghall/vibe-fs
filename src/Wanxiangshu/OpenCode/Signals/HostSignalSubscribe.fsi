namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation

module HostSignalSubscribe =
    type SignalHealth =
        { IsConnected: bool
          ReconnectAttempts: int }

    type HostSignalSubscription =
        { Health: unit -> SignalHealth
          Dispose: unit -> unit }

    val trySubscribe:
        input: obj ->
        onSignalEvent: (obj -> unit) ->
        _timerPort: ITimerPort option ->
            Task<Result<HostSignalSubscription option * string, string>>
