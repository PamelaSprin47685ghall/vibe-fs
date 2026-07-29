namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session

module HostForkWorkRecordTests =

    let private makeHost () =
        let mutable terminal: (SessionId -> TerminalOutcome -> unit) option = None
        let childId = SessionId.create "manager-child"

        let host =
            { new ISessionHostPort with
                member _.SubscribeTerminal(_, listener) =
                    terminal <- Some listener

                    { new IDisposable with
                        member _.Dispose() = terminal <- None }

                member _.SendPrompt(_, _, _) = Task.FromResult(Ok(MessageId.create "accepted"))
                member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
                member _.AbortSession(_) = Task.FromResult(Ok())
                member _.AbortChildren(_) = Task.FromResult(()) :> Task
                member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
                member _.GetSessionOutput(_) = [] }

        let complete () =
            terminal
            |> Option.iter (fun listener ->
                listener
                    childId
                    (TerminalOutcome.Completed(
                        { SessionId = childId
                          RootUserMessageId = MessageId.create "manager-root"
                          AssistantMessageId = MessageId.create "manager-assistant"
                          Role = "manager"
                          Directory = ""
                          FinalText = "manager session A"
                          Parts = [||] }
                    )))

        host, complete

    let private completedPayload joined =
        match joined with
        | Ok completion ->
            match completion.Outcome with
            | AgentCompleted payload -> payload
            | other -> failwithf "Expected completed manager, got %A" other
        | Error error -> failwithf "Expected completion, got %A" error

    [<Fact>]
    let ``HostForkRuntime uses session A when companion work record is missing`` () =
        task {
            let host, complete = makeHost ()

            let runtime =
                HostForkRuntime(
                    SessionId.create "orchestrator",
                    host,
                    childWorkRecordFor = (fun _ -> None)
                )

            let! forked = runtime.Fork("manager", AgentRole.Manager, "manage", agent = "fast-manager")
            Assert.Equal(Ok(ForkResult.Created "manager"), forked)

            complete ()
            let! joined = runtime.Join()
            let payload = completedPayload joined

            Assert.True(payload.WorkRecord.IsSome, "A must backstop a missing companion work record")
            Assert.Equal("manager session A", payload.WorkRecord.Value.Text)
        }

    [<Fact>]
    let ``HostForkRuntime prefers companion B over session A`` () =
        task {
            let host, complete = makeHost ()

            let runtime =
                HostForkRuntime(
                    SessionId.create "orchestrator-with-blog",
                    host,
                    childWorkRecordFor = (fun _ -> Some "manager companion B")
                )

            let! forked =
                runtime.Fork("manager-blogged", AgentRole.Manager, "manage", agent = "fast-manager")

            Assert.Equal(Ok(ForkResult.Created "manager-blogged"), forked)

            complete ()
            let! joined = runtime.Join()
            let payload = completedPayload joined

            Assert.True(payload.WorkRecord.IsSome)
            Assert.Equal("manager companion B", payload.WorkRecord.Value.Text)
            Assert.Equal("manager session A", payload.FinalText)
        }
