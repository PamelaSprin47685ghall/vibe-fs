namespace Wanxiangshu.Next.Tests.ProcessTests

open System
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

module PtyOwnershipTests =

    let private ok =
        function
        | Ok value -> value
        | Error error -> failwithf "Unexpected error: %A" error

    let private hostPort (childId: SessionId) =
        { new ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(_, _, _) = Task.FromResult(Ok(MessageId.create "accepted"))
            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task
            member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
            member _.GetSessionOutput(_) = [] }

    [<Fact>]
    let ``HostForkRuntime_shared_pty_port_routes_completion_only_to_owner`` () =
        task {
            let port = PtyPort(handler = (fun _ _ -> Task.FromResult(Ok())))

            let owner =
                HostForkRuntime(
                    SessionId.create "pty-owner",
                    hostPort (SessionId.create "pty-owner-child"),
                    ptyPort = port
                )

            let other =
                HostForkRuntime(
                    SessionId.create "pty-other",
                    hostPort (SessionId.create "pty-other-child"),
                    ptyPort = port
                )

            let! created = owner.ForkPty("cat")
            let id = ok created
            Assert.True((other.TryPty id.Value).IsNone, "other runtime must not resolve an owned PTY handle")

            port.Complete(id, outcome = Ok "owner output")

            let! ownerJoin = owner.Join()
            Assert.Equal(id.Value, (ok ownerJoin).RunId)

            let! otherJoin = other.Join()

            match otherJoin with
            | Error ForkError.NothingToJoin -> ()
            | other -> failwithf "foreign PTY completion leaked into join: %A" other
        }
