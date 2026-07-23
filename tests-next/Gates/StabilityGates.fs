namespace Wanxiangshu.Next.Tests.Gates

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools
open Wanxiangshu.Next.Tests
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module private StabilitySupport =
    type SimplePromptPort(msgId: MessageId) =
        interface IPromptPort with
            member _.SendPrompt (_sessionId: SessionId) (_text: string) (_opts: PromptOptions) =
                Task.FromResult(SendOutcome.Delivered msgId)

module StabilityGates =

    [<Fact>]
    let ``Stability_100_init_dispose_cycles`` () =
        withTempDir (fun tempDir ->
            task {
                for i in 1 .. 100 do
                    Assert.True(true)
                    let subDir = sprintf "%s/cycle_%d" tempDir i
                    let! startRes = Gateway.start subDir CancellationToken.None
                    match startRes with
                    | Error err -> Assert.True(false, sprintf "Cycle %d failed to start: %A" i err)
                    | Ok gateway ->
                        let sessionId = SessionId.create (sprintf "sess-cycle-%d" i)
                        let inbox = FifoInbox(100) :> ISessionInbox
                        let driver = new SessionDriver(gateway, sessionId, inbox)
                        let fact = Fact.Session(HumanTurnStarted {| TurnId = TurnId.create (sprintf "turn-%d" i) |})
                        let res = gateway.Append (StreamId.Session sessionId) None fact
                        match res with
                        | Committed _ -> ()
                        | _ -> Assert.True(false, sprintf "Cycle %d commit failed" i)
                        (driver :> IDisposable).Dispose()
                        let! _ = gateway.DisposeAsync()
                        ()
            })

    [<Fact>]
    let ``Stability_multi_session_concurrency`` () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None
                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sessionCount = 10
                    let tasks =
                        Array.init sessionCount (fun idx ->
                            let i = idx + 1
                            task {
                                let sIdStr = sprintf "sess-conc-%d" i
                                let sId = SessionId.create sIdStr
                                let inbox = FifoInbox(100) :> ISessionInbox
                                use driver = new SessionDriver(gateway, sId, inbox)
                                let turnId = TurnId.create (sprintf "turn-conc-%d" i)
                                let fact = Fact.Session(HumanTurnStarted {| TurnId = turnId |})
                                match gateway.Append (StreamId.Session sId) None fact with
                                | Committed _ -> ()
                                | _ -> Assert.True(false, sprintf "Session %s commit failed" sIdStr)

                                let todoFact = Fact.Todo(TodoChanged {| Snapshot = { Items = [ sprintf "task-%d" i ] } |})
                                match gateway.Append (StreamId.Session sId) None todoFact with
                                | Committed _ -> ()
                                | _ -> Assert.True(false, sprintf "Session %s todo commit failed" sIdStr)
                            })
                    do! Task.WhenAll(tasks)
                    let projs = gateway.ProjectionSet.SessionProjections
                    Assert.Equal(sessionCount, Map.count projs)
                    for i in 1 .. sessionCount do
                        let sId = SessionId.create (sprintf "sess-conc-%d" i)
                        let projOpt = Map.tryFind sId projs
                        Assert.True(projOpt.IsSome, sprintf "Projection missing for session %d" i)
                        Assert.True(projOpt.Value.Todos.IsSome)
                        Assert.Equal(1, projOpt.Value.Todos.Value.Items.Length)
                    let! _ = gateway.DisposeAsync()
                    ()
            })

    [<Fact>]
    let ``Stability_long_todo_chains`` () =
        withTempDir (fun tempDir ->
            task {
                let! startRes = Gateway.start tempDir CancellationToken.None
                match startRes with
                | Error err -> Assert.True(false, sprintf "Gateway start failed: %A" err)
                | Ok gateway ->
                    let sId = SessionId.create "sess-long-todo"
                    let inbox = FifoInbox(1000) :> ISessionInbox
                    use driver = new SessionDriver(gateway, sId, inbox)
                    let chainLength = 20
                    for i in 1 .. chainLength do
                        Assert.True(true)
                        let items = [ for j in 1 .. i -> sprintf "todo item %d" j ]
                        let todoFact = Fact.Todo(TodoChanged {| Snapshot = { Items = items } |})
                        match gateway.Append (StreamId.Session sId) None todoFact with
                        | Committed _ -> ()
                        | _ -> Assert.True(false, sprintf "Step %d commit failed" i)
                        do! EventDrivenHarness.yieldMicrotask ()
                    let projs = gateway.ProjectionSet.SessionProjections
                    let proj = Map.find sId projs
                    Assert.True(proj.Todos.IsSome)
                    Assert.Equal(chainLength, proj.Todos.Value.Items.Length)
                    let! _ = gateway.DisposeAsync()
                    ()
            })
