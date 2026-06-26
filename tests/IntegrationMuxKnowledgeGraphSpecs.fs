module Wanxiangshu.Tests.IntegrationMuxKnowledgeGraphSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup

open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.KnowledgeGraph.Prompts
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Shell.KnowledgeGraphFiles
open Wanxiangshu.Shell.Dyn


let muxExecutorRwTriggersMaintenanceSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-executor-maintenance-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [ knowledgeGraphEntry "0a3f" ["积压问题"] "Daily candidate" ]
    let deps = minimalMuxDeps ()
    deps?("directory") <- workspaceDir
    let reg = createRegistration deps
    let executor = muxToolByName reg "executor"
    if isNullish executor then
        check "mux registration exposes executor tool" false
    else
        let ctx = muxToolConfig workspaceDir "mux-executor-maintenance"
        let args = createObj [ "language", box "shell"; "program", box "printf mux-maintenance"; "timeout_type", box "short"; "mode", box "rw" ]
        let! result = ((get executor "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "mux rw executor returns output" (result.Contains "mux-maintenance")
        let after = get reg "tool.execute.after"
        let afterInput =
            createObj
                [ "tool", box "executor"
                  "sessionID", box "mux-executor-maintenance"
                  "callID", box "mux-exec-after"
                  "args", box args
                  "directory", box workspaceDir ]
        let afterOutput = createObj [ "output", box result ]
        do! after $ (afterInput, afterOutput) |> unbox<JS.Promise<unit>>
        do! waitForBackgroundJobsForTesting reg
        let launches = takeBookkeeperLaunchesForTesting reg
        check "mux rw executor triggers maintenance" (
            launches |> Array.exists (fun launch ->
                let title = (str launch "title").ToLowerInvariant()
                let prompt = (str launch "prompt").ToLowerInvariant()
                title.Contains "daily" || prompt.Contains "daily" || title.Contains "rewrite" || prompt.Contains "rewrite"))
    do! rmAsync workspaceDir
}
