namespace Wanxiangshu.Next.Tests.OpenCode

open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.OpenCode

module SessionPortLifecycleTests =

    [<Fact>]
    let ``Parent cancellation aborts all child sessions and cleans up`` () =
        task {
            let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
            let sessionPort = InjectedSessionPort(None, eventPort) :> ISessionHostPort
            let parentId = SessionId.create "parent-sess"
            let mutable childTerminalCount = 0

            let childOptions: OpenCodeChildOptions =
                { Title = Some "Child 1"
                  Agent = None
                  Directory = None }

            let! childIdRes = sessionPort.CreateChildSession(parentId, childOptions)

            let childId =
                match childIdRes with
                | Ok cId -> cId
                | Error err -> failwith err

            use _subChild =
                sessionPort.SubscribeTerminal(
                    childId,
                    fun _ outcome ->
                        match outcome with
                        | Aborted _ -> childTerminalCount <- childTerminalCount + 1
                        | _ -> ()
                )

            let! _ = sessionPort.AbortSession(parentId)

            Assert.Equal(1, childTerminalCount)
            let childOut = sessionPort.GetSessionOutput(childId)
            Assert.True(childOut |> List.exists (fun line -> line.Contains("Aborted")))
        }
