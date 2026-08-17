namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// JS-native boundary for host signal subscription. The Fable
/// `Result<HostSignalSubscription option * string, string>` and the
/// `HostSignalSubscription` record never cross this edge; only a plain
/// `{ ok, source, dispose }` / `{ ok: false, error }` shape is observable.
module HostSignalSubscribeSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'function'")>]
    let private isFunction (value: obj) : bool = jsNative

    [<Emit("$0.tag === 0")>]
    let private resultIsOk (result: obj) : bool = jsNative

    [<Emit("$0.fields[0]")>]
    let private resultFields (result: obj) : obj = jsNative

    [<Emit("$0[0]")>]
    let private arrayItem0 (value: obj) : obj = jsNative

    [<Emit("$0[1]")>]
    let private arrayItem1 (value: obj) : obj = jsNative

    [<Emit("$0.Dispose()")>]
    let private invokeDispose (value: obj) : unit = jsNative

    let private onSignalEventOf (value: obj) : obj -> unit =
        if isFunction value then unbox<obj -> unit> value else fun _ -> ()

    /// Translate the Fable Result into a JS-native object. The subscription
    /// disposer is wrapped so the caller receives a plain `dispose()` function,
    /// never the Fable record.
    let private translate (result: obj) : obj =
        if resultIsOk result then
            let tuple = resultFields result
            let subscription = arrayItem0 tuple
            let source = arrayItem1 tuple :?> string
            let dispose = if isNullish subscription then null else box (fun () -> invokeDispose subscription)
            box {| ok = true; source = source; dispose = dispose |}
        else
            box {| ok = false; error = resultFields result :?> string |}

    /// JS-native trySubscribe. Returns a plain object:
    ///   success → { ok: true, source: string, dispose: () => void | null }
    ///   failure → { ok: false, error: string }
    let trySubscribe (input: obj) (onSignalEvent: obj) (timerPort: obj) : Task<obj> =
        let callback = onSignalEventOf onSignalEvent
        let port = if isNullish timerPort then None else Some(unbox<ITimerPort> timerPort)

        task {
            let! result = HostSignalSubscribe.trySubscribe input callback port
            return translate (box result)
        }
