module Wanxiangshu.Tests.CompactionHookOpenCodeTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.BacklogSession
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.WorkBacklogToolsCodec

[<Import("mkdtempSync", "node:fs")>]
let private mkdtemp (prefix: string) : string = jsNative

[<Import("rmSync", "node:fs")>]
let private rm (path: string) (opts: obj) : unit = jsNative

let private commitBacklog (dir: string) (sessionID: string) (plan: string) : JS.Promise<unit> =
    let args: TodoWriteArgs =
        { Todos =
            [| { Content = "todo one"
                 Status = Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.Completed
                 Priority = Wanxiangshu.Kernel.ToolArgs.TodoItemPriority.High } |]
          AhaMoments = "aha"
          ChangesAndReasons = "changes"
          Gotchas = "gotchas"
          LessonsAndConventions = "lessons"
          Plan = plan
          SelectMethodology = [] }

    appendWorkBacklogCommittedOrFail dir sessionID args

let private runHook (dir: string) (sessionID: string) (output: obj) : JS.Promise<unit> =
    let scope = Wanxiangshu.Runtime.RuntimeScope.create ()
    let backlogSession = BacklogSession(opencode, scope)
    let input = createObj [ "sessionID", box sessionID ]

    Wanxiangshu.Hosts.Opencode.CompactionTransform.compactingTransform dir scope backlogSession input output

let private freshOutput () =
    createObj [ "context", box ([||]: string array) ]

let private contextEntries (output: obj) : string array =
    Wanxiangshu.Runtime.Dyn.get output "context" :?> string array

let testHookInjectsDirectiveAndBacklog () =
    promise {
        let tempDir = mkdtemp "compaction-hook-"

        try
            let sessionID = "s-hook-backlog"
            do! commitBacklog tempDir sessionID "PLAN_MARKER_123"
            let output = freshOutput ()
            do! runHook tempDir sessionID output
            let context = contextEntries output
            equal "context should gain exactly one entry" 1 context.Length
            check "directive forbids executing history" (context.[0].Contains("MUST NOT be executed"))
            check "do-not-exec fence wraps backlog" (context.[0].Contains("<do-not-exec>"))
            check "backlog plan present" (context.[0].Contains("PLAN_MARKER_123"))
            check "no dialogue history duplication" (not (context.[0].Contains("Dialogue History")))
        finally
            rm tempDir (createObj [ "recursive", box true; "force", box true ])
    }

let testHookInjectsDirectiveWithoutBacklog () =
    promise {
        let tempDir = mkdtemp "compaction-hook-"

        try
            let output = freshOutput ()
            do! runHook tempDir "s-hook-empty" output
            let context = contextEntries output
            equal "context should gain exactly one entry" 1 context.Length
            check "directive still present without backlog" (context.[0].Contains("MUST NOT be executed"))
            check "no empty fence without backlog" (not (context.[0].Contains("<do-not-exec>")))
        finally
            rm tempDir (createObj [ "recursive", box true; "force", box true ])
    }

let run () =
    promise {
        do! testHookInjectsDirectiveAndBacklog ()
        do! testHookInjectsDirectiveWithoutBacklog ()
    }
