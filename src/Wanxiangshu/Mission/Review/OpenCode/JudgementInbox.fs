namespace Wanxiangshu.Mission.Review.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review

/// Process-local rendezvous between one active Finality review CE and JudgeTool.
/// Ownership and one pending TCS are physical resources only; first/second review
/// order lives exclusively in the CE that arms two one-shot judgement requests.
module ReviewJudgementInbox =

    let private gate = obj ()
    let private owners = HashSet<string>()

    let private waiters =
        Dictionary<string, TaskCompletionSource<Result<ReviewJudgementDelivery, string>>>()

    let private key (sessionId: SessionId) = SessionId.value sessionId

    let private release sessionKey =
        let pending =
            lock gate (fun () ->
                owners.Remove sessionKey |> ignore

                match waiters.TryGetValue sessionKey with
                | true, waiter ->
                    waiters.Remove sessionKey |> ignore
                    Some waiter
                | false, _ -> None)

        pending
        |> Option.iter (fun waiter ->
            AsyncSupport.trySetResult waiter (Error "review judgement channel closed")
            |> ignore)

    let private awaitJudgement sessionKey () =
        lock gate (fun () ->
            if not (owners.Contains sessionKey) then
                Task.FromResult(Error "review judgement channel is closed")
            elif waiters.ContainsKey sessionKey then
                Task.FromResult(Error "review judgement channel already has a pending await")
            else
                let waiter =
                    TaskCompletionSource<Result<ReviewJudgementDelivery, string>>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    )

                waiters.[sessionKey] <- waiter
                waiter.Task)

    let acquire (sessionId: SessionId) : Result<ReviewJudgementChannel, string> =
        let sessionKey = key sessionId
        let acquired = lock gate (fun () -> owners.Add sessionKey)

        if not acquired then
            Error "review judgement channel is already owned"
        else
            Ok
                { AwaitJudgement = awaitJudgement sessionKey
                  Dispose = fun () -> release sessionKey }

    let isOwned (sessionId: SessionId) =
        lock gate (fun () -> owners.Contains(key sessionId))

    let tryDeliver
        (judgement: ReviewJudgement)
        (accept: unit -> unit)
        (challenge: unit -> unit)
        (reject: unit -> unit)
        =
        let sessionKey = key judgement.ReviewerSessionId

        let waiter =
            lock gate (fun () ->
                match owners.Contains sessionKey, waiters.TryGetValue sessionKey with
                | true, (true, pending) ->
                    waiters.Remove sessionKey |> ignore
                    Some pending
                | _ -> None)

        match waiter with
        | None -> None
        | Some pending ->
            AsyncSupport.trySetResult
                pending
                (Ok
                    { Judgement = judgement
                      Accept = accept
                      Challenge = challenge
                      Reject = reject })
            |> ignore

            Some()
