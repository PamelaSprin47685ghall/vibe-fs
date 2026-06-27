module Wanxiangshu.Tests.ToolOutputInfoTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.ToolOutputInfoTypes

let private hint s = InfoItem.Hint s
let private syntax s = InfoItem.Syntax s
let private status s = InfoItem.Status s
let private exitCode n = InfoItem.ExitCode n
let private signal s = InfoItem.Signal s
let private timeoutMs n = InfoItem.TimeoutMs n
let private bodyRef r = InfoItem.BodyRef r

let testRenderEmpty () =
    equal "render empty msg" "" (render { info = []; body = "" })

let testRenderBodyOnly () =
    equal "render body only" "hello" (render { info = []; body = "hello" })

let testRenderHintOnly () =
    let r = render { info = [ hint "test" ]; body = "" }
    check "render hint starts with ---" (r.StartsWith "---")
    check "render hint contains hint:" (r.Contains "hint:")

let testRenderBodyAndInfo () =
    let r = render { info = [ hint "x" ]; body = "b" }
    check "body+info starts with ---" (r.StartsWith "---")
    check "body+info contains body" (r.Contains "b")

let testTryParseEmpty () =
    equal "tryParse empty None" None (tryParse "")
    equal "tryParse null None" None (tryParse null)

let testTryParseNonFrontMatter () =
    equal "tryParse no fm None" None (tryParse "hello")

let testTryParseValid () =
    // Flat front-matter format: ---\nhint: hello\n---
    let text = render { info = [ hint "hello" ]; body = "world" }
    match tryParse text with
    | Some msg ->
        equal "tryParse valid body" "world" msg.body
        check "tryParse valid hint" (List.exists (function InfoItem.Hint "hello" -> true | _ -> false) msg.info)
    | None -> check "tryParse valid should succeed" false

let testHasExactHint () =
    let text = render { info = [ hint "fixme"; hint "todo" ]; body = "" }
    check "hasExactHint match" (hasExactHint text "fixme")
    check "hasExactHint no match" (not (hasExactHint text "other"))

let testNoChangeEnvelope () =
    let r = noChangeEnvelope ()
    check "noChangeEnvelope starts with ---" (r.StartsWith "---")
    check "noChangeEnvelope has tool_output" (r.Contains "tool_output")
    match tryParse r with
    | Some msg ->
        check "noChangeEnvelope has correct BodyRef"
            (List.exists (function InfoItem.BodyRef ToolOutputBodyRef.NoChangeSincePreviousReadWrite -> true | _ -> false) msg.info)
    | None -> check "noChangeEnvelope parseable" false

let testSeeBelowEnvelope () =
    let r = seeBelowEnvelope "some output"
    check "seeBelowEnvelope has body" (r.Contains "some output")
    match tryParse r with
    | Some msg -> equal "seeBelowEnvelope body parsed" "some output" msg.body
    | None -> check "seeBelowEnvelope parseable" false

let testParseOrBody () =
    let text = "---\nstatus: done\n---\nresult data"
    let r = parseOrBody text
    equal "parseOrBody fm body" "result data" r.body
    let raw = parseOrBody "plain body"
    equal "parseOrBody raw body" "plain body" raw.body

let testAppendInfo () =
    let msg = { info = [ hint "a" ]; body = "" }
    equal "appendInfo count" 2 (List.length (appendInfo (hint "b") msg).info)

let testSetBodyRefReplacesBodyRef () =
    let msg = { info = [ hint "x"; bodyRef ToolOutputBodyRef.SeeBelow ]; body = "b" }
    let r = setBodyRef ToolOutputBodyRef.NoChangeSincePreviousReadWrite msg
    let brs = r.info |> List.choose (function InfoItem.BodyRef x -> Some x | _ -> None)
    equal "setBodyRef only one BodyRef" 1 (List.length brs)

let testBodyRefValue () =
    equal "SeeBelow" "/See Below/" (bodyRefValue ToolOutputBodyRef.SeeBelow)
    equal "SeeBelowTruncated" "/See Below, Truncated/" (bodyRefValue ToolOutputBodyRef.SeeBelowTruncated)
    equal "NoChange" "/No Change Since Previous Read/Write/" (bodyRefValue ToolOutputBodyRef.NoChangeSincePreviousReadWrite)

let testWithBookkeepingHints () =
    let r = withBookkeepingHints "some output"
    check "withBookkeepingHints has hint" (r.Contains "hint")
    check "withBookkeepingHints has SeeBelow" (r.Contains "/See Below/")

let testAddSyntax () =
    let r = addSyntax "code block" "fsharp"
    check "addSyntax has syntax" (r.Contains "syntax")
    check "addSyntax has body" (r.Contains "code block")
    equal "addSyntax empty preserves" "raw" (addSyntax "raw" "")

let testWithIterator () =
    let r = withIterator "body" "my-iter"
    check "withIterator has iterator" (r.Contains "my-iter")
    check "withIterator has SeeBelow" (r.Contains "/See Below/")
    equal "withIterator empty returns body" "body" (withIterator "body" "")

let testTodoWriteOutput () =
    let r = todoWriteOutput [ "methodology_a" ] false
    check "todoWriteOutput has methodology" (r.Contains "methodology_a")
    check "todoWriteOutput has SeeBelow" (r.Contains "/See Below/")
    let rMeta = todoWriteOutput [ "methodology_a" ] true
    check "todoWriteOutput with meditator" (rMeta.Contains "Think thrice")
    let rEmpty = todoWriteOutput [] false
    check "todoWriteOutput empty" (rEmpty.Contains "Todos updated.")

let testHintsFromOutput () =
    let text = render { info = [ hint "a"; syntax "py"; hint "b" ]; body = "" }
    let hs = hintsFromOutput text
    equal "hintsFromOutput count" 2 (List.length hs)
    let noHints = hintsFromOutput (render { info = [ syntax "py" ]; body = "" })
    equal "hintsFromOutput no hints" 0 (List.length noHints)
    equal "hintsFromOutput no fm" [] (hintsFromOutput "plain")

let testBodyForBookkeeper () =
    equal "bodyForBookkeeper extracts" "real body" (bodyForBookkeeper "---\ntool_output: /See Below/\n---\nreal body")
    equal "bodyForBookkeeper raw" "plain" (bodyForBookkeeper "plain")
    equal "bodyForBookkeeper null" null (bodyForBookkeeper null)

let testHintForMethodologies () =
    equal "hintForMethodologies empty" "Todos updated." (hintForMethodologies [])
    check "hintForMethodologies nonempty" ((hintForMethodologies [ "a" ]).Contains "a")
    check "hintForMethodologies multiple" ((hintForMethodologies [ "a"; "b" ]).Contains "b")

let testAppendMultiple () =
    let msg = { info = []; body = "" }
    let r = appendInfo (hint "a") msg |> appendInfo (syntax "py") |> appendInfo (status "done")
    equal "append info count" 3 (List.length r.info)
    check "append preserves original" (msg.info.Length = 0)

let testEmptyWithBody () =
    let msg = withBody "some body"
    equal "withBody body" "some body" msg.body
    check "withBody empty info" (List.isEmpty msg.info)

let testConstants () =
    check "hintExecutorMisuse nonempty" (hintExecutorMisuse.Length > 0)
    check "hintTodoRefresh nonempty" (hintTodoRefresh.Length > 0)
    check "hintMeditator nonempty" (hintMeditator.Length > 0)
    let r = hintMethodologyFollowup "methodology_axiomatization"
    check "hintMethodologyFollowup contains id" (r.Contains "methodology_axiomatization")

let testInfoItemOrderingBodyRefLast () =
    let r =
        render
            { info =
                [ bodyRef ToolOutputBodyRef.SeeBelow
                  hint "first hint"
                  syntax "python"
                  status "ok"
                  exitCode 0
                  signal ""
                  timeoutMs 1000
                  bodyRef ToolOutputBodyRef.NoChangeSincePreviousReadWrite ]
              body = "" }
    let lines = r.Split('\n') |> Array.toList
    let hintLine = lines |> List.tryFindIndex (fun l -> l.Contains "hint:")
    let toolOutLine = lines |> List.tryFindIndex (fun l -> l.Contains "tool_output:")
    match hintLine, toolOutLine with
    | Some hp, Some tp -> check "BodyRef lines appear after hint lines" (hp < tp)
    | _ -> check "ordering labels found" false

let testRenderProducesFlatFrontMatter () =
    let r = render { info = [ status "completed"; exitCode 0; bodyRef ToolOutputBodyRef.SeeBelow ]; body = "OUT" }
    check "flat fm has no info block" (not (r.Contains "info:"))
    check "flat fm top-level status" (r.Contains "status: completed")
    check "flat fm top-level exit_code" (r.Contains "exit_code: 0")
    check "flat fm top-level tool_output" (r.Contains "tool_output: /See Below/")
    check "flat fm retains body" (r.Contains "OUT")

let testRenderMultiHintAsArray () =
    let r = render { info = [ hint "a"; hint "b" ]; body = "" }
    check "multi hint uses array item a" (r.Contains "- a")
    check "multi hint uses array item b" (r.Contains "- b")
    match tryParse r with
    | Some msg ->
        let hs = msg.info |> List.choose (function InfoItem.Hint h -> Some h | _ -> None)
        equal "multi hint parsed as two" 2 (List.length hs)
    | None -> check "multi hint roundtrip parseable" false

let run () =
    testRenderEmpty ()
    testRenderBodyOnly ()
    testRenderHintOnly ()
    testRenderBodyAndInfo ()
    testTryParseEmpty ()
    testTryParseNonFrontMatter ()
    testTryParseValid ()
    testHasExactHint ()
    testNoChangeEnvelope ()
    testSeeBelowEnvelope ()
    testParseOrBody ()
    testAppendInfo ()
    testSetBodyRefReplacesBodyRef ()
    testBodyRefValue ()
    testWithBookkeepingHints ()
    testAddSyntax ()
    testWithIterator ()
    testTodoWriteOutput ()
    testHintsFromOutput ()
    testBodyForBookkeeper ()
    testHintForMethodologies ()
    testAppendMultiple ()
    testEmptyWithBody ()
    testConstants ()
    testInfoItemOrderingBodyRefLast ()
    testRenderProducesFlatFrontMatter ()
    testRenderMultiHintAsArray ()
