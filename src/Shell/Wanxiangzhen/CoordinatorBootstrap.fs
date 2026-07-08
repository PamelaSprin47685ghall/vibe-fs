module Wanxiangshu.Shell.Wanxiangzhen.CoordinatorBootstrap

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Kernel.Wanxiangzhen.SquadConfig
open Wanxiangshu.Kernel.Wanxiangzhen.FfDecision
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.Wanxiangzhen.GitShell
open Wanxiangshu.Shell.PromiseQueue
open Wanxiangshu.Shell.Wanxiangzhen.HttpServer
open Wanxiangshu.Shell.Wanxiangzhen.HttpCodec
open Wanxiangshu.Shell.Wanxiangzhen.SquadEventDisplayCodec
open Wanxiangshu.Shell.Wanxiangzhen.ConfigReader
open Wanxiangshu.Shell.Wanxiangzhen.SessionIo
open Wanxiangshu.Shell.Wanxiangzhen.SquadEventLogRuntime
open Wanxiangshu.Shell.Wanxiangzhen.SlaveSpawn
open Wanxiangshu.Shell.Wanxiangzhen.PidMonitor
open Wanxiangshu.Shell.Wanxiangzhen.SymlinkShell
open Wanxiangshu.Kernel.Yaml
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRoutes

let createWithDeps
    (client: obj)
    (directory: string)
    (config: SquadConfig)
    (masterBranch: string)
    (gitError: string option)
    (deps: CoordinatorDeps)
    : JS.Promise<CoordinatorRuntime> =
    promise {
        let token =
            System.String([| for _ in 0..31 -> "0123456789abcdef".[int (JS.Math.random () * 16.0)] |])

        let rtRef = ref None

        let! server =
            startServer token (fun m p b ->
                promise {
                    match rtRef.Value with
                    | None ->
                        return
                            { StatusCode = 503
                              Body = box {| result = "not_ready" |} }
                    | Some r -> return! routeHandler r m p b
                })

        let runtime =
            { Dag = empty "" ""
              Sessions = Map.empty
              Config = config
              MasterBranch = masterBranch
              ProjectRoot = directory
              MasterSessionId = ""
              Client = client
              Token = token
              CoordinatorUrl = server.Url
              GitQueue = SerialQueue()
              InjectQueue = SerialQueue()
              Server = server
              Scheduling = false
              PidPollHandle = None
              GitError = gitError
              InjectError = None
              Deps = deps }

        rtRef.Value <- Some runtime
        startPidPolling runtime
        return runtime
    }

let create (client: obj) (directory: string) : JS.Promise<CoordinatorRuntime> =
    promise {
        let config = readConfig directory

        let mb, gitError =
            match config.MasterBranch with
            | Some b -> b, None
            | None ->
                try
                    if not (hasCommits directory) then
                        "master",
                        Some
                            "Repository has no commits. Run 'git commit --allow-empty -m \"Initial commit\"' before using /squad."
                    elif isDetached directory then
                        "master",
                        Some "Detached HEAD detected. Please configure squad.masterBranch in AGENTS.md frontmatter."
                    else
                        revParseBranch directory, None
                with ex ->
                    "master", Some(string ex.Message)

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
        return! createWithDeps client directory config mb gitError deps
    }
