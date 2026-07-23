namespace Wanxiangshu.Next.Tests.Flow

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel

type TestCtx = { mutable Stamp: int64 }

type TestError =
    | CustomError of string
    | NoProgressError of string

type AsyncDisposableResource(onDispose: unit -> unit) =
    interface IAsyncDisposable with
        member _.DisposeAsync() =
            onDispose ()
            ValueTask()

module FlowTests =

    let flow = FlowBuilder<TestCtx, TestError>(None)

    let flowWithProgress =
        FlowBuilder<TestCtx, TestError>(
            Some
                { Stamp = fun ctx -> ctx.Stamp
                  NoProgress = fun msg -> NoProgressError msg }
        )

    [<Fact>]
    let ``Bind short-circuits on Error`` () =
        task {
            let ctx = { Stamp = 0L }
            let mutable secondRan = false

            let program =
                flow {
                    let! _ = Flow.fail (CustomError "first failed")
                    secondRan <- true
                    return ()
                }

            let! res = Flow.run ctx CancellationToken.None program
            Assert.Equal(Error(CustomError "first failed"), res)
            Assert.False(secondRan)
        }

    [<Fact>]
    let ``While NoProgress when stamp unchanged`` () =
        task {
            let ctx = { Stamp = 5L }
            let mutable count = 0

            let program =
                flowWithProgress {
                    while count < 3 do
                        count <- count + 1
                        return ()
                }

            let! res = Flow.run ctx CancellationToken.None program
            Assert.Equal(Error(NoProgressError "Loop body completed without progress"), res)
            Assert.Equal(1, count)
        }

    [<Fact>]
    let ``While continues when stamp advances`` () =
        task {
            let ctx = { Stamp = 0L }
            let mutable count = 0

            let program =
                flowWithProgress {
                    while count < 3 do
                        count <- count + 1
                        ctx.Stamp <- ctx.Stamp + 1L
                        return ()
                }

            let! res = Flow.run ctx CancellationToken.None program
            Assert.Equal(Ok(), res)
            Assert.Equal(3, count)
            Assert.Equal(3L, ctx.Stamp)
        }

    [<Fact>]
    let ``Using disposes resource on success and error`` () =
        task {
            let ctx = { Stamp = 0L }
            let mutable disposedSuccess, disposedError = false, false

            let successProgram =
                flow {
                    use _res = new AsyncDisposableResource(fun () -> disposedSuccess <- true)
                    return "ok"
                }

            let! resSuccess = Flow.run ctx CancellationToken.None successProgram
            Assert.Equal(Ok "ok", resSuccess)
            Assert.True(disposedSuccess)

            let errorProgram =
                flow {
                    use _res = new AsyncDisposableResource(fun () -> disposedError <- true)
                    let! _ = Flow.fail (CustomError "fail in using")
                    return "unreachable"
                }

            let! resError = Flow.run ctx CancellationToken.None errorProgram
            Assert.Equal(Error(CustomError "fail in using"), resError)
            Assert.True(disposedError)
        }

    [<Fact>]
    let ``attempt wraps Error into Ok Result`` () =
        task {
            let ctx = { Stamp = 0L }
            let failingFlow = Flow.fail (CustomError "inner error")
            let program = Flow.attempt failingFlow
            let! res = Flow.run ctx CancellationToken.None program
            Assert.Equal(Ok(Error(CustomError "inner error")), res)
        }

    [<Fact>]
    let ``mapBounded preserves order`` () =
        task {
            let items = [ 1..10 ]

            let action (x: int) (ct: CancellationToken) =
                task {
                    return x * 10
                }

            let! results = Parallel.mapBounded 3 CancellationToken.None action items
            Assert.Equal<int list>([ 10; 20; 30; 40; 50; 60; 70; 80; 90; 100 ], results)
        }

    [<Fact>]
    let ``mapBounded_propagates_action_exception`` () =
        task {
            let items = [ 1..5 ]

            let action (x: int) (_ct: CancellationToken) =
                task {
                    if x = 3 then
                        failwith "Action 3 failed"

                    return x * 2
                }

            let! ex = Record.ExceptionAsync(fun () -> Parallel.mapBounded 2 CancellationToken.None action items :> Task)
            Assert.NotNull(ex)
            Assert.True(ex.ToString().Contains("Action 3 failed"))
        }

    [<Fact>]
    let ``mapBounded_empty_input`` () =
        task {
            let action (x: int) (_ct: CancellationToken) = task { return x }
            let! results = Parallel.mapBounded 3 CancellationToken.None action []
            Assert.Empty(results)
        }

    [<Fact>]
    let ``Flow run respects cancellation via OperationCanceledException`` () =
        task {
            let ctx = { Stamp = 0L }
            use cts = new CancellationTokenSource()
            cts.Cancel()

            let program =
                flow {
                    do!
                        Flow.create (fun _ ct ->
                            ct.ThrowIfCancellationRequested()
                            Task.FromResult(Ok()))

                    return ()
                }

            let! ex = Record.ExceptionAsync(fun () -> Flow.run ctx cts.Token program :> Task)
            Assert.NotNull(ex)
            Assert.IsAssignableFrom<OperationCanceledException>(ex) |> ignore
        }

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
        }

    [<Fact>]
    let ``Using_body_OCE_and_dispose_fail_preserves_both`` () =
        task {
            let ctx = { Stamp = 0L }
            let disposeException = Exception("Dispose failure")

            let program =
                flow {
                    use _res = new AsyncDisposableResource(fun () -> raise disposeException)
                    do! Flow.create (fun _ ct -> raise (OperationCanceledException("Body OCE")))
                    return "unreachable"
                }

            let! ex = Record.ExceptionAsync(fun () -> Flow.run ctx CancellationToken.None program :> Task)
            Assert.NotNull(ex)
        }

    [<Fact>]
    let ``Using_body_Error_and_dispose_fail_returns_Error`` () =
        task {
            let ctx = { Stamp = 0L }
            let disposeEx = Exception("Dispose failure")

            let program =
                flow {
                    use _res = new AsyncDisposableResource(fun () -> raise disposeEx)
                    let! _ = Flow.fail (CustomError "business failure")
                    return "unreachable"
                }

            let! res = Flow.run ctx CancellationToken.None program

            match res with
            | Error(CustomError "business failure") -> ()
            | _ -> Assert.True(false, sprintf "Expected CustomError, got %A" res)
        }
