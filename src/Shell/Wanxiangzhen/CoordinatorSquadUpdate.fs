module Wanxiangshu.Shell.Wanxiangzhen.CoordinatorSquadUpdate

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Kernel.Wanxiangzhen.SquadUpdateIdAssign
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorOps
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorLifecycle

type RawTask = string option * string * string * string list

let private parseEventsArgs (args: obj) : obj list option =
    let eventsRaw = get args "events"

    let arrOpt =
        if isArray eventsRaw then
            Some(eventsRaw :?> obj array)
        elif eventsRaw :? string then
            let s = string eventsRaw

            if s = "" then
                None
            else
                try
                    let parsed = JS.JSON.parse s
                    if isArray parsed then Some(parsed :?> obj array) else None
                with _ ->
                    None
        else
            None

    match arrOpt with
    | None -> None
    | Some arr when arr.Length = 0 -> None
    | Some arr -> Some(Array.toList arr)

let validateTasksArrayShape (events: obj list) : SquadUpdateOutcome option =
    events
    |> List.tryFind (fun e ->
        str e "type" = "tasks_created"
        && let t = get e "tasks" in
           isNullish t || not (isArray t))
    |> Option.map (fun _ -> InvalidInput "tasks_created must have a non-empty tasks array.")

let validateTaskFields (events: obj list) : SquadUpdateOutcome option =
    events
    |> List.tryPick (fun e ->
        if str e "type" <> "tasks_created" then
            None
        else
            let tasks = (get e "tasks") :?> obj array

            tasks
            |> Array.tryFind (fun t -> str t "title" = "" || str t "description" = "")
            |> Option.map (fun badT ->
                let v = get badT "taskId"
                let badId = if isNullish v then "<no-id>" else string v
                InvalidInput(sprintf "task '%s' must have non-empty title and description." badId)))

let private aggregateAssignedTasks (events: obj list) : RawTask list * bool =
    events
    |> List.fold
        (fun (acc, hasCancelled) e ->
            if str e "type" = "tasks_created" then
                let tasks = (get e "tasks") :?> obj array |> Array.toList

                let extracted =
                    tasks
                    |> List.map (fun t ->
                        (get t "taskId" |> fun v -> if isNullish v then None else Some(string v)),
                        str t "title",
                        str t "description",
                        let dr = get t "dependsOn"

                        if isNullish dr || not (isArray dr) then
                            []
                        else
                            (dr :?> obj array) |> Array.map string |> Array.toList)

                (acc @ extracted, hasCancelled)
            else
                (acc, true))
        ([], false)

let private commitAssigned
    (rt: CoordinatorRuntime)
    (existingTaskIds: Set<string>)
    (assigned: (string * string * string * string list) list)
    (hasCancelled: bool)
    : JS.Promise<Result<string, SquadUpdateOutcome>> =
    promise {
        let newIds = assigned |> List.map (fun (id, _, _, _) -> id) |> Set.ofList
        let allIds = Set.union existingTaskIds newIds

        let dangling =
            assigned
            |> List.collect (fun (id, _, _, deps) ->
                deps
                |> List.filter (fun d -> not (Set.contains d allIds))
                |> List.map (fun d -> id, d))

        if dangling <> [] then
            return Error(DependencyErrors dangling)
        else
            let depsList = assigned |> List.map (fun (id, _, _, deps) -> id, deps)

            let existingDeps =
                rt.Dag.Tasks |> Map.toList |> List.map (fun (id, t) -> id, t.DependsOn)

            match detectCycle (existingDeps @ depsList) with
            | Some cycle -> return Error(CycleDetected cycle)
            | None ->
                let createdTasks =
                    assigned |> List.map (fun (tid, title, desc, deps) -> tid, title, desc, deps)

                let! appendOk =
                    if createdTasks = [] then
                        Promise.lift (Ok())
                    else
                        commitEvent rt (TasksCreated(rt.Dag.SessionId, createdTasks))

                match appendOk with
                | Error err ->
                    rt.InjectError <- Some(sprintf "TasksCreated append failed: %s" err)
                    return Error(InvalidInput "event log append failed.")
                | Ok() ->
                    if createdTasks <> [] then
                        let now = rt.Deps.Now()

                        for (tid, title, desc, deps) in assigned do
                            rt.Dag <- rt.Dag |> addTask (create tid title desc deps now)

                    if hasCancelled then
                        do! handleSquadKill rt None

                    let resultText =
                        if hasCancelled && createdTasks = [] then
                            sprintf "Squad session %s cancelled." rt.Dag.SessionId
                        else
                            sprintf "%d task(s) created, scheduler notified." (List.length createdTasks)

                    schedulerTick rt |> Promise.start |> ignore
                    return Ok resultText
    }

let handleSquadUpdate (rt: CoordinatorRuntime) (args: obj) : JS.Promise<string> =
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
                        let allRawTasks, hasCancelled = aggregateAssignedTasks events
                        let existingTaskIds = rt.Dag.Tasks |> Map.toList |> List.map fst |> Set.ofList

                        let idGen =
                            { Generate = generateTaskId
                              RefExists = fun cand -> rt.Deps.ShowRefExists rt.ProjectRoot cand }

                        match assignTaskIds existingTaskIds allRawTasks idGen with
                        | Error() -> return formatSquadUpdateOutcome IdExhausted
                        | Ok assigned ->
                            match! commitAssigned rt existingTaskIds assigned hasCancelled with
                            | Error o -> return formatSquadUpdateOutcome o
                            | Ok text -> return text
    }
