namespace Wanxiangshu.Mission.Review.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Change
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

module ReviewHostSurface =

    let private outcomeView outcome : obj =
        match outcome with
        | HostReviewGuard.GuardNudgeOutcome.Sent promptKey ->
            box
                {| outcome = "Sent"
                   promptKey = PromptKey.value promptKey |}
        | HostReviewGuard.GuardNudgeOutcome.AlreadyOutstanding -> box {| outcome = "AlreadyOutstanding" |}
        | HostReviewGuard.GuardNudgeOutcome.NoLongerRequired -> box {| outcome = "NoLongerRequired" |}
        | HostReviewGuard.GuardNudgeOutcome.Failed reason ->
            box
                {| outcome = "Failed"
                   reason = reason |}

    type private PlainSessionPort(raw: obj, typed: ISessionHostPort) =
        let sendPrompt = raw?``SendPrompt``

        interface ISessionHostPort with
            member _.SubscribeTerminal(sessionId, listener) =
                typed.SubscribeTerminal(sessionId, listener)

            member _.SubscribeFutureTerminal(sessionId, listener) =
                typed.SubscribeFutureTerminal(sessionId, listener)

            member _.SendPrompt(sessionId, text, options) =
                emitJsExpr (sendPrompt, SessionId.value sessionId, text, options) "$0($1,$2,$3)"

            member _.AbortSession(sessionId) = typed.AbortSession sessionId
            member _.InterruptAttempt(sessionId) = typed.InterruptAttempt sessionId
            member _.IsManagedChild(sessionId) = typed.IsManagedChild sessionId
            member _.AbortChildren(sessionId) = typed.AbortChildren sessionId

            member _.CreateSiblingSession(owner, parent, options) =
                typed.CreateSiblingSession(owner, parent, options)

            member _.TryGetParentSession(sessionId) = typed.TryGetParentSession sessionId

            member _.CreateChildSession(parent, options) =
                typed.CreateChildSession(parent, options)

            member _.ListChildren(parent) = typed.ListChildren parent
            member _.FamilyRootOf(sessionId) = typed.FamilyRootOf sessionId

    let private sessionPort (port: obj) : ISessionHostPort =
        let typed = unbox<ISessionHostPort> port
        PlainSessionPort(port, typed) :> ISessionHostPort

    let admittedWithReceipt (value: string) : Outcome.SendOutcome =
        Outcome.SendOutcome.AdmittedWithReceipt(TransportReceipt.create value)

    let admittedWithPhysicalMessage (value: string) : Outcome.SendOutcome =
        Outcome.SendOutcome.AdmittedWithPhysicalMessage(PhysicalUserMessageId.create value)

    let sessionId (value: string) = SessionId.create value
    let reviewBarrierId (value: string) = ReviewBarrierId.create value
    let worktreePath (value: string) = WorktreePath.create value

    let setSessionDirectory (sessionId: string) (directory: string) =
        SharedState.SessionDirectories.[sessionId] <- directory

    let clearSessionDirectory (sessionId: string) =
        SharedState.SessionDirectories.Remove sessionId |> ignore

    let clearGuardNudges () =
        SharedState.clearReviewGuardNudgesForTests ()

    let reverify
        (manager: obj)
        (jobId: string)
        (managerSession: string)
        (worktree: string)
        (barrier: string)
        : Task<obj> =
        task {
            let port = unbox<ManagerPort> manager

            let! result =
                port.Reverify
                    (ManagerJobId.create jobId)
                    (SessionId.create managerSession)
                    (WorktreePath.create worktree)
                    (ReviewBarrierId.create barrier)

            return
                match result with
                | Ok() -> box {| ok = true |}
                | Error error -> box {| ok = false; error = error |}
        }

    let openBarrier
        (handle: JournalHandle)
        (managerSession: string)
        (reviewerSession: string)
        (barrier: string)
        (tree: string)
        : Task<obj> =
        task {
            let! result =
                ReviewBarrier.openBarrier
                    (Some handle.Journal)
                    (SessionId.create managerSession)
                    (SessionId.create reviewerSession)
                    (ReviewBarrierId.create barrier)
                    (GitTreeHash.create tree)

            return
                match result with
                | Ok() -> box {| ok = true |}
                | Error error -> box {| ok = false; error = error |}
        }

    let nudgeReviewer
        (port: obj)
        (handle: JournalHandle option)
        (reviewerSession: string)
        (terminalProviderRun: string)
        : Task<obj> =
        task {
            let journal = handle |> Option.map (fun value -> value.Journal)

            let! outcome =
                HostReviewGuard.nudgeReviewer
                    (sessionPort port)
                    journal
                    (SessionId.create reviewerSession)
                    (ProviderRunIdentity.create terminalProviderRun)

            return outcomeView outcome
        }

    let deliverJudgement
        (reviewerSession: string)
        (physicalUserMessage: string)
        (providerRun: string)
        (toolCall: string)
        (verdictText: string)
        : Task<obj> option =
        match StaticTools.reviewerVerdictOfString verdictText with
        | Error error -> Some(Task.FromResult(box {| ok = false; error = error |}))
        | Ok verdict ->
            let judgement: ReviewJudgement =
                { ReviewerSessionId = SessionId.create reviewerSession
                  PhysicalUserMessageId = PhysicalUserMessageId.create physicalUserMessage
                  ProviderRun = ProviderRunIdentity.create providerRun
                  ToolCallId = ToolCallId.create toolCall
                  Verdict = verdict }

            let tcs =
                TaskCompletionSource<obj>(TaskCreationOptions.RunContinuationsAsynchronously)

            let accept () =
                AsyncSupport.trySetResult tcs (box {| ok = true; effect = "Accepted" |})
                |> ignore

            let challenge () =
                AsyncSupport.trySetResult tcs (box {| ok = true; effect = "Challenge" |})
                |> ignore

            let reject () =
                AsyncSupport.trySetResult tcs (box {| ok = false; effect = "Rejected" |})
                |> ignore

            match ReviewJudgementInbox.tryDeliver judgement accept challenge reject with
            | None -> None
            | Some() -> Some tcs.Task
