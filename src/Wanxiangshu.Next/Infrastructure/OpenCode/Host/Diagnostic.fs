namespace Wanxiangshu.Next.OpenCode

open Fable.Core
open Fable.Core.JsInterop

/// CTX-014: the single diagnostic emit for HOST-007.
///
/// Every diagnostic the plugin logs must carry only the allowed fields; the
/// forbidden set is structural here, so a field named `context_ratio` or
/// `estimated_tokens_remaining` cannot be emitted without failing this module
/// (fail closed, and the failure is a programming error — a log line must never
/// become a recovery decision).
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

    [<Emit("console.warn($0)")>]
    let private warn (message: string) : unit = jsNative

    [<Emit("JSON.stringify($0)")>]
    let private stringify (value: obj) : string = jsNative

    /// Emit one diagnostic. `operation` names the HOST-007 event; `fields` are
    /// validated against the whitelist and serialised as one JSON object, so a
    /// structured consumer can read them and a human gets one line.
    let emit (operation: string) (fields: (string * string) list) : unit =
        let illegal =
            fields
            |> List.map fst
            |> List.filter (fun name -> not (Set.contains name AllowedFields))

        if not (List.isEmpty illegal) then
            failwith (sprintf "CTX-014: 诊断字段不在白名单: %A" illegal)

        let payload =
            createObj (
                Array.ofList (
                    ("operation", box operation)
                    :: (fields |> List.map (fun (name, value) -> name, box value))
                )
            )

        warn (stringify payload)
