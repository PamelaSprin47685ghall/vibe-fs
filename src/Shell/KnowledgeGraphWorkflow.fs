module Wanxiangshu.Shell.KnowledgeGraphWorkflow

open Fable.Core

/// Holds in-flight bookkeeper background promises and routes launch result text
/// back into host state (typically `UpdateLatestLaunchResultCmd`).
type BackgroundJobSink =
    { Jobs: ResizeArray<JS.Promise<unit>>
      RecordResult: string -> string -> unit }

let createSink (recordResult: string -> string -> unit) : BackgroundJobSink =
    { Jobs = ResizeArray()
      RecordResult = recordResult }

let trackBackgroundJob (sink: BackgroundJobSink) (job: JS.Promise<unit>) : unit =
    sink.Jobs.Add(job)
    job |> Promise.start

let recordLaunchResult (sink: BackgroundJobSink) (title: string) (result: string) : unit =
    sink.RecordResult title result

let drainPendingJobs (sink: BackgroundJobSink) : JS.Promise<unit> array =
    let jobs = sink.Jobs |> Seq.toArray
    sink.Jobs.Clear()
    jobs

let awaitAllBackgroundJobs (jobs: JS.Promise<unit> array) : JS.Promise<unit> =
    if jobs.Length > 0 then
        promise {
            let! _ = Promise.all jobs
            return ()
        }
    else
        Promise.lift ()