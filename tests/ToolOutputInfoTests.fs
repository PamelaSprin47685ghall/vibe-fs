module Wanxiangshu.Tests.ToolOutputInfoTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.Tooling.ToolOutputToml
open Wanxiangshu.Runtime.Tooling.ToolOutputPtyToml

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

[<Import("parse", "smol-toml")>]
let private parseToml (text: string) : obj = jsNative

let testPtySpawnTomlFlatFields () =
    let info: PtySpawnInfo =
        { id = "pty_100"
          title = "dev-server"
          command = "npm run dev"
          workdir = "/workspace"
          pid = 1234
          status = "running"
          notifyOnExit = false
          timeoutSeconds = "none"
          message = "PTY session spawned." }

    let text = renderPtySpawn info
    let parsed = parseToml text
    equal "pty spawn id" "pty_100" (string parsed?id)
    equal "pty spawn status" "running" (string parsed?status)
    equal "pty spawn command" "npm run dev" (string parsed?command)
    equal "pty spawn pid" 1234 (unbox<int> parsed?pid)
    equal "pty spawn message" "PTY session spawned." (string parsed?message)

let testPtyKillTomlFlatFields () =
    let info: PtyKillInfo =
        { id = "pty_100"
          action = "killed"
          cleanup = true
          title = "dev-server"
          command = "npm run dev"
          status = "stopped"
          finalLineCount = 42
          note = "session removed"
          message = "killed pty_100 (session removed)." }

    let text = renderPtyKill info
    let parsed = parseToml text
    equal "pty kill id" "pty_100" (string parsed?id)
    equal "pty kill action" "killed" (string parsed?action)
    equal "pty kill status" "stopped" (string parsed?status)
    equal "pty kill line count" 42 (unbox<int> parsed?final_line_count)

let testPtyReadTomlFlatFields () =
    let info: PtyReadInfo =
        { id = "pty_100"
          status = "running"
          offset = 0
          returned = 2
          totalLines = 10
          hasMore = true
          pattern = None
          totalMatches = None
          lines = [ "line1"; "line2" ]
          continuationHint = Some "more lines" }

    let text = renderPtyRead info
    let parsed = parseToml text
    equal "pty read id" "pty_100" (string parsed?id)
    equal "pty read status" "running" (string parsed?status)
    equal "pty read returned" 2 (unbox<int> parsed?returned)
    equal "pty read lines len" 2 (unbox<obj array> parsed?lines).Length
    equal "pty read first line" "line1" (string (unbox<obj array> parsed?lines).[0])

let testPtyWriteTomlFlatFields () =
    let info: PtyWriteInfo =
        { id = "pty_100"
          display = "^C"
          bytes = 1
          status = "written"
          message = "Sent: \"^C\"" }

    let text = renderPtyWrite info
    let parsed = parseToml text
    equal "pty write id" "pty_100" (string parsed?id)
    equal "pty write display" "^C" (string parsed?display)
    equal "pty write bytes" 1 (unbox<int> parsed?bytes)

let testPtyListTomlTableArray () =
    let item: PtySessionItem =
        { id = "pty_1"
          title = "worker"
          command = "node app.js"
          status = "running"
          pid = 8888
          lineCount = 50 }

    let info: PtyListInfo = { count = 1; sessions = [ item ] }
    let text = renderPtyList info
    let parsed = parseToml text
    equal "pty list count" 1 (unbox<int> parsed?count)
    let sess = unbox<obj array> parsed?sessions
    equal "pty list sess len" 1 sess.Length
    equal "pty list sess id" "pty_1" (string sess.[0]?id)

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
    testPtySpawnTomlFlatFields ()
    testPtyKillTomlFlatFields ()
    testPtyReadTomlFlatFields ()
    testPtyWriteTomlFlatFields ()
    testPtyListTomlTableArray ()
