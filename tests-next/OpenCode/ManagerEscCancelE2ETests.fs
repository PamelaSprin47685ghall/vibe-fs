namespace Wanxiangshu.Next.Tests.OpenCodeTests

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.OpenCode.HostSignalBootstrap
open Wanxiangshu.Next.Session

/// Esc on a manager join must stop the subagent's AABB retry loop, not only
/// the parent runtime.  This test drives the real HostSignalRouter and
/// HostForkRuntime together without any wall-clock waits.
module ManagerEscCancelE2ETests =

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
                Task.FromResult(Ok(SessionId.create "child-esc"))

            member _.GetSessionOutput(_) = [] }

    let private statusEvent (sessionId: string) (statusType: string) (attempt: string) =
        createObj
            [ "type", box "session.status"
              "properties",
              box (
                  createObj
                      [ "sessionID", box sessionId
                        "status",
                        box (
                            createObj
                                [ "type", box statusType
                                  "attempt", box attempt
                                  "message", box "provider retry" ]
                        ) ]
              ) ]

    [<Fact>]
    let ``Esc_cancel_on_manager_join_stops_child_retry_signals`` () =
        task {
            let parentId = "parent-esc"
            let childId = "child-esc"
            let ownedSessions = HashSet<string>([ parentId; childId ])
            let receivedSignals = ResizeArray<HostSignal>()
            let capturedFallback = ResizeArray<SessionId>()
            let capturedSignals = ResizeArray<SessionId>()

            let signalRouter =
                HostSignalRouter(ownedSessions, (fun signal -> receivedSignals.Add(signal)))

            let cancelSignals (ids: SessionId seq) =
                ids
                |> Seq.iter (fun id ->
                    capturedSignals.Add(id)
                    signalRouter.UnregisterOwned(id))

            let runtime =
                HostForkRuntime(
                    SessionId.create parentId,
                    hostPort (),
                    cancelFallbackRetries = (fun ids -> capturedFallback.AddRange(ids)),
                    cancelSignals = cancelSignals
                )

            // Link a DevOps child so cancel must tear it down too.
            let! forkResult = runtime.Fork("devops-1", AgentRole.DevOps, "inspect")
            Assert.True(Result.isOk forkResult, sprintf "fork failed: %A" forkResult)

            // Child is actively retrying: router owns it, so retry/idle signals
            // are dispatched.
            signalRouter.ObserveLocal(statusEvent childId "retry" "1")
            Assert.Equal(1, receivedSignals.Count)

            signalRouter.ObserveLocal(statusEvent childId "idle" "1")
            Assert.Equal(2, receivedSignals.Count)

            // User presses Esc on the manager join.  The parent runtime must
            // tear down fallback and signal registration for itself and every
            // linked child synchronously before returning.
            runtime.Cancel()

            Assert.True(runtime.IsCancelled, "parent runtime was not cancelled")

            Assert.Equal(2, capturedFallback.Count)
            Assert.True(capturedFallback.Contains(SessionId.create parentId))
            Assert.True(capturedFallback.Contains(SessionId.create childId))

            Assert.Equal(2, capturedSignals.Count)
            Assert.True(capturedSignals.Contains(SessionId.create parentId))
            Assert.True(capturedSignals.Contains(SessionId.create childId))

            // After cancel, the child is no longer owned.  Further retry/idle
            // events for the child must be dropped by the router, preventing
            // new ProviderRetryAttempt flushes and stopping the AABB loop.
            signalRouter.ObserveLocal(statusEvent childId "retry" "2")
            Assert.Equal(2, receivedSignals.Count)

            signalRouter.ObserveLocal(statusEvent childId "idle" "2")
            Assert.Equal(2, receivedSignals.Count)
        }
