module Wanxiangshu.Tests.IntegrationMuxToolSpecsRead

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Runtime.Dyn

let muxExecutorRoCatPrependsWarningSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-executor-ro-warning-"
        let reg = sharedMuxRegistration ()
        let executor = muxToolByName reg "executor"

        if isNullish executor then
            check "mux registration exposes executor tool for ro warning" false
        else
            let ctx =
                createObj
                    [ "directory", box workspaceDir
                      "workspaceId", box "mux-executor-ro-warning"
                      "sessionID", box "mux-executor-ro-warning" ]

            let args =
                createObj
                    [ "language", box "shell"
                      "command", box "cat /etc/passwd"
                      "timeout_type", box "short"
                      "what_to_summarize", box "summarize exit codes"
                      "max_bytes", box 8192
                      "warn",
                      box
                          "it-is-not-possible-to-do-it-using-other-tools-and-only-run-tests-when-static-analysis-cannot-handle-it" ]

            let! result = ((get executor "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
            check "mux executor ro cat includes misuse hint in envelope" (hasExactHint result hintExecutorMisuse)

        do! rmAsync workspaceDir
    }


let muxExecutorModeSchemaSpec () =
    promise {
        let reg = sharedMuxRegistration ()
        let modeSchema = muxExecutorModeSchema reg
        check "mux executor mode schema removed" (isNullish modeSchema)
    }

let muxReadToolReturnsContentSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-read-content-"
        let filePath = pathModule?join (workspaceDir, "sample.txt")
        let fileContent = "line1\nline2\nline3\nline4\nline5"
        do! writeFileAsync filePath fileContent
        let reg = sharedMuxRegistration ()
        let wrappers = unbox<obj[]> (get reg "wrappers")

        let fileReadWrapper =
            wrappers |> Array.tryFind (fun w -> str w "targetTool" = "file_read")

        if isNullish fileReadWrapper then
            check "mux registration exposes file_read wrapper" false
        else
            let fakeHostRead =
                box
                    {| execute =
                        System.Func<obj, obj, JS.Promise<obj>>(fun args _config ->
                            promise {
                                let path = str args "path"

                                if path = filePath then
                                    return
                                        box
                                            {| success = true
                                               content = fileContent |}
                                else
                                    return
                                        box
                                            {| success = false
                                               error = "file not found" |}
                            }) |}

            (get fileReadWrapper "wrapper") $ (fakeHostRead, createObj []) |> ignore
            let readTool = muxToolByName reg "read"

            if isNullish readTool then
                check "mux registration exposes read tool" false
            else
                let ctx =
                    createObj
                        [ "directory", box workspaceDir
                          "workspaceId", box "mux-read-content-session"
                          "sessionID", box "mux-read-content-session" ]

                let args = createObj [ "path", box filePath ]
                let! result = ((get readTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
                check "mux read returns non-nullish content" (not (isNullish result))
                check "mux read returns expected line text" (result.Contains "line1" && result.Contains "line5")
                check "mux read does not stringify undefined" (result <> "undefined")
                check "mux read does not stringify object" (not (result.Contains "[object Object]"))

        do! rmAsync workspaceDir
    }

let muxReadToolListsDirectoriesSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-read-directory-"
        do! writeFileAsync (pathModule?join (workspaceDir, "note.txt")) "alpha\nbeta"
        let reg = sharedMuxRegistration ()
        let wrappers = unbox<obj[]> (get reg "wrappers")

        let fileReadWrapper =
            wrappers |> Array.tryFind (fun w -> str w "targetTool" = "file_read")

        if isNullish fileReadWrapper then
            check "mux registration exposes file_read wrapper for directory read" false
        else
            let fakeHostRead =
                box
                    {| execute =
                        System.Func<obj, obj, JS.Promise<obj>>(fun args _config ->
                            promise {
                                let path = str args "path"

                                if path = workspaceDir then
                                    return
                                        box
                                            {| success = false
                                               error = $"Path is a directory, not a file: {workspaceDir}" |}
                                else
                                    return
                                        box
                                            {| success = true
                                               content = "1\talpha\n2\tbeta" |}
                            }) |}

            (get fileReadWrapper "wrapper") $ (fakeHostRead, createObj []) |> ignore
            let readTool = muxToolByName reg "read"

            if isNullish readTool then
                check "mux registration exposes read tool for directory read" false
            else
                let ctx =
                    createObj
                        [ "directory", box workspaceDir
                          "workspaceId", box "mux-read-directory-session"
                          "sessionID", box "mux-read-directory-session" ]

                let args = createObj [ "path", box workspaceDir ]
                let! result = ((get readTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
                check "mux read returns directory listing" (result.Contains "note.txt" && result.Contains "total 1")

                check
                    "mux read directory does not return file-only error"
                    (not (result.Contains "Path is a directory, not a file"))

        do! rmAsync workspaceDir
    }
