namespace Wanxiangshu.Next.OpenCode

open Fable.Core
open Fable.Core.JsInterop

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
    /// `tests-mjs/Context/ctx014.test.mjs` 的正向用例。
    let AllowedFields =
        set
            [ "session_id"
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
              "duration" ]

    /// CTX-014 禁止字段。出现在 `src/Wanxiangshu.Next/**/*.fs` 即负向测试红灯（与灭绝表同机制）。
    let ForbiddenFields =
        set
            [ "overflow"
              "context_ratio"
              "estimated_tokens_remaining"
              "compression_needed" ]

    [<Emit("console.error($0)")>]
    let private error (message: string) : unit = jsNative

    [<Emit("JSON.stringify($0)")>]
    let private stringify (value: obj) : string = jsNative

    /// Kill this process hard. Gated off under node:test and WANXIANGSHU_NO_FATAL_EXIT=1
    /// so unit/canary harnesses can assert the fatal path without dying.
    [<Emit("""(() => {
      if (process.env.WANXIANGSHU_NO_FATAL_EXIT === '1') return;
      if (process.env.NODE_TEST_CONTEXT != null && process.env.NODE_TEST_CONTEXT !== '') return;
      try { process.kill(process.pid, 'SIGKILL'); } catch (_) { process.exit(1); }
    })()""")>]
    let private killSelf () : unit = jsNative

    let private validate (fields: (string * string) list) : unit =
        let illegal =
            fields
            |> List.map fst
            |> List.filter (fun name -> not (Set.contains name AllowedFields))

        if not (List.isEmpty illegal) then
            failwith (sprintf "CTX-014: 诊断字段不在白名单: %A" illegal)

    let private payload (operation: string) (fields: (string * string) list) : obj =
        createObj (
            Array.ofList (
                ("operation", box operation)
                :: (fields |> List.map (fun (name, value) -> name, box value))
            )
        )

    /// Expected / best-effort. Validates CTX-014 whitelist; never prints.
    let emit (operation: string) (fields: (string * string) list) : unit = validate fields

    /// Unexpected invariant break. Print one JSON line, then kill the process.
    let fatal (operation: string) (fields: (string * string) list) : unit =
        validate fields
        error (stringify (payload operation fields))
        killSelf ()
