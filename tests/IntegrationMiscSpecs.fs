module Wanxiangshu.Tests.IntegrationMiscSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Runtime.Dyn

let writeToolSpec (reg: obj) =
    promise {
        let tools = unbox<obj[]> (get reg "tools")
        let writeDef = tools |> Array.find (fun t -> str t "name" = "write")

        let! missingPath =
            (get writeDef "execute")
            $ (createObj [ "cwd", box "/tmp"; "workspaceId", box "write-test-ws" ], createObj [ "content", box "x" ])
            |> unbox<JS.Promise<string>>

        check "write missing file_path error" (missingPath.Contains "file_path")
        let! tmpDir = mkdtempAsync "write-test-"

        let! writeResult =
            (get writeDef "execute")
            $ (createObj [ "cwd", box tmpDir; "workspaceId", box "write-test-ws" ],
               createObj [ "file_path", box "empty.txt"; "content", box "" ])
            |> unbox<JS.Promise<string>>

        check "write empty string succeeds" (writeResult.Contains "Successfully wrote")
        do! rmAsync tmpDir
    }

let loopCommandSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-loop-command-"
        let deps = createObj [ "directory", box workspaceDir ]
        let reg = Wanxiangshu.Hosts.Mux.Plugin.createRegistration deps
        let cmds = unbox<obj[]> (get reg "slashCommands")
        let loopCmd = cmds |> Array.find (fun c -> str c "key" = "loop")
        let sid = "mux-loop-spec-" + System.Guid.NewGuid().ToString("N")
        let! _ = (get loopCmd "execute") $ (sid, "") |> unbox<JS.Promise<string>>
        let! result = (get loopCmd "execute") $ (sid, "some task") |> unbox<JS.Promise<string>>
        check "loop resolve includes task" (result.Contains "some task")
        do! rmAsync workspaceDir
    }
