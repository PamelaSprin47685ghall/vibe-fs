module VibeFs.Tests.KnowledgeGraphWorkflowTests

open Fable.Core
open VibeFs.Tests.Assert
open VibeFs.Shell.KnowledgeGraphWorkflow

let drainPendingJobsClearsSink () =
    let recorded = ResizeArray<string * string>()
    let sink = createSink (fun title result -> recorded.Add(title, result))
    trackBackgroundJob sink (Promise.lift ())
    let jobs = drainPendingJobs sink
    check "drain returns one job" (jobs.Length = 1)
    check "drain clears sink" (sink.Jobs.Count = 0)

let awaitAllBackgroundJobsEmpty () = promise {
    do! awaitAllBackgroundJobs [||]
    check "await empty jobs" true
}

let recordLaunchResultInvokesCallback () =
    let mutable last = ""
    let sink = createSink (fun title result -> last <- title + "|" + result)
    recordLaunchResult sink "Daily" "ok"
    equal "recordLaunchResult callback" "Daily|ok" last

let run () = promise {
    drainPendingJobsClearsSink ()
    do! awaitAllBackgroundJobsEmpty ()
    recordLaunchResultInvokesCallback ()
}