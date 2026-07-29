namespace Wanxiangshu.Next.Tests.MockOpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session

module ForkRuntimeIntegration =

    let private equal expected actual =
        if not (Unchecked.equals expected actual) then
            failwithf "Expected %A, got %A" expected actual

    let private trueThat condition message =
        if not condition then failwith message

    /// Fork with runner that completes via Task.FromResult (no Task.Delay).
    let ``Fork creates agent and join receives completion`` () =
        task {
            let mutable completed = None
            let runner (_: string) (_: AgentRole) (_: string option) =
                Task.FromResult(AgentCompletion.ofSimpleText "agent-1" "run-1" AgentRole.Coder "task done")
            let runtime = ForkRuntime(runner = runner, listener = (fun c -> completed <- Some c))
            let result = runtime.Fork("agent-1", AgentRole.Coder, prompt = "implement feature")
            trueThat (match result with ForkResult.Created _ -> true | _ -> false) "Fork must return Created"
            let! joinResult = runtime.Join()
            match joinResult with
            | Ok c ->
                equal "agent-1" c.AgentId; equal AgentRole.Coder c.Role; equal "task done" (AgentCompletion.text c.Outcome)
            | Error err -> failwithf "Expected Ok completion, got %A" err
            match completed with Some c -> equal "agent-1" c.AgentId | None -> failwith "Listener not called"
        }

    /// Fork into a busy agent. Use a never-completing TCS to keep agent busy.
    let ``Fork busy agent returns Nudged`` () =
        task {
            let mutable count = 0
            let neverComplete = TaskCompletionSource<AgentCompletionOutcome>()
            let runner (_: string) (_: AgentRole) (_: string option) = neverComplete.Task
            let runtime = ForkRuntime(runner = runner, listener = (fun _ -> count <- count + 1))
            let r1 = runtime.Fork("a", AgentRole.Coder, prompt = "first")
            trueThat (match r1 with ForkResult.Created _ -> true | _ -> false) "First fork must be Created"

            // Second fork while first is busy -> Nudged
            let r2 = runtime.Fork("a", AgentRole.Reviewer, prompt = "second")
            trueThat (match r2 with ForkResult.Nudged _ -> true | _ -> false) "Busy fork must be Nudged"

            // Complete the first run
            neverComplete.SetResult(AgentCompletion.ofSimpleText "a" "run-a" AgentRole.Coder "done")
            let! j = runtime.Join()
            match j with
            | Ok c -> equal "a" c.AgentId; equal AgentRole.Coder c.Role; equal "done" (AgentCompletion.text c.Outcome)
            | Error e -> failwithf "Expected Ok, got %A" e
            equal 1 count
        }

    /// Join with no active agents returns NothingToJoin.
    let ``Join with no active agents returns NothingToJoin`` () =
        task {
            let runtime = ForkRuntime()
            let! result = runtime.Join()
            trueThat (match result with Error ForkError.NothingToJoin -> true | _ -> false) "Expected NothingToJoin"
        }

    /// Fork with a custom work function that succeeds.
    let ``Fork with work function delivers outcome`` () =
        task {
            let runtime = ForkRuntime()
            let work () : Task<AgentCompletionOutcome> = Task.FromResult(AgentCompletion.ofSimpleText "w" "run-w" AgentRole.Coder "computed")
            runtime.Fork("w", AgentRole.Coder, runWork = work) |> ignore
            let! j = runtime.Join()
            match j with Ok c -> equal "computed" (AgentCompletion.text c.Outcome) | Error e -> failwithf "Expected Ok, got %A" e
        }

    /// Fork with a failing work function returns error outcome.
    let ``Fork with failing work function delivers error outcome`` () =
        task {
            let runtime = ForkRuntime()
            let work () : Task<AgentCompletionOutcome> = Task.FromResult(AgentCompletion.ofSimpleError "f" "run-f" AgentRole.Coder "failed")
            runtime.Fork("f", AgentRole.Coder, runWork = work) |> ignore
            let! j = runtime.Join()
            match j with Ok c -> equal "failed" (AgentCompletion.text c.Outcome) | Error e -> failwithf "Expected Ok, got %A" e
        }

    let ``Nested sessions use the family root as OpenCode parent`` () =
        task {
            let state, _, sessionPort = MockOpenCode.createHost ()
            let rootId = SessionId.create "root-session"

            let options: OpenCodeChildOptions =
                { Title = None
                  Agent = None
                  Directory = None }

            let! childResult = sessionPort.CreateChildSession(rootId, options)

            let childId =
                match childResult with
                | Ok value -> value
                | Error error -> failwith error

            let! grandchildResult = sessionPort.CreateChildSession(childId, options)

            let grandchildId =
                match grandchildResult with
                | Ok value -> value
                | Error error -> failwith error

            equal rootId state.ParentChild.[childId]
            equal rootId state.ParentChild.[grandchildId]
        }

    /// Cancel a runtime that has a busy agent and pending join waiter.
    let ``Cancel runtime delivers Cancelled to pending joiners`` () =
        task {
            let runtime = ForkRuntime()
            let never = TaskCompletionSource<AgentCompletionOutcome>()
            runtime.Fork("a1", AgentRole.Coder, runWork = (fun () -> never.Task)) |> ignore
            let jt = runtime.Join()
            runtime.Cancel()
            let! r = jt
            trueThat (match r with Error ForkError.Cancelled -> true | _ -> false) "Expected Cancelled"
        }