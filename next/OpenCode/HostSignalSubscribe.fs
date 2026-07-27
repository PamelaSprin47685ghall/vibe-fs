namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop

/// Subscribes only coarse host signals. Message/part storms are discarded in JS
/// before crossing into F# business code. At most one global listener per runtime.
module HostSignalSubscribe =

    [<Emit("$0()")>]
    let private invokeDisposer (value: obj) : unit = jsNative

    [<Emit("new AbortController()")>]
    let private newAbortController () : obj = jsNative

    let private eventTypeOf (raw: obj) =
        if isNull raw then
            ""
        elif not (isNull raw?``type``) then
            unbox<string> raw?``type``
        elif not (isNull raw?payload) && not (isNull raw?payload?``type``) then
            unbox<string> raw?payload?``type``
        else
            ""

    let private isSignalEvent (raw: obj) =
        match eventTypeOf raw with
        | "session.status"
        | "session.idle"
        | "session.deleted" -> true
        | _ -> false

    let private unwrap (raw: obj) =
        if isNull raw then
            raw
        elif not (isNull raw?payload) then
            let payload = raw?payload

            if not (isNull raw?directory) && isNull payload?directory then
                payload?directory <- raw?directory

            payload
        else
            raw

    let private combine (parts: IDisposable list) : IDisposable option =
        match parts with
        | [] -> None
        | [ single ] -> Some single
        | many ->
            Some(
                { new IDisposable with
                    member _.Dispose() =
                        for d in many do
                            d.Dispose() }
            )

    let private subscribeListen (events: obj) (onSignalEvent: obj -> unit) : Result<IDisposable, string> =
        let listen = events?listen

        if isNull listen then
            Error "OPENCODE-SIGNAL-SUBSCRIBE: events.listen unavailable"
        else
            try
                let callback =
                    box (fun rawEvent ->
                        if isSignalEvent rawEvent then
                            onSignalEvent (unwrap rawEvent))

                let subscription = listen?call (events, callback)

                if isNull subscription then
                    Error "OPENCODE-SIGNAL-SUBSCRIBE: events.listen returned no subscription"
                else
                    Ok(
                        { new IDisposable with
                            member _.Dispose() = invokeDisposer subscription }
                    )
            with ex ->
                Error(sprintf "OPENCODE-SIGNAL-SUBSCRIBE: %s" ex.Message)

    let private subscribeGlobalEvent (client: obj) (onSignalEvent: obj -> unit) : IDisposable option =
        if isNull client then
            None
        else
            let globalApi = client?``global``

            if isNull globalApi || isNull globalApi?event then
                None
            else
                try
                    let abortCtrl = newAbortController ()

                    let onEvent =
                        box (fun data ->
                            let normalized = unwrap data

                            if isSignalEvent normalized then
                                onSignalEvent normalized)

                    let options =
                        createObj
                            [ "signal", abortCtrl?signal
                              "onSseEvent",
                              box (fun (evt: obj) ->
                                  if not (isNull evt) then
                                      let data = if isNull evt?data then evt else evt?data
                                      let normalized = unwrap data

                                      if isSignalEvent normalized then
                                          onSignalEvent normalized) ]

                    emitJsExpr
                        (globalApi, options, onEvent, abortCtrl)
                        """
                        ((globalApi, options, onEvent, abortCtrl) => {
                          (async () => {
                            try {
                              const result = await globalApi.event(options);
                              const stream = result && result.stream ? result.stream : result;
                              if (!stream || typeof stream[Symbol.asyncIterator] !== 'function') return;
                              for await (const data of stream) {
                                if (abortCtrl.signal.aborted) break;
                                onEvent(data);
                              }
                            } catch (_) {}
                          })();
                        })($0, $1, $2, $3)
                        """

                    Some(
                        { new IDisposable with
                            member _.Dispose() =
                                try
                                    abortCtrl?abort ()
                                with _ ->
                                    () }
                    )
                with _ ->
                    None

    let trySubscribe
        (input: obj)
        (onSignalEvent: obj -> unit)
        : Result<IDisposable option, string> =
        let listenTarget =
            if isNull input then
                None
            elif not (isNull input?events) then
                Some input?events
            else
                let client = input?client

                if not (isNull client) && not (isNull client?events) then
                    Some client?events
                else
                    None

        let client = if isNull input then null else input?client

        let listenSub =
            match listenTarget with
            | None -> Ok None
            | Some events ->
                match subscribeListen events onSignalEvent with
                | Error err -> Error err
                | Ok sub -> Ok(Some sub)

        match listenSub with
        | Error err -> Error err
        | Ok localSub ->
            let globalSub = subscribeGlobalEvent client onSignalEvent

            let parts =
                [ match localSub with
                  | Some s -> yield s
                  | None -> ()
                  match globalSub with
                  | Some s -> yield s
                  | None -> () ]

            Ok(combine parts)
