module VibeFs.Mux.KnowledgeGraphTestHooks

open System
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.KnowledgeGraphJobTesting
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Mux.KnowledgeGraphTools
open VibeFs.Shell.Dyn
open VibeFs.Shell.KnowledgeGraphRuntimeTestPorts

type MuxKnowledgeGraphTestHooks(runtime: MuxKnowledgeGraphRuntime) =
    member _.RegisterJob(sessionID: string, workspaceRoot: string, kindTag: string, payload: obj) : unit =
        if String.IsNullOrWhiteSpace workspaceRoot then
            failwith "Knowledge graph job workspaceRoot must be a non-empty directory path."

        let readField fieldName = str payload fieldName
        let ctx = buildTestingJobContext workspaceRoot kindTag readField
        runtime.RegisterJob(sessionID, ctx)

    member _.TakeLaunches() : obj array =
        let ports = runtime.CreateTestPorts()
        (unbox (ports.SwapState(fun s ->
            let launches, next = drainLaunches s
            next, box launches)) : BookkeeperLaunch list)
        |> List.map (fun l ->
            box (createObj [
                "agent", box l.agent
                "title", box l.title
                "prompt", box l.prompt
                "result", box l.result
            ]))
        |> List.toArray

    member _.WaitJobs() : JS.Promise<unit> =
        promise {
            let ports = runtime.CreateTestPorts()
            do! ports.RunOnCommandQueue(fun () -> Promise.lift ())
            do! ports.AwaitBackgroundSinkJobs()
        }

    member _.HasJob(sessionID: string) : bool =
        runtime.HasJobForTest(sessionID)

type MuxKnowledgeGraphRuntime with
    member this.TestHooks : MuxKnowledgeGraphTestHooks = MuxKnowledgeGraphTestHooks(this)