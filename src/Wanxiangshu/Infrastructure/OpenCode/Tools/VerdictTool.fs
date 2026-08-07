namespace Wanxiangshu.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open Wanxiangshu.Tools

/// Reviewer verdict tool. ToolCallId, ProviderRunIdentity and the current tree
/// are explicit witnesses; assistant prose never determines a verdict.
///
/// This module only gathers identities and reports. Every judgement — dedupe,
/// causal proof, witness construction — belongs to `ReviewController`, which is
/// the single writer (REVIEW-003/006).
module VerdictTool =

    /// The Manager that owns this reviewer.
    ///
    /// `sessionParents` only. The previous version fell back to scanning every
    /// session's linkage for one containing this child, which is a full scan
    /// (PERSIST-008) that also accepted a hit under the wrong parent. A reviewer
    /// whose parent is unknown fails closed instead.
    let private reviewOwner (scope: ToolRuntimeScope) (reviewerId: string) =
        match scope.SessionParents.TryGetValue reviewerId with
        | true, parentId -> Some(SessionId.create parentId)
        | false, _ -> None

    /// REVIEW-006 requires both in the witness, and both come from the durable
    /// Manager job. `None` is legitimate for a Manager that is not running under an
    /// Orchestrator job.
    let private jobIdentity (scope: ToolRuntimeScope) (managerSessionId: SessionId) =
        match scope.Journal with
        | None -> None, None
        | Some journal ->
            match
                OrchestratorProjection.tryFindByManagerSession
                    managerSessionId
                    (AgentJournal.snapshot journal).AgentProjections.Orchestrator
            with
            | None -> None, None
            | Some job -> Some job.ManagerJobId, Some job.WorktreeIdentity

    /// REVIEW-008: the barrier this verdict belongs to.
    ///
    /// Read from the reviewer's own guard rather than derived from the tree: a
    /// barrier is opened explicitly, and post-rebase review must be a NEW barrier
    /// even when the tree hash is unchanged. Deriving it from the tree would make
    /// those two indistinguishable.
    let private currentBarrier (scope: ToolRuntimeScope) (reviewerId: string) =
        match scope.Journal with
        | None -> None
        | Some journal ->
            AgentProjection.tryFind (SessionId.create reviewerId) (AgentJournal.snapshot journal).AgentProjections
            |> Option.bind (fun session -> session.ReviewGuard)
            |> Option.bind (fun guard -> guard.CurrentBarrierId)

    let private tString = ToolHostCodec.TString

    let private report (decision: VerdictDecision) =
        match decision with
        | VerdictDecision.Revised -> ToolHostCodec.tomlObject [ "verdict", tString "REVISE" ]
        // The challenge text IS the tool result the second run must consume
        // (REVIEW-003). Returning it here is what puts it into that run's input
        // seal, so the string comes from the domain rather than being written again.
        | VerdictDecision.ChallengeIssued challenge ->
            let instructions =
                if System.String.IsNullOrWhiteSpace challenge then
                    []
                else
                    [ challenge ]

            ToolHostCodec.tomlObjectWithInstructions instructions []
        | VerdictDecision.Confirmed -> ToolHostCodec.tomlObject [ "verdict", tString "PERFECT" ]
        | VerdictDecision.ChallengeUnproven ->
            ToolHostCodec.tomlObject
                [ "error",
                  tString
                      "Verdict rejected: this provider run has no input seal proving it received the previous challenge." ]
        | VerdictDecision.AlreadyCounted -> ToolHostCodec.tomlObject [ "status", tString "already_recorded" ]

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let diagLine =
                sprintf
                    "verdict-exec reviewer=%s role=%A run=%s"
                    context.SessionId
                    (scope.RoleFor context)
                    (match context.ProviderRunId with
                     | Some run -> ProviderRunIdentity.value run
                     | None -> "-")

            emitJsExpr diagLine "require('node:fs').appendFileSync('/tmp/verdict-diag.log', $0 + '\\n')"

            let verdict = StaticTools.reviewerVerdictOfString (args.Text "verdict")

            let validated =
                if scope.RoleFor context <> Some Role.Reviewer then
                    Error "the verdict tool is available only to reviewer sessions"
                elif String.IsNullOrWhiteSpace context.SessionId then
                    Error "missing sessionID"
                else
                    match verdict, context.ToolCallId, context.ProviderRunId with
                    | Error error, _, _ -> Error error
                    | _, None, _ -> Error "missing tool call id"
                    | _, _, None -> Error "missing provider run id"
                    | Ok value, Some toolCallId, Some providerRunId -> Ok(value, toolCallId, providerRunId)

            match validated with
            | Error error ->
                return ToolHostCodec.tomlObject [ "error", tString (sprintf "Verdict rejected: %s." error) ]
            | Ok(value, toolCallId, providerRunId) ->
                let reviewerId = context.SessionId

                match
                    scope.Journal,
                    reviewOwner scope reviewerId,
                    scope.TreePortFor reviewerId,
                    currentBarrier scope reviewerId
                with
                | None, _, _, _ ->
                    return
                        ToolHostCodec.tomlObject
                            [ "error", tString "Verdict rejected because the reviewer journal is unavailable." ]
                | _, None, _, _ ->
                    return
                        ToolHostCodec.tomlObject
                            [ "error", tString "Verdict rejected because the manager session is unknown." ]
                | _, _, None, _ ->
                    return
                        ToolHostCodec.tomlObject
                            [ "error", tString "Verdict rejected because the Git tree is unavailable." ]
                // REVIEW-008 fail closed: without an open barrier there is nothing
                // this verdict could confirm, and inventing one would let a
                // post-rebase review reuse a pre-rebase confirmation.
                | _, _, _, None ->
                    return
                        ToolHostCodec.tomlObject
                            [ "error", tString "Verdict rejected because no review barrier is open for this tree." ]
                | Some journal, Some managerSessionId, Some gitTree, Some barrierId ->
                    let managerJobId, worktreeIdentity = jobIdentity scope managerSessionId

                    let submission: VerdictSubmission =
                        { BarrierId = barrierId
                          GitTreeHash = GitTreeHash.create (gitTree.GetTreeHash())
                          ManagerSessionId = managerSessionId
                          ReviewerSessionId = SessionId.create reviewerId
                          ManagerJobId = managerJobId
                          WorktreeIdentity = worktreeIdentity
                          ProviderRun = providerRunId
                          ToolCallId = toolCallId
                          Verdict = value }

                    // No terminal notification here. A confirmed dual-PERFECT
                    // completes through the reconcile path's `reviewerAlreadyConfirmed`
                    // branch, which reads the Authority Root from the durable
                    // projection. The previous version notified inline and built the
                    // root from this tool's physical user message id — fabricating a
                    // PROMPT-002 identity from a PROMPT-001 one.
                    match ReviewController.submit journal HostDigest.sha256Hex submission with
                    | Ok VerdictDecision.ChallengeUnproven ->
                        // submit once more. The tool's `context.ProviderRunId` is the
                        // only reliable binding key — the reconcile `onTurn` run
                        // disagrees with it on Host 1.18.10 for challenge responses,
                        // so the onTurn binding was removed (it wrote dead data) and
                        // `ReviewSeal.bindToRun` is the sole writer (REVIEW-010).
                        match
                            ReviewSeal.bindToRun
                                journal
                                scope.PendingReviewSeals
                                (SessionId.create reviewerId)
                                providerRunId
                        with
                        | Error ReviewSeal.NoPendingSeal ->
                            return
                                ToolHostCodec.tomlObject
                                    [ "error",
                                      tString
                                          "Verdict rejected: this provider run has no input seal proving it received the previous challenge." ]
                        | Error(ReviewSeal.AppendFailed bindFailure) ->
                            return
                                ToolHostCodec.tomlObject
                                    [ "error",
                                      tString (
                                          sprintf
                                              "Verdict rejected: challenge unproven (seal bind failed: %s)"
                                              bindFailure
                                      ) ]
                        | Ok _ ->
                            match ReviewController.submit journal HostDigest.sha256Hex submission with
                            | Error retryError ->
                                return
                                    ToolHostCodec.tomlObject
                                        [ "error",
                                          tString (
                                              sprintf "Verdict rejected: challenge unproven (retry: %s)" retryError
                                          ) ]
                            | Ok VerdictDecision.ChallengeUnproven ->
                                return
                                    ToolHostCodec.tomlObject
                                        [ "error",
                                          tString
                                              "Verdict rejected: this provider run has no input seal proving it received the previous challenge." ]
                            | Ok decision ->
                                scope.PendingReviewSeals.Remove reviewerId |> ignore
                                scope.MarkVerdictSubmitted reviewerId
                                return report decision
                    | Ok decision ->
                        scope.MarkVerdictSubmitted reviewerId
                        return report decision
                    | Error error ->
                        return ToolHostCodec.tomlObject [ "error", tString (sprintf "Verdict rejected: %s." error) ]
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "verdict"
          Description = "Submit the review verdict"
          Arguments = [ "verdict", ToolHostCodec.enumSchema [ "PERFECT"; "REVISE" ] factory ]
          Execute = execute scope }
