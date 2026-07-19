module Wanxiangshu.Tests.Wanxiangzhen.TestDoubles

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadConfig
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Tests.Wanxiangzhen.TestTypes

let mkFake = Wanxiangshu.Tests.Wanxiangzhen.TestTypes.mkFake

let private lcgPrng (s: FakeState) () : float =
    s.randomSeed <- (s.randomSeed * 1103515245 + 12345) &&& 0x7FFFFFFF
    float s.randomSeed / 2147483647.0

let mkDeps (s: FakeState) : CoordinatorDeps =
    { PromptSession =
        fun c m p ->
            match s.promptSessionOverride with
            | Some f -> f c m p
            | None ->
                s.promptSessionCalls <- s.promptSessionCalls @ [ (m, p) ]
                Promise.lift ()
      GetLatestSquadSessionId =
        fun () ->
            match s.getLatestSquadSessionIdOverride with
            | Some f -> f ()
            | None -> Promise.lift None
      GetSquadDag =
        fun sessionId ->
            match s.getSquadDagOverride with
            | Some f -> f sessionId
            | None -> Promise.lift (Wanxiangshu.Kernel.Wanxiangzhen.Dag.empty sessionId "")
      GetSquadSessions =
        fun () ->
            match s.getSquadSessionsOverride with
            | Some f -> f ()
            | None -> Promise.lift Map.empty
      AppendSquadEvent =
        fun _ _ e ->
            s.appendSquadEventCalls <- s.appendSquadEventCalls @ [ e ]
            Promise.lift (Ok())
      TryWorktreeAdd =
        fun c b p b2 ->
            match s.tryWorktreeAddOverride with
            | Some f -> f c b p b2
            | None ->
                s.tryWorktreeAddCalls <- s.tryWorktreeAddCalls @ [ (c, b, p, b2) ]
                s.log.Value <- s.log.Value @ [ "tryWorktreeAdd"; b ]
                Ok ""
      TryWorktreeRemoveForce =
        fun c p ->
            s.tryWorktreeRemoveForceCalls <- s.tryWorktreeRemoveForceCalls @ [ (c, p) ]
            s.log.Value <- s.log.Value @ [ "tryWorktreeRemoveForce"; p ]
            Ok ""
      TryBranchDeleteForce =
        fun c b ->
            s.tryBranchDeleteForceCalls <- s.tryBranchDeleteForceCalls @ [ (c, b) ]
            s.log.Value <- s.log.Value @ [ "tryBranchDeleteForce"; b ]
            Ok ""
      ShowRefExists =
        fun c b ->
            s.showRefExistsCalls <- s.showRefExistsCalls @ [ (c, b) ]
            false
      RevParseHead =
        fun c ->
            s.revParseHeadCalls <- s.revParseHeadCalls @ [ c ]
            s.revParseRefResult
      RevParseRef =
        fun c r ->
            s.revParseRefCalls <- s.revParseRefCalls @ [ (c, r) ]

            match s.revParseRefOverrides.TryGetValue r with
            | true, v -> v
            | false, _ ->
                match s.revParseRefOverride with
                | Some f -> f c r
                | None -> s.revParseRefResult
      RevParseBranch =
        fun c ->
            s.revParseBranchCalls <- s.revParseBranchCalls @ [ c ]

            match s.revParseBranchOverride with
            | Some f -> f c
            | None -> s.revParseBranchResult
      IsDetached =
        fun c ->
            s.isDetachedCalls <- s.isDetachedCalls @ [ c ]
            false
      StatusIsClean =
        fun c ->
            match s.statusIsCleanOverride with
            | Some f -> f c
            | None ->
                s.statusIsCleanCalls <- s.statusIsCleanCalls @ [ c ]
                s.statusClean
      MergeBaseIsAncestor =
        fun c a d ->
            s.mergeBaseIsAncestorCalls <- s.mergeBaseIsAncestorCalls @ [ (c, a, d) ]
            s.mergeBaseCallCount <- s.mergeBaseCallCount + 1

            match s.mergeBaseOverride with
            | Some f -> f c a d
            | None -> s.mergeBaseCallCount <= s.mergeBaseTrueForFirstN
      MergeFfOnly =
        fun c b ->
            s.mergeFfOnlyCalls <- s.mergeFfOnlyCalls @ [ (c, b) ]
            s.mergeFfOnlyCalled <- true
            s.revParseRefOverrides <- s.revParseRefOverrides.Add(s.revParseBranchResult, "merged-sha")
            s.revParseRefResult
      CreateSymlinks = fun _ _ _ -> s.createSymlinksCount <- s.createSymlinksCount + 1
      SpawnSlave =
        fun t wt e p ->
            s.spawnSlaveCalls <- s.spawnSlaveCalls @ [ (t, wt, e, p) ]
            s.log.Value <- s.log.Value @ [ "spawnSlave"; t ]
      IsPidAlive = fun _ -> s.isPidAliveResult
      KillPid =
        fun p signal ->
            match s.killPidOverride with
            | Some f -> f p signal
            | None ->
                s.killPidCalled <- true
                s.killPidPid <- Some p
                s.killPidSignal <- Some signal
      WaitForPidDeath =
        fun p r ->
            s.waitForPidDeathCalls <- s.waitForPidDeathCalls @ [ (p, r) ]
            Promise.lift ()
      StartPolling =
        fun ms callback ->
            match s.startPollingOverride with
            | Some g -> g ms callback
            | None ->
                s.startPollingCalls <- s.startPollingCalls @ [ (ms, callback) ]
                box "poll-handle"
      StopPolling =
        fun h ->
            match s.stopPollingOverride with
            | Some f -> f h
            | None -> s.stopPollingCalls <- s.stopPollingCalls @ [ h ]
      Now = fun () -> "2025-01-01T00:00:00.000Z"
      HasCommits =
        fun c ->
            match s.hasCommitsOverride with
            | Some f -> f c
            | None -> s.hasCommitsResult
      RandomGen = lcgPrng s }

let mkRuntime (deps: CoordinatorDeps) : CoordinatorRuntime =
    { Dag = empty "squad-session-001" ""
      Sessions = Map.empty
      Config =
        { defaults with
            MasterBranch = Some "main" }
      MasterBranch = "main"
      ProjectRoot = "/tmp/project"
      MasterSessionId = ""
      Client = createObj []
      Token = "test-token"
      CoordinatorUrl = "http://127.0.0.1:0"
      GitQueue = SerialQueue()
      DagQueue = SerialQueue()
      InjectQueue = SerialQueue()
      Server =
        { Port = 0
          Url = ""
          Close = fun () -> () }
      Scheduling = false
      PidPollHandle = None
      GitError = None
      InjectError = None
      IsE2e = false
      SentWarnings = Set.empty
      Deps = deps }

let mkTask (taskId: string) (title: string) (desc: string) (deps: string list) : obj =
    let baseFields =
        [ "title", box title
          "description", box desc
          "dependsOn", box (Array.ofList deps) ]

    match taskId with
    | "" -> createObj baseFields
    | _ -> createObj (("taskId", box taskId) :: baseFields)

let mkTasksCreated (tasks: obj list) : obj =
    createObj [ "type", box "tasks_created"; "tasks", box (Array.ofList tasks) ]

let mkTaskEvent (taskId: string) (title: string) (desc: string) (deps: string list) : obj =
    mkTasksCreated [ mkTask taskId title desc deps ]

let mkSquadUpdateArgs (events: obj array) : obj = createObj [ "events", box events ]

let findTask (id: string) (dag: Dag) : SquadTask option = dag.Tasks |> Map.tryFind id

[<Emit("fetch($0, $1)")>]
let fetch (url: string) (init: obj) : JS.Promise<obj> = jsNative

[<Global>]
let JSON: obj = jsNative

let fetchJson (url: string) (init: obj) : JS.Promise<{| status: int; body: obj |}> =
    promise {
        let! resp = fetch url (box init)
        let! body = resp?json ()
        return {| status = resp?status; body = body |}
    }
