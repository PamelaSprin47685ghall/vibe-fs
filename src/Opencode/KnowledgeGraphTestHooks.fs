module VibeFs.Opencode.KnowledgeGraphTestHooks

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.KnowledgeGraphJobTesting
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Shell.Dyn
open VibeFs.Shell.KnowledgeGraphRuntimeTestPorts

type KnowledgeGraphTestHooks(runtime: KnowledgeGraphRuntime) =
    member _.RegisterJob(sessionID: string, workspaceRoot: string, kindTag: string, payload: obj) : unit =
        let readField fieldName = str payload fieldName
        let ctx = buildTestingJobContext workspaceRoot kindTag readField
        runtime.RegisterJob(sessionID, ctx)

    member _.TakeLaunches() : obj array =
        let ports = runtime.CreateTestPorts()
        unbox (ports.SwapState(fun s ->
            let launches, next = drainLaunches s
            next, box launches))
        |> List.map box
        |> List.toArray

    member _.WaitJobs() : JS.Promise<unit> =
        promise {
            let ports = runtime.CreateTestPorts()
            do! ports.RunOnCommandQueue(fun () -> Promise.lift ())
            do! ports.AwaitBackgroundSinkJobs()
        }

type KnowledgeGraphRuntime with
    member this.TestHooks : KnowledgeGraphTestHooks = KnowledgeGraphTestHooks(this)