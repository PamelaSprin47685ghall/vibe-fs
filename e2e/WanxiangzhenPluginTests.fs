module Wanxiangshu.E2e.WanxiangzhenPluginTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.HarnessTypes

[<Import("start", "./wanxiangzhen-harness.js")>]
let private startHarness: obj -> JS.Promise<obj> = jsNative

let private harnessFromObj (o: obj) : WanxiangzhenHarness = unbox o

let private mkTask (taskId: string) (title: string) (desc: string)
    (deps: string list) : obj =
    let baseFields = [
        "title",       box title
        "description", box desc
        "dependsOn",   box (Array.ofList deps)
    ]
    if taskId = "" then createObj baseFields
    else createObj (("taskId", box taskId) :: baseFields)

let private mkTasksCreated (tasks: obj list) : obj =
    createObj [
        "type",  box "tasks_created"
        "tasks", box (Array.ofList tasks)
    ]

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let mutable ok = 0
        let chk label cond =
            check label cond
            if cond then
                ok <- ok + 1

        try
            printfn "Starting Wanxiangzhen E2E inProcess tests..."
            let! apiObj =
                startHarness (createObj [ "inProcess", box true ])
            if not (isNullish apiObj?error) then
                failwithf "harness start failed: %O\n%O" apiObj?error apiObj?stack
            let harness = harnessFromObj apiObj

            chk "wxz.inProcess.mode" (harness.mode = "inProcess")

            // 1. /squad command — creates a new squad session
            let! cmdRes =
                harness.runCommand "squad" "sess-wxz-e2e"
                    (box "add feature-1")
            let parts: obj[] = unbox cmdRes
            let mutable text = ""
            for p in parts do
                text <- text + (string (get p "text"))
            
            chk "wxz.command.squad.output"
                (text.Contains "squad_event: squad_created"
                 && text.Contains "add feature-1")
            chk "wxz.command.squad.ndjson"
                ((harness.readMeta()).Contains "squad_created")

            // 2. squad_update — submit task decomposition
            let updateArgs = createObj [
                "events",
                box [|
                    mkTasksCreated [
                        mkTask "task-e2e-01" "T1" "D1" []
                        mkTask "task-e2e-02" "T2" "D2" []
                    ]
                |]
            ]
            let! _ = harness.toolRound "squad_update" updateArgs
            chk "wxz.ndjson.tasks_created"
                ((harness.readMeta()).Contains "tasks_created")

            // 3. scheduler trigger & wait — tasks should transition to Running
            let spawnCalls = harness.getSpawnCalls ()
            let wtAddCalls = harness.getWorktreeAddCalls ()
            chk "wxz.scheduler.spawned" (spawnCalls.Length > 0)
            chk "wxz.scheduler.wtAdded" (wtAddCalls.Length > 0)

            // 4. register — POST /task/{taskId}/register with pid
            let! regRes1 =
                harness.coordinatorPost
                    "/task/task-e2e-01/register"
                    (createObj [ "pid", box 12345 ]) None
            let regStatus1 = unbox<int> (get regRes1 "status")
            chk "wxz.register1.status" (regStatus1 = 200)

            let! regRes2 =
                harness.coordinatorPost
                    "/task/task-e2e-02/register"
                    (createObj [ "pid", box 12346 ]) None
            let regStatus2 = unbox<int> (get regRes2 "status")
            chk "wxz.register2.status" (regStatus2 = 200)

            // 5. submit — POST /task/{taskId}/submit with commitSha (for task-e2e-01)
            harness.setRevParseRef "task-e2e-01" "abc"
            harness.setMergeBaseResult true
            harness.setMergeFfResult "merged-sha"

            let! subRes =
                harness.coordinatorPost
                    "/task/task-e2e-01/submit"
                    (createObj [ "commitSha", box "abc" ]) None
            let subStatus = unbox<int> (get subRes "status")
            chk "wxz.submit.status" (subStatus = 200)

            // 5b. done — POST /task/{taskId}/done to mark done and write TaskDone (for task-e2e-02)
            let! doneRes =
                harness.coordinatorPost
                    "/task/task-e2e-02/done"
                    (createObj []) None
            let doneStatus = unbox<int> (get doneRes "status")
            chk "wxz.done.status" (doneStatus = 200)

            // 6. verify ndjson log chain — all expected events present
            let ndjson = harness.readMeta()
            chk "wxz.chain.squad_created"
                (ndjson.Contains "squad_created")
            chk "wxz.chain.tasks_created"
                (ndjson.Contains "tasks_created")
            chk "wxz.chain.task_started"
                (ndjson.Contains "task_started")
            chk "wxz.chain.task_submitted"
                (ndjson.Contains "task_submitted")
            chk "wxz.chain.task_merged" (ndjson.Contains "task_merged")
            chk "wxz.chain.task_done" (ndjson.Contains "task_done")

            do! harness.dispose()
            printfn "✓ inProcess tests completed successfully"

            // ── opencode serve mode: auth tests ──
            printfn "Starting Wanxiangzhen E2E opencode serve tests..."
            let! apiObj2 =
                startHarness (createObj [ "inProcess", box false ])
            if not (isNullish apiObj2?error) then
                failwithf "harness2 start failed: %O\n%O" apiObj2?error apiObj2?stack
            let harness2 = harnessFromObj apiObj2

            chk "wxz.opencode.mode" (harness2.mode = "opencode")

            // 1. GET /state without auth → 401
            let! unauthRes =
                harness2.coordinatorGet "/state" (Some "__NO_AUTH__")
            let unauthStatus = unbox<int> (get unauthRes "status")
            chk "wxz.opencode.unauth" (unauthStatus = 401)

            // 2. GET /state with default token → 200
            let! authRes = harness2.coordinatorGet "/state" None
            let authStatus = unbox<int> (get authRes "status")
            chk "wxz.opencode.auth" (authStatus = 200)

            do! harness2.dispose()
            printfn "✓ opencode serve tests completed successfully"

            printfn "\n✓ %d wanxiangzhen E2E checks passed" ok
            return summary ()
        with ex ->
            printfn "E2E ERROR: %O" ex
            return 1
    }
