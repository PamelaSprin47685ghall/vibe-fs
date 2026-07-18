module Wanxiangshu.Runtime.Wanxiangzhen.SquadTaskLifecycleStart

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Kernel.Wanxiangzhen.SquadPrompts
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime

[<Import("dirname", "node:path")>]
let private pathDirname (p: string) : string = jsNative

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let rec resolveBranchName (rt: CoordinatorRuntime) (taskId: string) (attempts: int) : string =
    let candidate = taskId

    if attempts <= 0 then
        candidate
    elif rt.Deps.ShowRefExists rt.ProjectRoot candidate then
        let suffix = (generateTaskId ()).Substring 6
        resolveBranchName rt (taskId + "-" + suffix) (attempts - 1)
    else
        candidate

let startTask (rt: CoordinatorRuntime) (taskId: string) : JS.Promise<unit> =
    promise {
        match findTask taskId rt.Dag with
        | None -> return ()
        | Some task ->
            if not (rt.Deps.HasCommits rt.ProjectRoot) then
                let errorMessage =
                    "Repository has no commits. Run 'git commit --allow-empty -m \"Initial commit\"' before using /squad."

                let! _ = commitEvent rt (TaskError(rt.Dag.SessionId, taskId, errorMessage))
                return ()

            let parent = pathDirname rt.ProjectRoot
            let branchName = resolveBranchName rt taskId 5
            let wtPath = pathJoin parent ("worktree-" + branchName)

            match rt.Deps.TryWorktreeAdd rt.ProjectRoot branchName wtPath rt.MasterBranch with
            | Error e ->
                let! _ = commitEvent rt (TaskError(rt.Dag.SessionId, taskId, e))
                return ()
            | Ok _ ->
                let! cr = commitEvent rt (TaskStarted(rt.Dag.SessionId, taskId, wtPath, branchName))

                match cr with
                | Error _ -> return ()
                | Ok() ->
                    rt.Deps.CreateSymlinks wtPath rt.ProjectRoot rt.Config.SharedDirs
                    let prompt = buildSlavePrompt taskId task.Title task.Description rt.MasterBranch
                    let slaveEnv = createObj []
                    assignInto slaveEnv (get nodeProcess "env") |> ignore
                    setKey slaveEnv "SQUAD_COORDINATOR_URL" (box rt.CoordinatorUrl)
                    setKey slaveEnv "SQUAD_TASK_ID" (box taskId)
                    setKey slaveEnv "SQUAD_WORKTREE_PATH" (box wtPath)
                    setKey slaveEnv "SQUAD_MASTER_BRANCH" (box rt.MasterBranch)
                    setKey slaveEnv "SQUAD_TOKEN" (box rt.Token)
                    rt.Deps.SpawnSlave rt.Config.Terminal wtPath slaveEnv prompt
                    let now = rt.Deps.Now()

                    rt.Dag <-
                        rt.Dag
                        |> updateTask taskId (fun (t: SquadTask) ->
                            match tryWithStatus t Running now with
                            | Ok t2 ->
                                { t2 with
                                    WorktreePath = Some wtPath
                                    BranchName = Some branchName }
                            | Error _ -> t)
    }
