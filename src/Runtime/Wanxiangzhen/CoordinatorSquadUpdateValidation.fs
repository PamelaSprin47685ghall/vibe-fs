module Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorSquadUpdateValidation

open Fable.Core
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadUpdateIdAssign
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime

type RawTask = string option * string * string * string list

let parseEventsArgs (args: obj) : obj list option =
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

let aggregateAssignedTasks (events: obj list) : RawTask list * bool =
    let rawTasks, hasCancelled =
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

                    let newAcc = extracted |> List.fold (fun a x -> x :: a) acc
                    (newAcc, hasCancelled)
                else
                    (acc, true))
            ([], false)

    (List.rev rawTasks, hasCancelled)

let aggregateAndAssignTasks
    (rt: CoordinatorRuntime)
    (existingTaskIds: Set<string>)
    (events: obj list)
    : Result<TaskItem list * bool, SquadUpdateOutcome> =
    let allRawTasks, hasCancelled = aggregateAssignedTasks events

    let idGen =
        { Generate = fun () -> generateTaskIdWith rt.Deps.RandomGen
          RefExists = fun cand -> rt.Deps.ShowRefExists rt.ProjectRoot cand }

    match assignTaskIds existingTaskIds allRawTasks idGen with
    | Error() -> Error IdExhausted
    | Ok assigned -> Ok(assigned, hasCancelled)
