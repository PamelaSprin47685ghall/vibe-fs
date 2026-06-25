module VibeFs.Tests.IntegrationMiscSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Mux.Plugin
open VibeFs.Shell.Dyn

let writeToolSpec (reg: obj) = promise {
    let tools = unbox<obj[]> (get reg "tools")
    let writeDef = tools |> Array.find (fun t -> str t "name" = "write")
    let! missingPath = (get writeDef "execute") $ (createObj [ "cwd", box "/tmp"; "workspaceId", box "write-test-ws" ], createObj [ "content", box "x" ]) |> unbox<JS.Promise<string>>
    check "write missing file_path error" (missingPath.Contains "file_path")
    let! tmpDir = mkdtempAsync "write-test-"
    let! writeResult = (get writeDef "execute") $ (createObj [ "cwd", box tmpDir; "workspaceId", box "write-test-ws" ], createObj [ "file_path", box "empty.txt"; "content", box "" ]) |> unbox<JS.Promise<string>>
    check "write empty string succeeds" (writeResult.Contains "Successfully wrote")
    do! rmAsync tmpDir
}

let loopCommandSpec (reg: obj) = promise {
    let cmds = unbox<obj[]> (get reg "slashCommands")
    let loopCmd = cmds |> Array.find (fun c -> str c "key" = "loop")
    let! result = (get loopCmd "execute") $ ("test-ws", "some task") |> unbox<JS.Promise<string>>
    check "loop resolve includes task" (result.Contains "some task")
}