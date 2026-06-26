module Wanxiangshu.Shell.KnowledgeGraphRuntimeTestPorts

open Fable.Core
open Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState
open Wanxiangshu.Shell.KnowledgeGraphWorkflow

type KnowledgeGraphRuntimeTestPorts =
    { SwapState: (KnowledgeGraphState -> KnowledgeGraphState * obj) -> obj
      RunOnCommandQueue: (unit -> JS.Promise<unit>) -> JS.Promise<unit>
      AwaitBackgroundSinkJobs: unit -> JS.Promise<unit> }

let createFromStateQueueSink
    (getState: unit -> KnowledgeGraphState)
    (setState: KnowledgeGraphState -> unit)
    (enqueue: (unit -> JS.Promise<unit>) -> JS.Promise<unit>)
    (sink: BackgroundJobSink)
    : KnowledgeGraphRuntimeTestPorts =
    { SwapState =
          fun update ->
              let nextState, value = update (getState ())
              setState nextState
              value
      RunOnCommandQueue = fun work -> enqueue (fun () -> work ())
      AwaitBackgroundSinkJobs =
          fun () ->
              let jobs = drainPendingJobs sink
              awaitAllBackgroundJobs jobs }