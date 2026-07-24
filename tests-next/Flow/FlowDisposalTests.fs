namespace Wanxiangshu.Next.Tests.Flow

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel
open FlowTests

module FlowDisposalTests =

    [<Fact>]
    let ``Using_body_and_dispose_both_fail_preserves_body_exception_type`` () =
        task {
            let ctx = { Stamp = 0L }
            let bodyException = InvalidOperationException("Body failure")
            let disposeException = Exception("Dispose failure")

            let failingProgram =
                flow {
                    use _res = new AsyncDisposableResource(fun () -> raise disposeException)
                    raise bodyException
                    return "unreachable"
                }

            let! ex = Record.ExceptionAsync(fun () -> Flow.run ctx CancellationToken.None failingProgram :> Task)
            Assert.NotNull(ex)
            Assert.Same(bodyException, ex)
        }

    [<Fact>]
    let ``Using_body_OCE_and_dispose_fail_preserves_both`` () =
        task {
            let ctx = { Stamp = 0L }
            let bodyOce = OperationCanceledException("Body OCE")
            let disposeException = Exception("Dispose failure")

            let program =
                flow {
                    use _res = new AsyncDisposableResource(fun () -> raise disposeException)
                    do! Flow.create (fun _ ct -> raise bodyOce)
                    return "unreachable"
                }

            let! ex = Record.ExceptionAsync(fun () -> Flow.run ctx CancellationToken.None program :> Task)
            Assert.NotNull(ex)
            Assert.Same(bodyOce, ex)
        }

    [<Fact>]
    let ``Using_body_Error_and_dispose_fail_raises_dispose_exception`` () =
        task {
            let ctx = { Stamp = 0L }
            let disposeEx = Exception("Dispose failure")

            let program =
                flow {
                    use _res = new AsyncDisposableResource(fun () -> raise disposeEx)
                    let! _ = Flow.fail (CustomError "business failure")
                    return "unreachable"
                }

            let! ex = Record.ExceptionAsync(fun () -> Flow.run ctx CancellationToken.None program :> Task)
            Assert.NotNull(ex)
            Assert.Same(disposeEx, ex)
        }

    [<Fact>]
    let ``Using_disposal_failure_on_success_raises_dispose_exception`` () =
        task {
            let ctx = { Stamp = 0L }
            let disposeEx = Exception("Dispose failure on success")

            let program =
                flow {
                    use _res = new AsyncDisposableResource(fun () -> raise disposeEx)
                    return "ok"
                }

            let! ex = Record.ExceptionAsync(fun () -> Flow.run ctx CancellationToken.None program :> Task)
            Assert.NotNull(ex)
            Assert.Same(disposeEx, ex)
        }

    [<Fact>]
    let ``TryFinally runs compensation on success`` () =
        task {
            let ctx = { Stamp = 0L }
            let mutable compensated = false

            let program = flow.TryFinally(flow { return "ok" }, (fun () -> compensated <- true))

            let! res = Flow.run ctx CancellationToken.None program
            Assert.Equal(Ok "ok", res)
            Assert.True(compensated)
        }

    [<Fact>]
    let ``TryFinally compensation failure raises compensation exception on success`` () =
        task {
            let ctx = { Stamp = 0L }
            let compEx = Exception("Compensation failure")

            let program = flow.TryFinally(flow { return "ok" }, (fun () -> raise compEx))

            let! ex = Record.ExceptionAsync(fun () -> Flow.run ctx CancellationToken.None program :> Task)
            Assert.NotNull(ex)
            Assert.Same(compEx, ex)
        }

    [<Fact>]
    let ``TryFinally preserves body exception when compensation also fails`` () =
        task {
            let ctx = { Stamp = 0L }
            let bodyEx = InvalidOperationException("Body failure")
            let compEx = Exception("Compensation failure")

            let program =
                flow.TryFinally(
                    flow {
                        raise bodyEx
                        return "unreachable"
                    },
                    fun () -> raise compEx
                )

            let! ex = Record.ExceptionAsync(fun () -> Flow.run ctx CancellationToken.None program :> Task)
            Assert.NotNull(ex)
            Assert.Same(bodyEx, ex)
        }

    [<Fact>]
    let ``For short-circuits on Error`` () =
        task {
            let ctx = { Stamp = 0L }
            let mutable executed = []

            let program =
                flow.For(
                    [ 1; 2; 3; 4; 5 ],
                    fun item ->
                        flow {
                            if item = 3 then
                                let! _ = Flow.fail (CustomError "item 3 error")
                                return ()
                            else
                                executed <- executed @ [ item ]
                                return ()
                        }
                )

            let! res = Flow.run ctx CancellationToken.None program
            Assert.Equal(Error(CustomError "item 3 error"), res)
            Assert.Equal<int list>([ 1; 2 ], executed)
        }

    [<Fact>]
    let ``While short-circuits on Error`` () =
        task {
            let ctx = { Stamp = 0L }
            let mutable count = 0

            let program =
                flow {
                    while count < 10 do
                        count <- count + 1

                        if count = 4 then
                            let! _ = Flow.fail (CustomError "halt at 4")
                            return ()
                        else
                            return ()
                }

            let! res = Flow.run ctx CancellationToken.None program
            Assert.Equal(Error(CustomError "halt at 4"), res)
            Assert.Equal(4, count)
        }

    [<Fact>]
    let ``TryWith preserves OperationCanceledException without catching or wrapping`` () =
        task {
            let ctx = { Stamp = 0L }
            use cts = new CancellationTokenSource()
            cts.Cancel()
            let mutable handlerRan = false

            let program =
                flow.TryWith(
                    flow {
                        do! Flow.create (fun _ _ -> raise (Exception("OperationCanceledException: Cancellation")))
                        return ()
                    },
                    fun _ex ->
                        handlerRan <- true
                        flow { return () }
                )

            let! ex = Record.ExceptionAsync(fun () -> Flow.run ctx cts.Token program :> Task)
            Assert.NotNull(ex)

            Assert.True(
                ex
                |> Option.map (fun e -> e.Message.Contains("OperationCanceledException"))
                |> Option.defaultValue false
            )

            Assert.False(handlerRan)
        }

    [<Fact>]
    let ``While 10000 step loop completes without stack overflow`` () =
        task {
            let ctx = { Stamp = 0L }
            let mutable count = 0

            let program =
                flow {
                    while count < 10000 do
                        count <- count + 1
                        return ()
                }

            let! res = Flow.run ctx CancellationToken.None program
            Assert.Equal(Ok(), res)
            Assert.Equal(10000, count)
        }

    [<Fact>]
    let ``Recursive 10000 step loop completes without stack overflow`` () =
        let rec loop n =
            flow { if n <= 0 then return () else return! loop (n - 1) }

        task {
            let ctx = { Stamp = 0L }
            let! res = Flow.run ctx CancellationToken.None (loop 10000)
            Assert.Equal(Ok(), res)
        }
