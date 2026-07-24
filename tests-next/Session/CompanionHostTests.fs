namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Threading.Tasks
open Xunit
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools

module CompanionHostTests =

    let private makeFake () =
        let mutable terminal: (SessionId -> TerminalOutcome -> unit) option = None
        let mutable childCount = 0
        let childId = SessionId.create "blogger-1"

        let host =
            { new ISessionHostPort with
                member _.SubscribeTerminal(_, listener) =
                    terminal <- Some listener

                    { new IDisposable with
                        member _.Dispose() = terminal <- None }

                member _.SendPrompt(_, _, _) =
                    terminal
                    |> Option.iter (fun listener -> listener childId (Completed(MessageId.create "blog")))

                    Task.FromResult(Ok(MessageId.create "accepted"))

                member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
                member _.AbortSession(_) = Task.FromResult(Ok())

                member _.CreateChildSession(_, _) =
                    childCount <- childCount + 1
                    Task.FromResult(Ok childId)

                member _.GetSessionOutput(_) = [ "blog paragraph" ] }

        host, (fun () -> childCount)

    [<Fact>]
    let ``CompanionHost_updates_B_and_reuses_blogger`` () =
        task {
            let host, childCount = makeFake ()
            let companion = CompanionHost(SessionId.create "primary", host)

            Assert.Equal(Submitted, companion.SubmitProjection("{\"step\":1}"))
            do! companion.WaitInFlightAsync()
            Assert.Equal(Some "blog paragraph", companion.Memory.CurrentB)

            Assert.Equal(Submitted, companion.SubmitProjection("{\"step\":2}"))
            do! companion.WaitInFlightAsync()
            Assert.Equal(1, childCount ())

            let messages =
                [ { Role = "user"
                    Text = "old"
                    ToolCalls = None
                    Metadata = None }
                  { Role = "user"
                    Text = "tail"
                    ToolCalls = None
                    Metadata = None } ]

            let replaced = companion.ReplacePrefix(messages, 0)
            Assert.Equal("blog paragraph", replaced.Head.Text)

            let host2, _ = makeFake ()
            let companion2 = CompanionHost(SessionId.create "raw-primary", host2)
            let first = [ createObj [ "role", box "user"; "text", box "old" ] ]
            let second = first @ [ createObj [ "role", box "user"; "text", box "tail" ] ]
            companion2.TransformRaw first |> ignore
            do! companion2.WaitInFlightAsync()
            let projected = companion2.TransformRaw second
            Assert.Equal(2, projected.Length)
            Assert.Equal("blog paragraph", (projected.Head: obj)?text)
            Assert.Equal("tail", (projected.[1]: obj)?text)
        }
