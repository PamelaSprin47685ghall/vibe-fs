module Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorDepsFactory

open Fable.Core
open Wanxiangshu.Kernel.Wanxiangzhen.SquadConfig
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Runtime.Wanxiangzhen.ConfigReader
open Wanxiangshu.Runtime.Wanxiangzhen.SquadEventLogRuntime
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.Wanxiangzhen.GitShell
open Wanxiangshu.Runtime.Wanxiangzhen.SlaveSpawn
open Wanxiangshu.Runtime.Wanxiangzhen.PidMonitor
open Wanxiangshu.Runtime.Wanxiangzhen.SymlinkShell
open Wanxiangshu.Runtime.Wanxiangzhen.SessionIo
open Wanxiangshu.Runtime.SquadEventStore

let resolveMasterBranch (directory: string) (config: SquadConfig) (deps: CoordinatorDeps) : string * string option =
    match config.MasterBranch with
    | Some b -> b, None
    | None ->
        try
            if not (deps.HasCommits directory) then
                "master",
                Some
                    "Repository has no commits. Run 'git commit --allow-empty -m \"Initial commit\"' before using /squad."
            elif deps.IsDetached directory then
                "master", Some "Detached HEAD detected. Please configure squad.masterBranch in AGENTS.md frontmatter."
            else
                deps.RevParseBranch directory, None
        with ex ->
            "master", Some(string ex.Message)

let realCoordinatorDeps (workspaceRoot: string) : CoordinatorDeps =
    let store = getStore workspaceRoot

    let rec deps =
        { PromptSession = promptSession
          GetLatestSquadSessionId = fun () -> store.GetLatestSquadSessionId()
          GetSquadDag = fun sessionId -> store.GetSquadDag sessionId
          GetSquadSessions = fun () -> store.GetSquadSessions()
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
          WaitForPidDeath = fun pid r -> waitForPidDeath deps pid r
          StartPolling = startPolling
          StopPolling = stopPolling
          Now = fun () -> System.DateTime.UtcNow.ToString("o")
          RandomGen = fun () -> JS.Math.random () }

    deps
