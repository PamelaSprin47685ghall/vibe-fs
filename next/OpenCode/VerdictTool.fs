namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools

/// Reviewer verdict tool. ToolCallId, ProviderRunId, physical user id, and tree
/// are passed as explicit witnesses; assistant prose never determines verdict.
module VerdictTool =

    let private textFromParts (parts: MessagePart array) = CompletedTurnClassifier.partsText parts

    let private latestUserPromptText
        (snapshot: ISessionSnapshotPort option)
        (sessionId: string)
        (preferredMessageId: string option)
        =
        task {
            match snapshot with
            | None -> return None
            | Some port ->
                match! port.GetMessages(SessionId.create sessionId) with
                | Error _ -> return None
                | Ok messages ->
                    let users: SessionMessage list =
                        messages |> List.filter (fun message -> message.Role = "user")

                    let preferred =
                        preferredMessageId
                        |> Option.bind (fun messageId ->
                            users
                            |> List.tryFind (fun message -> MessageId.value message.Id = messageId)
                            |> Option.bind (fun message ->
                                let text = textFromParts message.Parts
                                if String.IsNullOrWhiteSpace text then None else Some text))

                    return
                        preferred
                        |> Option.orElseWith (fun () ->
                            users
                            |> List.rev
                            |> List.tryPick (fun message ->
                                let text = textFromParts message.Parts
                                if String.IsNullOrWhiteSpace text then None else Some text))
        }

    let private reviewOwner (scope: ToolRuntimeScope) (reviewerId: string) =
        match scope.SessionParents.TryGetValue reviewerId with
        | true, parentId -> Some parentId
        | false, _ ->
            match scope.Journal with
            | None -> None
            | Some journal ->
                let child = ChildId.create reviewerId

                (AgentJournal.snapshot journal).AgentProjections.Sessions
                |> Map.tryPick (fun parentId session ->
                    match session.Linkage with
                    | Some linkage when Map.containsKey child linkage.LinkedChildren -> Some(SessionId.value parentId)
                    | _ -> None)

    let private execute
        (scope: ToolRuntimeScope)
        (args: HostToolArguments)
        (context: HostToolContext)
        =
        task {
            let verdict = StaticTools.reviewerVerdictOfString(args.Text "verdict")

            let validation =
                if scope.RoleFor context <> Some Role.Reviewer then
                    Error "The verdict tool is available only to reviewer sessions"
                elif String.IsNullOrWhiteSpace context.SessionId then
                    Error "Missing sessionID"
                elif context.ToolCallId.IsNone then
                    Error "Missing tool call id"
                else
                    verdict

            match validation, context.ToolCallId with
            | Error error, _ -> return sprintf "Verdict rejected: %s." error
            | Ok _, None -> return "Verdict rejected because reviewer context is missing."
            | Ok value, Some toolCallId ->
                let reviewerId = context.SessionId
                let managerId = reviewOwner scope reviewerId
                let treePort = scope.TreePortFor reviewerId

                match scope.Journal, managerId, treePort, context.ProviderRunId with
                | None, _, _, _ -> return "Verdict rejected because the reviewer journal is unavailable."
                | _, None, _, _ -> return "Verdict rejected because the manager session is missing."
                | _, _, None, _ -> return "Verdict rejected because the Git tree is unavailable."
                | _, _, _, None -> return "Verdict rejected because the provider run is missing."
                | Some _, Some owner, Some gitTree, Some providerRunId ->
                    let physicalUserMessageId =
                        scope.CurrentPhysicalUserMessage reviewerId |> Option.orElse context.UserMessageId

                    let! promptText =
                        match context.PromptText with
                        | Some text -> task { return Some text }
                        | None -> latestUserPromptText scope.Snapshot reviewerId physicalUserMessageId

                    let host = scope.ReviewerHostFor(reviewerId, owner, gitTree)

                    match
                        host.SubmitVerdict(
                            toolCallId,
                            value,
                            providerRunId,
                            ?userPromptText = promptText,
                            ?userMessageId = physicalUserMessageId
                        )
                    with
                    | Error error -> return sprintf "Verdict rejected: %s." error
                    | Ok result ->
                        scope.MarkVerdictSubmitted reviewerId

                        match result, value with
                        | ReviewFinishResult.Confirmed, ReviewGuardVerdict.Perfect ->
                            return "PERFECT recorded for the current tree."
                        | ReviewFinishResult.Confirmed, ReviewGuardVerdict.Revise
                        | ReviewFinishResult.NeedsReview, ReviewGuardVerdict.Revise ->
                            return "REVISE recorded for the current tree."
                        | ReviewFinishResult.NeedsReview, ReviewGuardVerdict.Perfect ->
                            return HostReviewGuard.skepticalReevaluationPrompt
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "verdict"
          Description = "Submit the review verdict"
          Arguments = [ "verdict", ToolHostCodec.enumSchema [ "PERFECT"; "REVISE" ] factory ]
          Execute = execute scope }
