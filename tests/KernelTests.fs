module Wanxiangshu.Tests.KernelTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.Dyn


let headTail' () =
    let r = headTail "hello" 2 2
    check "headTail" (r = "he...lo")

/// Characterization net for Executor.strip: locks the pipe-stripping lexer's
/// 12 behavioral boundaries (EOF / terminator / quote / comment / chained-pipe
/// handling) before any refactor of parsePipe+scan.
let stripLexer' () =
    // 1. plain no pipe
    let r1 = strip "echo hi"
    check "strip plain script unchanged" (r1.script = "echo hi")
    check "strip plain no stripped" (List.isEmpty r1.stripped)

    // 2. head at EOF
    let r2 = strip "printf hi | head -n 1"
    check "strip head eof script" (r2.script = "printf hi")
    check "strip head eof one stripped" (r2.stripped.Length = 1)
    check "strip head eof name" (r2.stripped.[0].name = "head")
    check "strip head eof count" (r2.stripped.[0].count = 1)
    check "strip head eof pipe" (r2.stripped.[0].pipe = "| head -n 1")

    // 3. tail at EOF
    let r3 = strip "data | tail -n 2"
    check "strip tail eof script" (r3.script = "data")
    check "strip tail eof name" (r3.stripped.[0].name = "tail")
    check "strip tail eof count" (r3.stripped.[0].count = 2)

    // 4. bare dash no -n
    let r4 = strip "data | head -1"
    check "strip bare dash script" (r4.script = "data")
    check "strip bare dash name" (r4.stripped.[0].name = "head")
    check "strip bare dash count" (r4.stripped.[0].count = 1)

    // 5. head followed by \n
    let r5 = strip "printf hi | head -n 1\necho done"
    check "strip head newline script" (r5.script = "printf hi\necho done")
    check "strip head newline one stripped" (r5.stripped.Length = 1)

    // 6. head followed by ;
    let r6 = strip "printf hi | head -n 1; echo"
    check "strip head semicolon script" (r6.script = "printf hi; echo")
    check "strip head semicolon one stripped" (r6.stripped.Length = 1)

    // 7. pipe in single quotes
    let r7 = strip "echo 'foo | head -n 1'"
    check "strip single quote unchanged" (r7.script = "echo 'foo | head -n 1'")
    check "strip single quote empty" (List.isEmpty r7.stripped)

    // 8. pipe in double quotes
    let r8 = strip "echo \"foo | head -n 1\""
    check "strip double quote unchanged" (r8.script = "echo \"foo | head -n 1\"")
    check "strip double quote empty" (List.isEmpty r8.stripped)

    // 9. non-head/tail pipe
    let r9 = strip "a | grep foo"
    check "strip grep unchanged" (r9.script = "a | grep foo")
    check "strip grep empty" (List.isEmpty r9.stripped)

    // 10. two chained head/tail pipes: strip's outer multi-pass loop strips tail first (reducing to "a | head -n 1"), then head (now at EOF)
    let r10 = strip "a | head -n 1 | tail -n 2"
    check "strip chained two stripped" (r10.stripped.Length = 2)
    check "strip chained script fully reduced" (r10.script = "a")
    check "strip chained head name" (r10.stripped.[0].name = "head")
    check "strip chained head count" (r10.stripped.[0].count = 1)
    check "strip chained tail name" (r10.stripped.[1].name = "tail")
    check "strip chained tail count" (r10.stripped.[1].count = 2)

    // 11. pipe in hash comment
    let r11 = strip "echo hi\n# noisy | head -n 1"
    check "strip hash comment unchanged" (r11.script = "echo hi\n# noisy | head -n 1")
    check "strip hash comment empty" (List.isEmpty r11.stripped)

    // 12. pipe after double-quoted segment
    let r12 = strip "echo \"x\" | head -n 1"
    check "strip pipe after quote script" (r12.script = "echo \"x\"")
    check "strip pipe after quote one stripped" (r12.stripped.Length = 1)
    check "strip pipe after quote head name" (r12.stripped.[0].name = "head")
    check "strip pipe after quote head count" (r12.stripped.[0].count = 1)

let jsBoundary' () =
    check
        "abort message classified"
        (translateJsError (createObj [ "message", box "Aborted" ]) = Wanxiangshu.Kernel.Domain.ClientCancellation
                                                                         "abort-text")

    let nestedCause: obj =
        createObj
            [ "name", box "TypeError"
              "message", box "terminated"
              "cause", box (createObj [ "name", box "AbortError"; "message", box "aborted" ]) ]

    check
        "abort nested via cause"
        (translateJsError nestedCause = Wanxiangshu.Kernel.Domain.ClientCancellation "AbortError")

    let nestedError: obj =
        createObj
            [ "name", box "TypeError"
              "error", box (createObj [ "name", box "AbortError" ]) ]

    check
        "abort nested via error"
        (translateJsError nestedError = Wanxiangshu.Kernel.Domain.ClientCancellation "AbortError")

    let nonAbort: obj =
        createObj [ "name", box "RangeError"; "message", box "out of range" ]

    check
        "non-abort stays unknown"
        (translateJsError nonAbort = Wanxiangshu.Kernel.Domain.UnknownJsError "out of range")

    let msgs: Message<obj> list =
        [ { info =
              { id = ""
                sessionID = ""
                role = Assistant
                agent = ""
                isError = false
                toolName = ""
                details = null
                time = null }
            parts = [ TextPart "hello" ]
            source = Native
            raw = null } ]

    let text = readAssistantText msgs 0 "\n\n"
    check "assistant text read" (text = Some "hello")

/// `Dyn.deleteKey` is the primitive HookExecute leans on to clear Mimocode
/// task extras off the original args reference.
let dynDeleteKey () =
    let target = createObj [ "keep", box "yes"; "drop", box "bye" ]
    deleteKey target "drop"
    check "deleteKey removes key" (isNullish (get target "drop"))
    check "deleteKey preserves siblings" (str target "keep" = "yes")
    deleteKey null "missing"
    check "deleteKey null is a no-op" true
