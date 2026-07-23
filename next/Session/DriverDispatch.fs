namespace Wanxiangshu.Next.Session

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal

module DriverDispatch =

    let dispatchCommand (gateway: IGateway) (sessionId: SessionId) (cmd: SessionCommand) =
        match cmd with
        | UpsertTodo(snap, reply) ->
            let fact = Fact.Todo(Fact.TodoChanged {| Snapshot = snap |})
            let commitRes = gateway.Append (StreamId.Session sessionId) None fact

            match commitRes with
            | Committed _ -> reply (Ok SessionCommandResult.Upserted)
            | _ -> reply (Error SessionCommandError.InboxFull)
        | QuerySnapshot reply ->
            match Map.tryFind sessionId gateway.ProjectionSet.SessionProjections with
            | Some proj ->
                let todoSnap = defaultArg proj.Todos { Items = [] }
                reply todoSnap
            | None -> reply { Items = [] }
        | SubmitReview(reportText, reply) ->
            let fact =
                Fact.Review(
                    Fact.ReviewApplied
                        {| Verdict = Fact.ReviewVerdict.NeedsChanges [ reportText ]
                           Round = 1
                           ResultingTodo = None |}
                )

            let commitRes = gateway.Append (StreamId.Session sessionId) None fact

            match commitRes with
            | Committed _ -> reply (Ok SessionCommandResult.ReviewSubmitted)
            | _ -> reply (Error SessionCommandError.InboxFull)
        | ReturnVerdict(verdictText, reply) ->
            let verdict =
                if verdictText.Equals("Passed", StringComparison.OrdinalIgnoreCase) then
                    Fact.ReviewVerdict.Passed
                else
                    Fact.ReviewVerdict.NeedsChanges [ verdictText ]

            let fact =
                Fact.Review(
                    Fact.ReviewApplied
                        {| Verdict = verdict
                           Round = 1
                           ResultingTodo = None |}
                )

            let commitRes = gateway.Append (StreamId.Session sessionId) None fact

            match commitRes with
            | Committed _ -> reply (Ok SessionCommandResult.VerdictReturned)
            | _ -> reply (Error SessionCommandError.InboxFull)
