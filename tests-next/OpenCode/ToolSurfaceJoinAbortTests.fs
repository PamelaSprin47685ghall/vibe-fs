namespace Wanxiangshu.Next.Tests.OpenCodeTests

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.OpenCode.ToolSurfacePty

module ToolSurfaceJoinAbortTests =

    let private hostPort () =
        { new ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(_, _, _) =
                Task.FromResult(Ok(MessageId.create "accepted"))

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())

            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task

            member _.CreateChildSession(_, _) =
                Task.FromResult(Ok(SessionId.create "child-abort"))

            member _.GetSessionOutput(_) = [] }

    [<Fact>]
    let ``join_execute_with_aborted_signal_cancels_runtime_and_returns_CANCELLED`` () =
        task {
            let parentId = SessionId.create "parent-join-abort"
            let captured = ResizeArray<SessionId>()
            let signalCaptured = ResizeArray<SessionId>()

            let runtime =
                HostForkRuntime(
                    parentId,
                    hostPort (),
                    cancelFallbackRetries = (fun ids -> captured.AddRange(ids)),
                    cancelSignals = (fun ids -> signalCaptured.AddRange(ids))
                )

            let deps: PtyToolDeps =
                { SessionRoles = Dictionary<string, string>()
                  SessionDirectories = Dictionary<string, string>()
                  WorkspaceDirectory = None
                  RuntimeFor = fun _ -> Ok runtime
                  OrchestratorHostFor = fun _ -> failwith "not an orchestrator session" }

            let callbacks = ResizeArray<unit -> unit>()

            let signal =
                createObj
                    [ "aborted", box true
                      "addEventListener",
                      box (fun (_evt: string) (cb: obj) (_opts: obj) -> callbacks.Add(unbox<unit -> unit> cb))
                      "removeEventListener", box (fun _ _ -> ()) ]

            let ctx =
                createObj [ "sessionID", box (SessionId.value parentId); "abort", box signal ]

            let! result = ToolSurfaceJoin.joinExecute deps (createObj []) ctx
            let output = result.ToString()

            Assert.True(output.Contains("CANCELLED"), output)
            Assert.True(runtime.IsCancelled, "runtime was not cancelled by abort signal")
            Assert.True(captured.Contains(parentId), "fallback cancel was not invoked for parent")
            Assert.True(signalCaptured.Contains(parentId), "signal cancel was not invoked for parent")
        }
