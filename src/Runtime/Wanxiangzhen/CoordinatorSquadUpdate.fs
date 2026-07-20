module Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorSquadUpdate

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Kernel.Wanxiangzhen.SquadUpdateIdAssign
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorOps
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorLifecycle
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorSquadUpdateValidation
let validateTasksArrayShape = CoordinatorSquadUpdateValidation.validateTasksArrayShape
let validateTaskFields = CoordinatorSquadUpdateValidation.validateTaskFields

let private computeSquadUpdate
    (rt: CoordinatorRuntime)
    (existingTaskIds: Set<string>)
    (assigned: TaskItem list)
    : SquadUpdateOutcome option =
    let newIds = assigned |> List.map (fun item -> item.taskId) |> Set.ofList
    let allIds = Set.union existingTaskIds newIds

    let dangling =
        assigned
        |> List.collect (fun item ->
            item.dependsOn
            |> List.filter (fun d -> not (Set.contains d allIds))
            |> List.map (fun d -> item.taskId, d))

    if dangling <> [] then
        DependencyErrors dangling |> Some
    else
        let depsList = assigned |> List.map (fun item -> item.taskId, item.dependsOn)

        let existingDeps =
            rt.Dag.Tasks |> Map.toList |> List.map (fun (id, t) -> id, t.DependsOn)

        match detectCycle (existingDeps @ depsList) with
        | Some cycle -> CycleDetected cycle |> Some
        | None -> None

let private commitTasksEvent
    (rt: CoordinatorRuntime)
    (assigned: TaskItem list)
    : JS.Promise<Result<unit, SquadUpdateOutcome>> =
    promise {
        if assigned = [] then
            return Ok()
        else
            let! appendOk = commitEvent rt (TasksCreated(rt.Dag.SessionId, assigned))

            match appendOk with
            | Error err ->
                rt.InjectError <- Some(sprintf "TasksCreated append failed: %s" err)
                return Error(InvalidInput "event log append failed.")
            | Ok() -> return Ok()
    }

let private applySquadUpdate
    (rt: CoordinatorRuntime)
    (assigned: TaskItem list)
    (hasCancelled: bool)
    : JS.Promise<string> =
    promise {
        if assigned <> [] then
            let now = rt.Deps.Now()

            let updatedDag =
                assigned
                |> List.fold
                    (fun dag item ->
                        let t = create item.taskId item.title item.description item.dependsOn now
                        addTask t dag)
                    rt.Dag

            rt.Dag <- updatedDag

        if hasCancelled then
            do! handleSquadKillCore rt None

        let resultText =
            if hasCancelled && assigned = [] then
                sprintf "Squad session %s cancelled." rt.Dag.SessionId
            else
                sprintf "%d task(s) created, scheduler notified." (List.length assigned)

        schedulerTick rt |> Promise.start |> ignore
        return resultText
    }

let private commitAssigned
    (rt: CoordinatorRuntime)
    (existingTaskIds: Set<string>)
    (assigned: TaskItem list)
    (hasCancelled: bool)
    : JS.Promise<Result<string, SquadUpdateOutcome>> =
    promise {
        match computeSquadUpdate rt existingTaskIds assigned with
        | Some o -> return Error o
        | None ->
            let! commitOk = commitTasksEvent rt assigned

            match commitOk with
            | Error o -> return Error o
            | Ok() ->
                let! text = applySquadUpdate rt assigned hasCancelled
                return Ok text
    }

let private handleSquadUpdateCore (rt: CoordinatorRuntime) (args: obj) : JS.Promise<string> =
    promise {
        match parseEventsArgs args with
        | None -> return formatSquadUpdateOutcome (InvalidInput "events must be a non-empty array.")
        | Some events ->
            if rt.Dag.SessionId = "" then
                return formatSquadUpdateOutcome (InvalidInput "no active squad session. Start with /squad first.")
            else
                match validateTasksArrayShape events with
                | Some o -> return formatSquadUpdateOutcome o
                | None ->
                    match validateTaskFields events with
                    | Some o -> return formatSquadUpdateOutcome o
                    | None ->
                        let existingTaskIds = rt.Dag.Tasks |> Map.toList |> List.map fst |> Set.ofList

                        match aggregateAndAssignTasks rt existingTaskIds events with
                        | Error o -> return formatSquadUpdateOutcome o
                        | Ok(assigned, hasCancelled) ->
                            match! commitAssigned rt existingTaskIds assigned hasCancelled with
                            | Error o -> return formatSquadUpdateOutcome o
                            | Ok text -> return text
    }

let handleSquadUpdate (rt: CoordinatorRuntime) (args: obj) : JS.Promise<string> =
    rt.DagQueue.Enqueue(fun () -> handleSquadUpdateCore rt args)
