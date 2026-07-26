namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop

/// Subscribes HostEventPort to OpenCode event streams.
/// Instance plugin/event is filtered by location.directory; worktree-scoped
/// agent prompts publish under a different directory, so we also open
/// /global/event and normalize its { directory, payload } envelope.
module HostEventSubscribe =

    [<Emit("$0()")>]
    let private invokeDisposer (value: obj) : unit = jsNative

    [<Emit("new AbortController()")>]
    let private newAbortController () : obj = jsNative

    /// Global SSE wraps instance events as { directory, payload: { id,type,properties } }.
    let normalizeHostEvent (rawEvent: obj) : obj =
        if isNull rawEvent then
            rawEvent
        elif not (isNull rawEvent?event) then
            rawEvent
        elif not (isNull rawEvent?payload) then
            let payload = rawEvent?payload

            if isNull payload then
                rawEvent
            elif not (isNull payload?``type``) || not (isNull payload?properties) then
                payload
            else
                rawEvent
        else
            rawEvent

    let observe (port: Events.HostEventPort) (rawEvent: obj) =
        port.Observe(normalizeHostEvent rawEvent)

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

    let private subscribeListen (events: obj) (port: Events.HostEventPort) : Result<IDisposable, string> =
        let listen = events?listen

        if isNull listen then
            Error "OPENCODE-EVENT-SUBSCRIBE: host event capability exists but events.listen is unavailable"
        else
            try
                let callback = box (fun rawEvent -> observe port rawEvent)
                let subscription = listen?call (events, callback)

                if isNull subscription then
                    Error "OPENCODE-EVENT-SUBSCRIBE: events.listen returned no subscription"
                else
                    Ok(
                        { new IDisposable with
                            member _.Dispose() = invokeDisposer subscription }
                    )
            with ex ->
                Error(sprintf "OPENCODE-EVENT-SUBSCRIBE: %s" ex.Message)

    /// Drain /global/event so worktree-directory terminals still complete runs.
    /// Soft-fails: missing global.event is Ok None (plugin event hook remains).
    let private subscribeGlobalEvent (client: obj) (port: Events.HostEventPort) : IDisposable option =
        if isNull client then
            None
        else
            let globalApi = client?``global``

            if isNull globalApi || isNull globalApi?event then
                None
            else
                try
                    let abortCtrl = newAbortController ()
                    let onEvent = box (fun data -> observe port data)

                    let options =
                        createObj
                            [ "signal", abortCtrl?signal
                              "onSseEvent",
                              box (fun (evt: obj) ->
                                  if not (isNull evt) then
                                      let data = if isNull evt?data then evt else evt?data
                                      observe port data) ]

                    // client.global.event is async (SDK get.sse). Await the result,
                    // then drain stream so onSseEvent keeps firing.
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

    let trySubscribeHostEvents (input: obj) (port: Events.HostEventPort) : Result<IDisposable option, string> =
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
                match subscribeListen events port with
                | Error err -> Error err
                | Ok sub -> Ok(Some sub)

        match listenSub with
        | Error err -> Error err
        | Ok localSub ->
            let globalSub = subscribeGlobalEvent client port

            let parts =
                [ match localSub with
                  | Some s -> yield s
                  | None -> ()
                  match globalSub with
                  | Some s -> yield s
                  | None -> () ]

            Ok(combine parts)
