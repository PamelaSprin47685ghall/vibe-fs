module Wanxiangshu.Tests.ToolOutputInfoTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.Tooling.ToolOutputToml

let testRenderEmpty () =
    equal "render empty msg" "" (render empty)

let testRenderBodyOnly () =
    equal "render body only" "body = \"hello\"\n" (render (withBody "hello"))

let testRenderHintOnly () =
    let r = render { empty with hint = Some "test" }
    check "render hint contains hint text" (r.Contains "test")
    check "render hint uses flat key" (r.Contains "hint =")

let testRenderBodyAndInfo () =
    let r = render { empty with body = Some "b"; hint = Some "x" }
    check "body+info contains body" (r.Contains "body = \"b\"")
    check "body+info contains hint" (r.Contains "hint = \"x\"")

let testNoChangeEnvelope () =
    let r = noChangeEnvelope ()
    check "noChangeEnvelope has status" (r.Contains noChangeStatus)
    check "noChangeEnvelope uses flat status" (r.Contains "status =")

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

let testEmptyWithBody () =
    let msg = withBody "some body"
    equal "withBody body" (Some "some body") msg.body

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
    testAddSyntax ()
    testWithIterator ()
    testTodoWriteOutput ()
    testHintForMethodologies ()
    testEmptyWithBody ()
    testConstants ()
