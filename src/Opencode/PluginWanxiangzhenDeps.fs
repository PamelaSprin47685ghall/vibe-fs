module Wanxiangshu.Opencode.PluginWanxiangzhenDeps

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorBootstrap
open Wanxiangshu.Shell.Wanxiangzhen.ConfigReader
open Wanxiangshu.Shell.Wanxiangzhen.SquadEventLogRuntime
open Wanxiangshu.Shell.Wanxiangzhen.GitShell
open Wanxiangshu.Shell.Wanxiangzhen.SlaveSpawn
open Wanxiangshu.Shell.Wanxiangzhen.PidMonitor
open Wanxiangshu.Shell.Wanxiangzhen.SymlinkShell
open Wanxiangshu.Shell.Wanxiangzhen.HttpServer
open Wanxiangshu.Shell.Wanxiangzhen.SessionIo

let realCoordinatorDeps () : CoordinatorDeps =
    let depsRef =
        ref
            { PromptSession = fun _ _ _ -> Promise.lift ()
              ReadAllSquadEvents = readAllSquadEvents
              AppendSquadEvent = appendSquadEvent
              TryWorktreeAdd = fun _ _ _ _ -> Ok ""
              TryWorktreeRemoveForce = fun _ _ -> Ok ""
              TryBranchDeleteForce = fun _ _ -> Ok ""
              ShowRefExists = fun _ _ -> false
              RevParseHead = fun _ -> ""
              RevParseRef = fun _ _ -> ""
              RevParseBranch = fun _ -> ""
              IsDetached = fun _ -> false
              StatusIsClean = fun _ -> true
              MergeBaseIsAncestor = fun _ _ _ -> false
              MergeFfOnly = fun _ _ -> ""
              HasCommits = fun _ -> false
              CreateSymlinks = fun _ _ _ -> ()
              SpawnSlave = fun _ _ _ _ -> ()
              IsPidAlive = fun _ -> false
              KillPid = fun _ _ -> ()
              WaitForPidDeath = fun _ _ -> Promise.lift ()
              StartPolling = fun _ _ -> box null
              StopPolling = fun _ -> ()
              Now = fun () -> System.DateTime.UtcNow.ToString("o") }

    let deps =
        { PromptSession = promptSession
          ReadAllSquadEvents = readAllSquadEvents
          AppendSquadEvent = appendSquadEvent
          TryWorktreeAdd = tryWorktreeAdd
          TryWorktreeRemoveForce = tryWorktreeRemoveForce
          TryBranchDeleteForce = tryBranchDeleteForce
          ShowRefExists = showRefExists
          RevParseHead = revParseHead
          RevParseRef = revParseRef
          RevParseBranch = revParseBranch
          IsDetached = isDetached
          StatusIsClean = statusIsClean
          MergeBaseIsAncestor = mergeBaseIsAncestor
          MergeFfOnly = mergeFfOnly
          HasCommits = hasCommits
          CreateSymlinks = createSymlinks
          SpawnSlave = spawnSlave
          IsPidAlive = isPidAlive
          KillPid = killPid
          WaitForPidDeath = fun pid r -> waitForPidDeath depsRef.Value pid r
          StartPolling = startPolling
          StopPolling = stopPolling
          Now = fun () -> System.DateTime.UtcNow.ToString("o") }

    depsRef.Value <- deps
    deps

let pluginWithDeps
    (ctx: obj)
    (deps: CoordinatorDeps)
    : JS.Promise<
          {| hooks: obj
             runtime: CoordinatorRuntime |}
       >
    =
    promise {
        let client = get ctx "client"
        let directory = str ctx "directory"
        let config = readConfig directory

        let mb, gitError =
            match config.MasterBranch with
            | Some b -> b, None
            | None ->
                try
                    if not (deps.HasCommits directory) then
                        "master",
                        Some
                            "Repository has no commits. Run 'git commit --allow-empty -m \"Initial commit\"' before using /squad."
                    elif deps.IsDetached directory then
                        "master",
                        Some "Detached HEAD detected. Please configure squad.masterBranch in AGENTS.md frontmatter."
                    else
                        deps.RevParseBranch directory, None
                with ex ->
                    "master", Some(string ex.Message)

        let! rt = createWithDeps client directory config mb gitError deps
        let hooks = PluginWanxiangzhenHooks.assembleCoordinatorHooks rt
        return {| hooks = hooks; runtime = rt |}
    }
