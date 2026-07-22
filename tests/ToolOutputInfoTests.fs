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
    equal "render body only" "output = \"hello\"\n" (render (plainText "hello"))

let testRenderHintOnly () =
    let r = render { empty with hint = Some "test" }
    check "render hint contains hint text" (r.Contains "test")
    check "render hint uses flat key" (r.Contains "hint =")

let testRenderBodyAndInfo () =
    let r = render { empty with content = Plain "b"; hint = Some "x" }
    check "body+info contains output" (r.Contains "output = \"b\"")
    check "body+info contains hint" (r.Contains "hint = \"x\"")

let testNoChangeEnvelope () =
    let r = noChangeEnvelope ()
    check "noChangeEnvelope has status" (r.Contains noChangeStatus)
    check "noChangeEnvelope uses flat status" (r.Contains "status =")

let testAddSyntax () =
    let r = addSyntax (plainText "code block") "fsharp" |> render
    check "addSyntax has syntax" (r.Contains "fsharp")
    check "addSyntax has output" (r.Contains "code block")
    equal "addSyntax empty preserves" (plainText "raw") (addSyntax (plainText "raw") "")

let testWithIterator () =
    let r = withIterator (plainText "body") "my-iter" |> render
    check "withIterator has iterator" (r.Contains "my-iter")
    equal "withIterator empty returns msg" (plainText "body") (withIterator (plainText "body") "")

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
    let msg = plainText "some body"
    equal "plainText content" (Plain "some body") msg.content

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
          timeoutSeconds = "none" }

    let text = renderPtySpawn info
    let parsed = parseToml text
    equal "pty spawn id" "pty_100" (string parsed?id)
    equal "pty spawn status" "running" (string parsed?status)
    equal "pty spawn command" "npm run dev" (string parsed?command)
    equal "pty spawn pid" 1234 (unbox<int> parsed?pid)

let testPtyKillTomlFlatFields () =
    let info: PtyKillInfo =
        { id = "pty_100"
          action = "killed"
          cleanup = true
          title = "dev-server"
          command = "npm run dev"
          status = "stopped"
          finalLineCount = 42
          note = "session removed" }

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
          status = "written" }

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

let testFuzzyFindStructuredMatches () =
    let msg =
        { empty with
            content =
                FuzzyFind
                    { pattern = Some "Tool"
                      totalMatched = Some 1
                      totalFiles = Some 10
                      matches =
                          [ { path = "src/A.fs"
                              pattern = Some "Tool"
                              annotation = None } ] } }

    let text = render msg
    check "fuzzy find has matches table" (text.Contains "[[matches]]" || text.Contains "matches")
    check "fuzzy find has path" (text.Contains "src/A.fs")
    check "fuzzy find has total_matched" (text.Contains "total_matched")
    check "fuzzy find no body" (not (text.Contains "body ="))
    check "fuzzy find no summary prose" (not (text.Contains "summary ="))

let testFuzzyGrepStructuredMatches () =
    let msg =
        { empty with
            content =
                FuzzyGrep
                    { pattern = Some "foo"
                      totalMatched = Some 1
                      regexFallbackError = None
                      matches =
                          [ { path = "src/B.fs"
                              line = 12
                              content = "let foo = 1"
                              pattern = Some "foo"
                              contextBefore = []
                              contextAfter = []
                              annotation = None } ] } }

    let text = render msg
    check "fuzzy grep has matches" (text.Contains "matches")
    check "fuzzy grep has path" (text.Contains "src/B.fs")
    check "fuzzy grep has line" (text.Contains "12")
    check "fuzzy grep has content" (text.Contains "let foo = 1")
    check "fuzzy grep no body" (not (text.Contains "body ="))
    check "fuzzy grep no summary prose" (not (text.Contains "summary ="))

let testExecutorStructuredFields () =
    let msg =
        { empty with
            content =
                Executor
                    { stdout = "ok"
                      stderr = None
                      exitCode = Some 0
                      signal = None
                      status = "completed"
                      truncated = false
                      summary = None } }

    let text = render msg
    check "executor has stdout" (text.Contains "stdout")
    check "executor has status" (text.Contains "completed")
    check "executor no body" (not (text.Contains "body ="))

let testWriteResultStructuredFields () =
    let msg =
        { empty with
            content =
                WriteResult
                    { path = "a.fs"
                      success = true
                      syntaxErrors = [] } }

    let text = render msg
    check "write has path" (text.Contains "a.fs")
    check "write has success" (text.Contains "success = true")
    check "write no body" (not (text.Contains "body ="))

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
    testFuzzyFindStructuredMatches ()
    testFuzzyGrepStructuredMatches ()
    testExecutorStructuredFields ()
    testWriteResultStructuredFields ()
