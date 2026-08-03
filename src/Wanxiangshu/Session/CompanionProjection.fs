namespace Wanxiangshu.Session

open Fable.Core
open Fable.Core.JsInterop

/// The one Host-object mutation the Companion transform performs.
///
/// In-place, and that is not a style preference. `plugin.trigger` discards the hook's
/// return value (`plugin/index.ts:284-293`) and the Host then reads its original
/// `msgs` binding (`prompt.ts:1262`), so `output.messages = rewritten` is silently
/// ignored: the provider receives the untouched transcript while every assertion
/// passes. Confirmed against Host source — `docs/archive/shock-anneal-2026/evidence/host-context-recovery.md`
/// item 1.
///
/// What used to live here was the whole context-estimation layer: a token estimator, a
/// reserved-output budget, an effective-context-limit calculation, and
/// `shouldSwitchEpoch`, which compared a projected token count against a model's window
/// to decide when to compress. CTX-001 forbids reading a window at all and CTX-002
/// forbids predicting overflow, so none of it had a legal caller once the failure-driven
/// protocol landed.
module CompanionProjection =

    let replaceMessagesInPlace (rawOutObj: obj) (transformed: obj list) =
        emitJsExpr (rawOutObj?messages, List.toArray transformed) "$0.length = 0; $0.push(...$1);"
        |> ignore
