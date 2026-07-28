namespace Wanxiangshu.Next.Tests.SessionTests

open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Session

module ForkRuntimeJoinTests =

    [<Fact>]
    let ``Join_with_no_active_item_returns_NothingToJoin`` () =
        task {
            let runtime = ForkRuntime()
            let! result = runtime.Join()

            match result with
            | Error ForkError.NothingToJoin -> ()
            | other -> Assert.True(false, sprintf "expected NothingToJoin, got %A" other)
        }

    [<Fact>]
    let ``Cancel_completes_pending_join_waiters`` () =
        task {
            let runtime = ForkRuntime()
            // Park a busy agent with a never-completing work item so Join waits.
            let never = TaskCompletionSource<AgentCompletionOutcome>()
            let _ =
                runtime.Fork(
                    "a1",
                    AgentRole.Coder,
                    runWork = (fun () -> never.Task)
                )

            let joinTask = runtime.Join()
            runtime.Cancel()
            let! result = joinTask

            match result with
            | Error ForkError.Cancelled -> ()
            | other -> Assert.True(false, sprintf "expected Cancelled, got %A" other)

            let! after = runtime.Join()

            match after with
            | Error ForkError.Cancelled
            | Error ForkError.NothingToJoin -> ()
            | other -> Assert.True(false, sprintf "post-cancel unexpected %A" other)
        }
