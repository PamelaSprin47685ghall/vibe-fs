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
let private iterator s = InfoItem.Iterator s

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
    check "noChangeEnvelope has status" (r.Contains "status: No Change Since Previous Read/Write")
    match tryParse r with
    | Some msg ->
        let sts = msg.info |> List.choose (function InfoItem.Status s -> Some s | _ -> None)
        equal "noChangeEnvelope status value" noChangeStatus (sts |> List.head)
    | None -> check "noChangeEnvelope parseable" false

let testParseOrBody () =
    let text = "---\nstatus: done\n---\nresult data"
    let r = parseOrBody text
    equal "parseOrBody fm body" "result data" r.body
    let raw = parseOrBody "plain body"
    equal "parseOrBody raw body" "plain body" raw.body

let testAppendInfo () =
    let msg = { info = [ hint "a" ]; body = "" }
    equal "appendInfo count" 2 (List.length (appendInfo (hint "b") msg).info)

let testAddSyntax () =
    let r = addSyntax "code block" "fsharp"
    check "addSyntax has syntax" (r.Contains "syntax")
    check "addSyntax has body" (r.Contains "code block")
    equal "addSyntax empty preserves" "raw" (addSyntax "raw" "")

let testWithIterator () =
    let r = withIterator "body" "my-iter"
    check "withIterator has iterator" (r.Contains "my-iter")
    equal "withIterator empty returns body" "body" (withIterator "body" "")

let testTodoWriteOutput () =
    let r = todoWriteOutput [ "methodology" ] false
    check "todoWriteOutput has methodology" (r.Contains "methodology")
    let rMeta = todoWriteOutput [ "methodology" ] true
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
    let r = hintMethodologyFollowup "methodology"
    check "hintMethodologyFollowup contains id" (r.Contains "methodology")

let testInfoItemOrdering () =
    let r =
        render
            { info =
                [ hint "first hint"
                  syntax "python"
                  status "ok"
                  exitCode 0
                  iterator "iter-1" ]
              body = "" }
    let lines = r.Split('\n') |> Array.toList
    let hintLine = lines |> List.tryFindIndex (fun l -> l.Contains "hint:")
    let syntaxLine = lines |> List.tryFindIndex (fun l -> l.Contains "syntax:")
    match hintLine, syntaxLine with
    | Some hp, Some sp -> check "hint appears before syntax" (hp < sp)
    | _ -> check "ordering fields found" false

let testRenderMultiHintAsArray () =
    let r = render { info = [ hint "a"; hint "b" ]; body = "" }
    check "multi hint uses array item a" (r.Contains "- a")
    check "multi hint uses array item b" (r.Contains "- b")
    match tryParse r with
    | Some msg ->
        let hs = msg.info |> List.choose (function InfoItem.Hint h -> Some h | _ -> None)
        equal "multi hint parsed as two" 2 (List.length hs)
    | None -> check "multi hint roundtrip parseable" false

let testTryParseMergesMultipleFrontMatterBlocks () =
    let text = "---\nhint: a\n---\n---\nsyntax: json\n---\nbody after"
    match tryParse text with
    | Some msg ->
        equal "multi-block body" "body after" msg.body
        check "multi-block hint" (List.exists (function InfoItem.Hint "a" -> true | _ -> false) msg.info)
        check "multi-block syntax" (List.exists (function InfoItem.Syntax "json" -> true | _ -> false) msg.info)
    | None -> check "multi-block parse should succeed" false

let run () =
    testRenderEmpty ()
    testRenderBodyOnly ()
    testRenderHintOnly ()
    testRenderBodyAndInfo ()
    testTryParseEmpty ()
    testTryParseNonFrontMatter ()
    testTryParseValid ()
    testTryParseMergesMultipleFrontMatterBlocks ()
    testHasExactHint ()
    testNoChangeEnvelope ()
    testParseOrBody ()
    testAppendInfo ()
    testAddSyntax ()
    testWithIterator ()
    testTodoWriteOutput ()
    testHintsFromOutput ()
    testHintForMethodologies ()
    testAppendMultiple ()
    testEmptyWithBody ()
    testConstants ()
    testInfoItemOrdering ()
    testRenderMultiHintAsArray ()
