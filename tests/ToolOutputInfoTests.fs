module Wanxiangshu.Tests.ToolOutputInfoTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.Tooling.ToolOutputToml

let private hint s = InfoItem.Hint s
let private syntax s = InfoItem.Syntax s
let private status s = InfoItem.Status s
let private exitCode n = InfoItem.ExitCode n
let private iterator s = InfoItem.Iterator s

let testRenderEmpty () =
    equal "render empty msg" "" (render { info = []; body = "" })

let testRenderBodyOnly () =
    equal "render body only" "body = \"hello\"\n" (render { info = []; body = "hello" })

let testRenderHintOnly () =
    let r = render { info = [ hint "test" ]; body = "" }
    check "render hint contains body" (r.Contains "body = \"\"")
    check "render hint contains info table" (r.Contains "[[info]]")
    check "render hint contains hint text" (r.Contains "test")

let testRenderBodyAndInfo () =
    let r = render { info = [ hint "x" ]; body = "b" }
    check "body+info contains body" (r.Contains "body = \"b\"")
    check "body+info contains info" (r.Contains "[[info]]")

let testNoChangeEnvelope () =
    let r = noChangeEnvelope ()
    check "noChangeEnvelope contains info" (r.Contains "[[info]]")
    check "noChangeEnvelope has status" (r.Contains noChangeStatus)

let testAppendInfo () =
    let msg = { info = [ hint "a" ]; body = "" }
    equal "appendInfo count" 2 (List.length (appendInfo (hint "b") msg).info)

let testAddSyntax () =
    let r = addSyntax "code block" "fsharp"
    check "addSyntax has syntax" (r.Contains "fsharp")
    check "addSyntax has body" (r.Contains "code block")
    equal "addSyntax empty preserves" "raw" (addSyntax "raw" "")

let testWithIterator () =
    let r = withIterator "body" "my-iter"
    check "withIterator has iterator" (r.Contains "my-iter")
    equal "withIterator empty returns body" "body" (withIterator "body" "")

let testTodoWriteOutput () =
    let r = todoWriteOutput [ "methodology" ]
    check "todoWriteOutput has methodology" (r.Contains "methodology")
    let rEmpty = todoWriteOutput []
    check "todoWriteOutput empty" (rEmpty.Contains "Todos updated.")

let testHintForMethodologies () =
    equal "hintForMethodologies empty" "Todos updated." (hintForMethodologies [])
    check "hintForMethodologies nonempty" ((hintForMethodologies [ "a" ]).Contains "a")
    check "hintForMethodologies multiple" ((hintForMethodologies [ "a"; "b" ]).Contains "b")

let testAppendMultiple () =
    let msg = { info = []; body = "" }

    let r =
        appendInfo (hint "a") msg
        |> appendInfo (syntax "py")
        |> appendInfo (status "done")

    equal "append info count" 3 (List.length r.info)
    check "append preserves original" (msg.info.Length = 0)

let testEmptyWithBody () =
    let msg = withBody "some body"
    equal "withBody body" "some body" msg.body
    check "withBody empty info" (List.isEmpty msg.info)

let testConstants () =
    check "hintExecutorMisuse nonempty" (hintExecutorMisuse.Length > 0)
    check "hintTodosUpdated nonempty" (hintTodosUpdated.Length > 0)
    let r = hintMethodologyFollowup "methodology"
    check "hintMethodologyFollowup contains id" (r.Contains "methodology")

let run () =
    testRenderEmpty ()
    testRenderBodyOnly ()
    testRenderHintOnly ()
    testRenderBodyAndInfo ()
    testNoChangeEnvelope ()
    testAppendInfo ()
    testAddSyntax ()
    testWithIterator ()
    testTodoWriteOutput ()
    testHintForMethodologies ()
    testAppendMultiple ()
    testEmptyWithBody ()
    testConstants ()
