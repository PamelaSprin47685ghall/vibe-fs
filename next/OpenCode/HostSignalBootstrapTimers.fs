namespace Wanxiangshu.Next.OpenCode

open Fable.Core

/// Node-safe timers for Host signal side effects. Avoid Browser.Dom.
module HostSignalBootstrapTimers =

    [<Emit("setTimeout($0, $1)")>]
    let private setTimeoutJs (fn: unit -> unit) (ms: int) : obj = jsNative

    [<Emit("clearTimeout($0)")>]
    let private clearTimeoutJs (handle: obj) : unit = jsNative

    let deferMs (ms: int) (fn: unit -> unit) : obj = setTimeoutJs fn ms

    let clearTimeout (handle: obj) : unit =
        if not (isNull handle) then
            clearTimeoutJs handle
