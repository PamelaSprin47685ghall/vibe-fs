module Wanxiangshu.Runtime.Wanxiangzhen.SquadEventDisplayCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Kernel.Wanxiangzhen.SquadPrompts
open Wanxiangshu.Runtime.Yaml
open Wanxiangshu.Runtime.Dyn

let private fmKey (e: SquadEvent) =
    let o =
        createObj [ "squad_event", box (eventTypeName e); "session_id", box (eventSessionId e) ]

    (match e with
     | TasksCreated(_, tasks) ->
         let items = System.Collections.Generic.List<obj>()

         for item in tasks do
             let o2 =
                 createObj
                     [ "task_id", box item.taskId
                       "title", box item.title
                       "description", box item.description ]

             if item.dependsOn <> [] then
                 setKey o2 "depends_on" (box (List.toArray item.dependsOn))

             items.Add o2

         setKey o "tasks" (box (items.ToArray()))
     | TaskStarted(_, tid, wt, branch) ->
         setKey o "task_id" (box tid)
         setKey o "worktree_path" (box wt)
         setKey o "branch_name" (box branch)
     | TaskSubmitted(_, tid, sha) ->
         setKey o "task_id" (box tid)
         setKey o "commit_sha" (box sha)
     | TaskMerged(_, tid, sha) ->
         setKey o "task_id" (box tid)
         setKey o "master_sha" (box sha)
     | TaskDone(_, tid, merged) ->
         setKey o "task_id" (box tid)
         setKey o "merged" (box merged)
     | TaskError(_, tid, err) ->
         setKey o "task_id" (box tid)
         setKey o "error" (box err)
     | SquadCancelled _ -> ()
     | SquadCreated(_, req) -> setKey o "requirement" (box req))

    o

let encodeEvent (e: SquadEvent) : string =
    let fmObj = fmKey e
    let yamlText = stringify fmObj
    let prose = eventProse e
    "---\n" + yamlText + "---\n\n" + prose

let encodeEvents (events: SquadEvent list) : string =
    events |> List.map encodeEvent |> String.concat "\n"

let private strField (parsed: obj) k =
    let v = get parsed k
    if isNullish v then None else Some(string v)

let private intField (parsed: obj) k =
    let v = get parsed k
    if isNullish v then None else Some(unbox<int> v)

let private boolField (parsed: obj) k =
    let v = get parsed k
    if isNullish v then None else Some(unbox<bool> v)

let private arrField (parsed: obj) k =
    let v = get parsed k

    if isNullish v || not (isArray v) then
        None
    else
        Some((v :?> obj array) |> Array.map string |> Array.toList)

let private optStr (parsed: obj) k = strField parsed k |> Option.defaultValue ""

let private optBool (parsed: obj) k = boolField parsed k |> Option.defaultValue false

let private parseTaskDef (o: obj) : TaskItem option =
    let tid = str o "task_id"

    if tid = "" then
        None
    else
        let title = str o "title"
        let desc = str o "description"
        let depsArr = get o "depends_on"

        let deps =
            if isNullish depsArr || not (isArray depsArr) then
                []
            else
                (depsArr :?> obj array) |> Array.map string |> Array.toList

        Some
            { taskId = tid
              title = title
              description = desc
              dependsOn = deps }

let private parseTasks (parsed: obj) : TaskItem list =
    let tasksRaw = get parsed "tasks"

    if isNullish tasksRaw || not (isArray tasksRaw) then
        []
    else
        (tasksRaw :?> obj array)
        |> Array.toList
        |> List.choose parseTaskDef

let private parseEvent (parsed: obj) (typeName: string) : SquadEvent option =
    let sid = str parsed "session_id"

    match typeName with
    | "squad_created" ->
        let req = optStr parsed "requirement"
        Some(SquadCreated(sid, req))
    | "tasks_created" -> Some(TasksCreated(sid, parseTasks parsed))
    | "task_started" ->
        let tid = optStr parsed "task_id"
        let wt = optStr parsed "worktree_path"
        let branch = optStr parsed "branch_name"
        Some(TaskStarted(sid, tid, wt, branch))
    | "task_submitted" ->
        let tid = optStr parsed "task_id"
        let sha = optStr parsed "commit_sha"
        Some(TaskSubmitted(sid, tid, sha))
    | "task_merged" ->
        let tid = optStr parsed "task_id"
        let sha = optStr parsed "master_sha"
        Some(TaskMerged(sid, tid, sha))
    | "task_done" ->
        let tid = optStr parsed "task_id"
        let merged = optBool parsed "merged"
        Some(TaskDone(sid, tid, merged))
    | "task_error" ->
        let tid = optStr parsed "task_id"
        let err = optStr parsed "error"
        Some(TaskError(sid, tid, err))
    | "squad_cancelled" -> Some(SquadCancelled sid)
    | _ -> None

let decodeEvents (text: string) : SquadEvent list =
    let rec scan (s: string) (acc: SquadEvent list) : SquadEvent list =
        let startIdx = s.IndexOf "---\n"

        if startIdx < 0 then
            acc
        else
            let afterStart = s.Substring(startIdx + 4)
            let endIdx = afterStart.IndexOf "\n---"

            if endIdx < 0 then
                acc
            else
                let yamlText = afterStart.Substring(0, endIdx).Trim()
                let rest = afterStart.Substring(endIdx + 4)

                let evOpt =
                    let parsed = parse yamlText
                    let typeName = str parsed "squad_event"

                    match eventTypeNameFromString typeName with
                    | None -> None
                    | Some _ -> parseEvent parsed typeName

                match evOpt with
                | Some ev -> scan rest (ev :: acc)
                | None -> scan rest acc

    scan text [] |> List.rev

let decodeEvent (text: string) : SquadEvent option = decodeEvents text |> List.tryHead
