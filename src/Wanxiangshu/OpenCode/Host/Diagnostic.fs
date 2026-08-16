namespace Wanxiangshu.OpenCode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// CTX-014 / HOST-007 runtime diagnostics — two severities only:
///
///   emit  — expected / best-effort. No console. Never a recovery decision.
///   fatal — unexpected invariant break. Print once, then kill the process.
///
/// A log line must never become a recovery protocol (HOST-007). Classification
/// is at the call site: if the world can still continue, call `emit` (or nothing);
/// if the plugin is out of contract, call `fatal`.
module Diagnostic =

    /// CTX-014 允许字段清单。新增字段必须同时登记到白名单与
    /// `tests/unit/Context/ctx014.test.mjs` 的正向用例。
    let AllowedFields =
        set
            [ "session_id"
              // SPEC-INV-013: visible DryRun child identity; observation-only.
              "replica_session_id"
              "blogger_session_id"
              "operation"
              "request_kind"
              "offset"
              "side"
              "armed"
              "probe_available"
              "probe_used"
              "probe_promoted"
              "squash_attempted"
              "squash_committed"
              "frame_count_before"
              "frame_count_after"
              "cutoff_before"
              "cutoff_after"
              "delta_bytes"
              "result"
              "provider_error"
              "duration"
              // LOOP-010
              "weighted_distinct_token_count"
              "detector_step" ]

    /// CTX-014 禁止字段。出现在 `src/Wanxiangshu/**/*.fs` 即负向测试红灯（与灭绝表同机制）。
    let ForbiddenFields =
        set
            [ "overflow"
              "context_ratio"
              "estimated_tokens_remaining"
              "compression_needed" ]

    [<Emit("console.error($0)")>]
    let private error (message: string) : unit = jsNative

    [<Emit("process.env.WANXIANGSHU_DIAG === '1'")>]
    let private diagnosticsVisible () : bool = jsNative

    [<Emit("JSON.stringify($0)")>]
    let private stringify (value: obj) : string = jsNative

    let private validate (fields: (string * string) list) : unit =
        let illegal =
            fields
            |> List.map fst
            |> List.filter (fun name -> not (Set.contains name AllowedFields))

        if not (List.isEmpty illegal) then
            failwith (sprintf "CTX-014: 诊断字段不在白名单: %A" illegal)

    /// One JSON object: the operation plus its whitelisted fields.
    ///
    /// `Object.fromEntries` over explicit pairs rather than `createObj` over a tuple list: the
    /// shape of what gets printed is the one thing a diagnostic cannot afford to get wrong, and
    /// this spelling is checked by `CTX_014_diagnostic_records_carry_their_fields`.
    [<Emit("Object.fromEntries([['operation', $0]].concat($1))")>]
    let private payloadObject (operation: string) (entries: string array array) : obj = jsNative

    let private payload (operation: string) (fields: (string * string) list) : obj =
        fields
        |> List.map (fun (name, value) -> [| name; value |])
        |> List.toArray
        |> payloadObject operation

    /// Expected / best-effort. Validates the CTX-014 whitelist.
    ///
    /// Silent by default, and that default is the clause: HOST-007 forbids a log line from
    /// becoming a recovery protocol, and code that prints on the happy path invites exactly that.
    /// `WANXIANGSHU_DIAG=1` makes the same records observable without changing a single decision —
    /// which is what a stalled run needs, because the alternative measured in practice is a
    /// session parked with no explanation anywhere: every branch that could explain it emitted
    /// into silence.
    let emit (operation: string) (fields: (string * string) list) : unit =
        validate fields

        if diagnosticsVisible () then
            error (stringify (payload operation fields))

    /// Unexpected invariant break. Print one JSON line, then kill the process.
    let fatal (operation: string) (fields: (string * string) list) : unit =
        validate fields
        error (stringify (payload operation fields))
        FatalProcess.kill ()
