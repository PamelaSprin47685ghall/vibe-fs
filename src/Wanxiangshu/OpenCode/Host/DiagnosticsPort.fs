namespace Wanxiangshu.OpenCode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host.Ports

/// HNI P-12: the OpenCode adapter for the neutral diagnostics sink.
///
/// The host call is `client.app.log` — the OpenCode SDK verb that writes a
/// log entry to the server logs (`POST /log`, see
/// `node_modules/@opencode-ai/sdk/dist/gen/sdk.gen.js:559-572`). The body
/// shape is the SDK's `AppLogData`
/// (`node_modules/@opencode-ai/sdk/dist/gen/types.gen.d.ts:2842-2862`):
/// `{ service, level, message, extra? }`.
///
/// The neutral port has no error channel: `Emit` returns `unit`. A diagnostic
/// is a best-effort observation, never a recovery protocol (HOST-007), so a
/// failed host log write is dropped here — it must not throw into product code
/// and it must not break the turn. The write is not awaited: `Emit` is
/// synchronous, so the request is fired and its rejection is observed and
/// dropped instead of surfacing as an unhandled rejection.
module DiagnosticsPort =

    /// A host value is absent when it is `null` or `undefined`; a missing
    /// host property reads as `undefined`.
    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    /// A host object field; a missing field reads as `undefined`.
    [<Emit("$0 == null ? undefined : $0[$1]")>]
    let private field (value: obj) (name: string) : obj = jsNative

    /// Observe a host promise and drop its rejection. A failed log write is
    /// not an error the product may see: the neutral sink has no error channel
    [<Emit("(function () { if ($0 && typeof $0.then === 'function') { $0.then(function () {}, function () {}); } })()")>]
    let private dropRejection (promise: obj) : unit = jsNative

    /// The fields of a diagnostic, as the host's `extra` map.
    let private extraOf (fields: (string * string) list) : obj =
        fields |> List.map (fun (name, value) -> name, box value) |> createObj

    /// The host log body: the operation is the message, the fields ride along
    /// as `extra`. The SDK accepts one of four levels; the neutral port
    /// carries no severity, so every diagnostic is written at `info`.
    let private bodyOf (operation: string) (fields: (string * string) list) : obj =
        createObj
            [ "service", box "wanxiangshu"
              "level", box "info"
              "message", box operation
              "extra", box (extraOf fields) ]

    /// Safely post to the host log, swallowing both synchronous throws and
    /// promise rejections. A diagnostic must never throw into product code.
    let private tryPostLog (appObj: obj) (logFn: obj) (operation: string) (fields: (string * string) list) : unit =
        try
            let payload = createObj [ "body", box (bodyOf operation fields) ]
            let promise = logFn?call (appObj, payload)
            dropRejection promise
        with _ ->
            // A diagnostic that cannot be written is dropped: the neutral
            // port has no error channel, and a failed log must not break
            // the turn.
            ()

    /// The sink over the OpenCode SDK client (`client.app.log`).
    type SdkDiagnosticSink(client: obj) =
        interface IDiagnosticSink with
            member this.Emit(operation: string, fields: (string * string) list) : unit =
                let appObj = field client "app"
                let logFn = field appObj "log"

                if not (isNullish logFn) then
                    tryPostLog appObj logFn operation fields

    /// The sink for the given host wiring, or `None` when the wiring names no
    /// host that exposes a log verb.
    let create (input: obj) : IDiagnosticSink option =
        let client = if isNullish input then null else field input "client"

        if isNullish client then None
        elif isNullish (field (field client "app") "log") then None
        else Some(SdkDiagnosticSink(client) :> IDiagnosticSink)
