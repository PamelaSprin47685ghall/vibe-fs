module Wanxiangshu.E2e.TestsEdgeCaseScenarios

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.HarnessTypes

let runRest
    (harness: Harness)
    (sessionID: string)
    (chk: string -> bool -> unit)
    (toolRound: Harness -> string -> string -> obj -> string -> JS.Promise<unit>)
    (toolRoundWithCalls: Harness -> string -> string -> obj -> string -> int -> JS.Promise<unit>)
    (textRound: Harness -> string -> string -> JS.Promise<unit>)
    (containsTool: Harness -> string -> bool)
    (bodies: Harness -> string)
    (emptyObj: obj)
    (jsonStringify: obj -> string)
    (textRoundWithCalls: Harness -> string -> string -> int -> JS.Promise<unit>)
    (expected: int)
    (summary: unit -> int)
    : JS.Promise<int> =
    promise {
        // 14. history-echo
        do! textRoundWithCalls harness sessionID "first turn" 1
        do! textRoundWithCalls harness sessionID "second turn" 1
        let! msgs = withTimeout (harness.getMessages sessionID emptyObj)
        chk "e2e.history-echo.ok" (unbox<obj> msgs?ok = true)
        let mb = jsonStringify (unbox<obj> msgs?data)
        chk "e2e.history-echo.non-empty" (mb.Length > 2)
        chk "e2e.history-echo.first" (mb.Contains "first turn")
        chk "e2e.history-echo.second" (mb.Contains "second turn")

        // 15. session-listing
        let! sessionsRes = withTimeout (harness.getSessions emptyObj)
        let sessions = unbox<obj> sessionsRes
        chk "e2e.session-listing" ((jsonStringify (sessions?data)).Contains sessionID)

        // 16. write-overwrite
        do!
            toolRound
                harness
                sessionID
                "write"
                (box
                    {| filePath = "note.txt"
                       content = "one" |})
                "write one"

        chk "e2e.write-overwrite.first-call" (containsTool harness "write")

        do!
            toolRound
                harness
                sessionID
                "write"
                (box
                    {| filePath = "note.txt"
                       content = "two" |})
                "write two"

        chk "e2e.write-overwrite.second-call" (containsTool harness "write")

        // 17. read-side-effect
        do! toolRound harness sessionID "read" (box {| filePath = "README.md" |}) "read README"
        chk "e2e.read-side-effect.tool-called" (containsTool harness "read")
        let b4 = bodies harness
        chk "e2e.read-side-effect.body" (b4.Contains "README")

        // 18. boundary-empty-content
        do!
            toolRound
                harness
                sessionID
                "write"
                (box
                    {| filePath = "empty.txt"
                       content = "" |})
                "write empty file"

        chk "e2e.boundary-empty-content.tool-called" (containsTool harness "write")

        // 19. boundary-special-chars-filename
        do!
            toolRound
                harness
                sessionID
                "write"
                (box
                    {| filePath = "test-file_123.txt"
                       content = "special" |})
                "write special filename"

        chk "e2e.boundary-special-chars-filename.tool-called" (containsTool harness "write")

        // 20. boundary-unicode-content
        do!
            toolRound
                harness
                sessionID
                "write"
                (box
                    {| filePath = "unicode.txt"
                       content = "你好世界 🌍" |})
                "write unicode content"

        chk "e2e.boundary-unicode-content.tool-called" (containsTool harness "write")

        // 21. error-path-read-nonexistent
        do! toolRound harness sessionID "read" (box {| filePath = "nonexistent.txt" |}) "read nonexistent file"
        chk "e2e.error-path-read-nonexistent.tool-called" (containsTool harness "read")
        let errorBodies = bodies harness
        chk "e2e.error-path-read-nonexistent.has-error-indication" (errorBodies.Contains "nonexistent.txt")

        // 22. multi-turn-file-reference
        do!
            toolRound
                harness
                sessionID
                "write"
                (box
                    {| filePath = "context.txt"
                       content = "important context data" |})
                "write context file"

        chk "e2e.multi-turn-file-reference.write-ok" (containsTool harness "write")

        do!
            toolRoundWithCalls
                harness
                sessionID
                "read"
                (box {| filePath = "context.txt" |})
                "read the context file we just wrote"
                1

        let contextBodies = bodies harness
        chk "e2e.multi-turn-file-reference.read-previous-file" (contextBodies.Contains "important context data")

        // 23. session-create-second
        let! session2Res =
            withTimeout (
                harness.createSession
                    (createObj [ "model", createObj [ "id", box "test-model"; "providerID", box "test" ] ])
                    emptyObj
            )

        let session2Data = unbox<obj> session2Res
        chk "e2e.session-create-second.ok" (session2Data?ok = true)

        // 24. todo-with-actual-content
        do!
            toolRound
                harness
                sessionID
                "todowrite"
                (box
                    {| todos =
                        ResizeArray(
                            [ box
                                  {| content = "implement feature X"
                                     status = "in_progress"
                                     priority = "high" |} ]
                        )
                       select_methodology = ResizeArray([ "first_principles" ]) |})
                "write todo with content"

        chk "e2e.todo-with-content.tool-called" (containsTool harness "todowrite")
        let todoBodies = bodies harness
        chk "e2e.todo-with-content.has-task-content" (todoBodies.Contains "implement feature X")

        // 25. executor-javascript
        do!
            toolRound
                harness
                sessionID
                "executor"
                (box
                    {| language = "javascript"
                       command = "console.log('js test')" |})
                "run javascript"

        chk "e2e.executor-javascript.tool-called" (containsTool harness "executor")

        // 26. fuzzy-find-no-results
        do!
            toolRound
                harness
                sessionID
                "fuzzy_find"
                (box {| pattern = [| "xyznonexistent123" |] |})
                "find nonexistent pattern"

        chk "e2e.fuzzy-find-no-results.tool-called" (containsTool harness "fuzzy_find")

        // 27. write-large-content
        let largeContent = String.replicate 1000 "x"

        do!
            toolRound
                harness
                sessionID
                "write"
                (box
                    {| filePath = "large.txt"
                       content = largeContent |})
                "write large file"

        chk "e2e.write-large-content.tool-called" (containsTool harness "write")

        // 28. multiple-files-write-sequential
        do!
            toolRound
                harness
                sessionID
                "write"
                (box
                    {| filePath = "file1.txt"
                       content = "one" |})
                "write file1"

        do!
            toolRound
                harness
                sessionID
                "write"
                (box
                    {| filePath = "file2.txt"
                       content = "two" |})
                "write file2"

        do!
            toolRound
                harness
                sessionID
                "write"
                (box
                    {| filePath = "file3.txt"
                       content = "three" |})
                "write file3"

        // 29. fuzzy-grep-array-pattern
        do!
            toolRound
                harness
                sessionID
                "fuzzy_grep"
                (box {| pattern = [| "fuzzyGrepMulti"; "fuzzyGrepSingle" |] |})
                "grep for multiple patterns"

        chk "e2e.fuzzy-grep-array-pattern.tool-called" (containsTool harness "fuzzy_grep")
        let bGrep = bodies harness

        chk
            "e2e.fuzzy-grep-array-pattern.output-has-blocks"
            (bGrep.Contains "## pattern: \\\"fuzzyGrepMulti\\\""
             || bGrep.Contains "## pattern: \"fuzzyGrepMulti\"")

        do!
            Wanxiangshu.E2e.TestsServePlugin.runServePluginChecks
                harness
                sessionID
                chk
                toolRound
                toolRoundWithCalls
                textRound
                containsTool
                bodies
                emptyObj
                "todowrite"

        return summary ()
    }
