namespace Wanxiangshu.Next.Tests.Session

open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Session

module ChildFlowTests =

    [<Fact>]
    let ``ChildFlow_while_with_None_progress_completes`` () =
        task {
            let mutable count = 0

            let script: ChildScript =
                { GetOrCreateSession =
                    fun _ ->
                        ChildFlows.child {
                            let session: ChildSession =
                                { SessionId = "child_while_test"
                                  Run = fun _ -> ChildFlows.child { return CompletedChild "done" }
                                  Close = fun () -> ChildFlows.child { return () } }

                            return session
                        } }

            let flow =
                ChildFlows.child {
                    while count < 2 do
                        count <- count + 1

                    return ()
                }

            let! res = Flow.run script CancellationToken.None flow

            match res with
            | Error e -> Assert.Fail(sprintf "Expected Ok, got Error: %A" e)
            | Ok() -> Assert.Equal(2, count)
        }

    [<Fact>]
    let ``ChildFlow_RunParallel_preserves_input_order`` () =
        task {
            let createScript () : ChildScript =
                { GetOrCreateSession =
                    fun req ->
                        ChildFlows.child {
                            let session: ChildSession =
                                { SessionId = req.Prompt
                                  Run =
                                    fun text ->
                                        Flow.create (fun _ _ ->
                                            task {
                                                if req.Prompt = "req2" then
                                                    do! Task.Delay(50)

                                                return Ok(CompletedChild(sprintf "result_%s" text))
                                            })
                                  Close = fun () -> ChildFlows.child { return () } }

                            return session
                        } }

            let requests = [ { Prompt = "req1" }; { Prompt = "req2" }; { Prompt = "req3" } ]

            let dummyScript = createScript ()
            let flow = ChildFlows.runParallel 3 createScript requests
            let! res = Flow.run dummyScript CancellationToken.None flow

            match res with
            | Error e -> Assert.Fail(sprintf "Expected Ok, got Error: %A" e)
            | Ok results ->
                Assert.Equal(3, List.length results)

                let expected =
                    [ CompletedChild "result_req1"
                      CompletedChild "result_req2"
                      CompletedChild "result_req3" ]

                Assert.Equal<ChildResult list>(expected, results)
        }

    [<Fact>]
    let ``ChildFlow_RunParallel_uses_independent_scripts`` () =
        task {
            let mutable scriptCreationCount = 0

            let createScript () : ChildScript =
                Interlocked.Increment(&scriptCreationCount) |> ignore
                let mutable runCount = 0

                { GetOrCreateSession =
                    fun req ->
                        ChildFlows.child {
                            runCount <- runCount + 1

                            let session: ChildSession =
                                { SessionId = req.Prompt
                                  Run =
                                    fun text ->
                                        ChildFlows.child { return CompletedChild(sprintf "%s_run%d" text runCount) }
                                  Close = fun () -> ChildFlows.child { return () } }

                            return session
                        } }

            let requests = [ { Prompt = "req1" }; { Prompt = "req2" }; { Prompt = "req3" } ]

            let dummyScript = createScript ()
            Assert.Equal(1, scriptCreationCount)

            let flow = ChildFlows.runParallel 3 createScript requests
            let! res = Flow.run dummyScript CancellationToken.None flow

            match res with
            | Error e -> Assert.Fail(sprintf "Expected Ok, got Error: %A" e)
            | Ok results ->
                Assert.Equal(4, scriptCreationCount)
                Assert.Equal(3, List.length results)

                let expected =
                    [ CompletedChild "req1_run1"
                      CompletedChild "req2_run1"
                      CompletedChild "req3_run1" ]

                Assert.Equal<ChildResult list>(expected, results)
        }

    [<Fact>]
    let ``ChildFlow_runChild_GetOrCreate_then_Run_returns_CompletedChild`` () =
        task {
            let script: ChildScript =
                { GetOrCreateSession =
                    fun req ->
                        ChildFlows.child {
                            let session: ChildSession =
                                { SessionId = "child_123"
                                  Run = fun text -> ChildFlows.child { return CompletedChild("done: " + text) }
                                  Close = fun () -> ChildFlows.child { return () } }

                            return session
                        } }

            let request = { Prompt = "hello child" }
            let flow = ChildFlows.runChild script request
            let! res = Flow.run script CancellationToken.None flow

            match res with
            | Error e -> Assert.Fail(sprintf "Expected Ok, got Error: %A" e)
            | Ok childRes -> Assert.Equal(CompletedChild "done: hello child", childRes)
        }

    [<Fact>]
    let ``Child_Close_does_not_fail_after_Run`` () =
        task {
            let mutable closed = false

            let script: ChildScript =
                { GetOrCreateSession =
                    fun req ->
                        ChildFlows.child {
                            let session: ChildSession =
                                { SessionId = "child_close_test"
                                  Run = fun text -> ChildFlows.child { return CompletedChild text }
                                  Close =
                                    fun () ->
                                        ChildFlows.child {
                                            closed <- true
                                            return ()
                                        } }

                            return session
                        } }

            let request = { Prompt = "run text" }

            let flow =
                ChildFlows.child {
                    let! session = script.GetOrCreateSession(request)
                    let! runRes = session.Run("run text")
                    do! session.Close()
                    return runRes
                }

            let! res = Flow.run script CancellationToken.None flow

            match res with
            | Error e -> Assert.Fail(sprintf "Expected Ok, got Error: %A" e)
            | Ok runRes ->
                Assert.Equal(CompletedChild "run text", runRes)
                Assert.True(closed)
        }
