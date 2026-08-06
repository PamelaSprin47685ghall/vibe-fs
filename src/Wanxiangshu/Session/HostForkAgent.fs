namespace Wanxiangshu.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.OpenCode
open Wanxiangshu.Session.AgentRoleIdentity

[<AutoOpen>]
module HostForkAgent =

    let private userPromptText (message: SessionMessage) =
        message.Parts
        |> Array.choose (function
            | MessagePart.Text text -> Some text
            | _ -> None)
        |> String.concat "\n"
        |> fun text -> if String.IsNullOrWhiteSpace text then None else Some text

    let private missingReviewRequirement (input: ReviewRequirementInput) : Result<string list, string> =
        Error(
            sprintf
                "Cannot load original user requirement message %s for reviewer"
                (AuthorityRootUserMessageId.value input.AuthorityRootUserMessageId)
        )

    let rec private resolveReviewRequirementInputs
        (port: ISessionSnapshotPort)
        (inputs: ReviewRequirementInput list)
        (cached: Map<SessionId, SessionMessage list>)
        (resolved: string list)
        : Task<Result<string list, string>> =
        task {
            match inputs with
            | [] -> return Ok(List.rev resolved)
            | input :: remaining ->
                let consume (messages: SessionMessage list) =
                    // The Authority Root was promoted from a physical message, so its
                    // value is that message's wire address. Compared as an address
                    // because the transcript has no notion of authority.
                    let address = AuthorityRootUserMessageId.value input.AuthorityRootUserMessageId

                    messages
                    |> List.tryFind (fun message -> message.Id = address)
                    |> Option.bind userPromptText

                match Map.tryFind input.SourceSessionId cached with
                | Some messages ->
                    match consume messages with
                    | Some text -> return! resolveReviewRequirementInputs port remaining cached (text :: resolved)
                    | None -> return! resolveReviewRequirementInputs port remaining cached resolved
                | None ->
                    let! messagesResult = port.GetMessages input.SourceSessionId

                    match messagesResult with
                    | Error err -> return Error(sprintf "Cannot load original user requirements for reviewer: %s" err)
                    | Ok messages ->
                        let updated = Map.add input.SourceSessionId messages cached

                        match consume messages with
                        | Some text -> return! resolveReviewRequirementInputs port remaining updated (text :: resolved)
                        // Host revert cleanup permanently removes the reverted user
                        // message. Its HumanRoot requirement is therefore withdrawn,
                        // not unavailable: continue with the still-live roots. A
                        // snapshot Error above remains fail-closed and cannot be
                        // mistaken for a withdrawal.
                        | None -> return! resolveReviewRequirementInputs port remaining updated resolved
        }

    /// REVIEW-002's authoritative review scope, as the texts themselves.
    ///
    /// Returns the requirement list rather than a composed prompt, which is the N3 change. It used to
    /// return `assignment` wrapped in a `[Original user requirements …]` envelope, so the reviewer's
    /// first prompt had one shape with requirements and another without — and the caller then wrapped
    /// THAT in a second conditional envelope. ARCH-010 wants the instruction/data split decided by the
    /// producer, so the producer hands over data and `ForkChildPayload` decides the shape.
    ///
    /// An empty list is the ordinary case: every non-Reviewer fork, and a Reviewer forked when no new
    /// HumanRoot has arrived since the last double-PERFECT barrier.
    let private resolveReviewerRequirements
        (journal: AgentJournal option)
        (snapshot: ISessionSnapshotPort option)
        (parentId: SessionId)
        : Task<Result<string list, string>> =
        task {
            let promptInputs = AgentJournal.pendingReviewRequirements journal parentId

            if List.isEmpty promptInputs then
                return Ok []
            else
                match snapshot with
                | None ->
                    return
                        Error
                            "Cannot start reviewer: original user requirements are unavailable without a session transcript"
                | Some port -> return! resolveReviewRequirementInputs port promptInputs Map.empty []
        }

    type HostForkRuntime with

        /// PROMPT-008: `agent` is the managed agent name the caller selected, and
        /// it is required. Defaulting it to `fast-ROLE` invented a tier, and the
        /// invented name then travelled to the Host send boundary as if chosen.
        ///
        /// `renderedPrompt` (PENDING 7): the caller already rendered the first-prompt
        /// ARCH-010 payload and wants it sent verbatim instead of the Host's own relay
        /// envelope. Used by `ForkTool` when a Coder TDD phase must reach the child as
        /// the durable `[tdd]` table. `prompt` stays the original assignment so opening
        /// capture and journal facts keep the task text, not the envelope.
        member this.Fork
            (
                agentId: string,
                role: AgentRole,
                agent: string,
                prompt: string,
                payload: string option,
                ?firstPrompt: bool,
                ?renderedPrompt: string
            ) : Task<Result<ForkResult, string>> =
            let agentName = agent.Trim()
            let isFirstPrompt = defaultArg firstPrompt true

            task {
                // GREEN-4: recovery ownership is SessionRecoveryWorkflow only.
                let retired = this.IsRetiredHandle agentId

                let existing =
                    lock this.Gate (fun () ->
                        match this.Children.TryGetValue agentId with
                        | true, childId -> Some childId
                        | false, _ -> None)

                // The ARCH-010 first-prompt payload is computed once and used by
                // both child paths: a brand-new child AND an idle restored child
                // receive the same envelope, so a canary declaration anchored on
                // the envelope cannot tell which path produced the request
                // (measured: the post-restart review fork sent the raw opening
                // prompt and every barrier-reviewer declaration failed to match).
                // Continuations (busy nudge, challenge, manager resume) opt out.
                //
                // PENDING 7: `?renderedPrompt: string` (not `string option`) so the
                // body sees `string option`. Caller supplies a pre-rendered ARCH-010
                // document when present; otherwise Host builds the relay envelope.
                let! enrichedResult =
                    if isFirstPrompt then
                        match renderedPrompt with
                        | Some rendered -> Task.FromResult(Ok([], rendered))
                        | None ->
                            task {
                                let! requirementsResult =
                                    match role with
                                    | AgentRole.Reviewer ->
                                        resolveReviewerRequirements this.Journal this.SessionSnapshot this.ParentId
                                    | _ -> Task.FromResult(Ok [])

                                return
                                    requirementsResult
                                    |> Result.map (fun requirements ->
                                        requirements,
                                        ForkChildPayload.relay
                                            prompt
                                            (this.ParentWorkRecordOf this.ParentId)
                                            requirements
                                            payload)
                            }
                    else
                        Task.FromResult(Ok([], prompt))

                match enrichedResult with
                | Error err -> return Error err
                | Ok(requirements, enrichedPrompt) ->
                    match retired, existing with
                    | Some true, _ -> return Error(sprintf "RetiredHandle: %s" agentId)
                    | _, Some childId ->
                        match HostPendingRun.sessionDeadRefusal this.Journal childId with
                        | Some refusal -> return Error refusal
                        | None ->
                            return!
                                HostForkChildDispatch.sendToExistingChild
                                    this.Gate
                                    this.PendingRuns
                                    this.Journal
                                    this.ParentId
                                    this.Sessions
                                    this.ChildWorkRecordOf
                                    this.Runtime
                                    this.SendChildPrompt
                                    this.SendBusyNudge
                                    (fun child role -> this.RunStarted child role (this.DirectoryOf agentId))
                                    agentId
                                    childId
                                    role
                                    prompt
                                    agentName
                                    (if isFirstPrompt then Some enrichedPrompt else None)
                    | _, None ->
                        let! childResult =
                            this.Sessions.CreateChildSession(
                                this.ParentId,
                                { Title = Some agentId
                                  Agent = Some agentName
                                  Directory = this.DirectoryOf agentId }
                            )

                        match childResult with
                        | Error err -> return Error err
                        | Ok childId ->
                            let linkageResult =
                                HandleController.link this.Journal this.ParentId agentId childId agentName role
                                |> Result.mapError (sprintf "Failed to persist HandleLinked: %s")

                            match linkageResult with
                            | Error err ->
                                let! _ = this.Sessions.AbortSession childId
                                return Error err
                            | Ok() ->
                                let run = this.InstallRun(agentId, childId, role)

                                lock this.Gate (fun () -> this.Children.[agentId] <- childId)

                                this.ChildCreated agentId role childId
                                this.ChildCreatedDir agentId childId (this.DirectoryOf agentId)

                                // REVIEW-007: a Manager's own review fork opens the
                                // barrier for the Reviewer BEFORE its first prompt is
                                // sent, so the first verdict can confirm. Orchestrator
                                // runtimes keep this off — ORCH-006 opens their
                                // barriers at the reverify site, and a second writer
                                // would fight the first over `CurrentBarrierId`.
                                let barrierOutcome =
                                    if this.ManagerOpensReviewBarrier && role = AgentRole.Reviewer then
                                        match this.TreeHashFor agentId with
                                        | None -> Ok()
                                        | Some tree ->
                                            let barrierId = ReviewBarrierId.create (System.Guid.NewGuid().ToString("N"))

                                            ReviewBarrier.openBarrier this.Journal this.ParentId childId barrierId tree
                                    else
                                        Ok()

                                match barrierOutcome with
                                | Error err ->
                                    let! _ = this.Sessions.AbortSession childId
                                    this.FailRun(run, err)
                                    return Error err
                                | Ok() ->
                                    let result =
                                        this.Runtime.Fork(
                                            agentId,
                                            role,
                                            agentName,
                                            runWork = (fun () -> run.Source.Task)
                                        )

                                    match result with
                                    | ForkResult.NotFound _ ->
                                        this.FailRun(run, "Fork runtime is cancelled")
                                        return Error "Fork runtime is cancelled"
                                    | _ ->
                                        this.MarkReady(run)

                                        // COMPANION-003 / EXEC-006: the child's
                                        // OpeningPromptRaw is the ORIGINAL fork
                                        // assignment and authoritative requirements,
                                        // NOT the rendered envelope (which carries
                                        // parent_work_record and would nest the
                                        // parent LWR recursively). Captured before
                                        // the first prompt is sent; idempotent.
                                        if isFirstPrompt then
                                            XTraceCapture.captureOpening this.Journal childId prompt requirements

                                        let! sent =
                                            HostForkAgentOwner.sendFirstPrompt
                                                this.Sessions
                                                this.Journal
                                                childId
                                                agentName
                                                (this.DirectoryOf agentId)
                                                enrichedPrompt

                                        match sent with
                                        | Ok _ -> return Ok result
                                        | Error err ->
                                            this.FailRun(run, err)
                                            return Error err
            }

        member this.Reuse(agentId: string, prompt: string) : Task<Result<ForkResult, string>> =
            task {
                // GREEN-4: recovery ownership is SessionRecoveryWorkflow only.
                let retired = this.IsRetiredHandle agentId

                let existing =
                    lock this.Gate (fun () ->
                        match this.Children.TryGetValue agentId with
                        | true, childId -> Some childId
                        | false, _ -> None)

                match retired, existing with
                | Some true, _ -> return Error(sprintf "RetiredHandle: %s" agentId)
                | _, None -> return Error(sprintf "Unknown agent id: %s" agentId)
                | _, Some childId ->
                    match HostPendingRun.sessionDeadRefusal this.Journal childId with
                    | Some refusal -> return Error refusal
                    | None ->
                        // The record carries the managed name this handle was forked
                        // with. Rebuilding it from the role would silently downgrade a
                        // deep-* agent to fast-* on reuse.
                        let recordOpt =
                            this.Runtime.List()
                            |> fst
                            |> List.tryFind (fun agent -> agent.AgentId = agentId)

                        match recordOpt with
                        | None -> return Error(sprintf "Unknown agent id: %s" agentId)
                        | Some record when System.String.IsNullOrWhiteSpace record.Agent ->
                            return Error(sprintf "Agent handle '%s' has no managed agent name" agentId)
                        | Some record ->
                            let role = record.Role
                            let agentName = record.Agent

                            return!
                                HostForkChildDispatch.sendToExistingChild
                                    this.Gate
                                    this.PendingRuns
                                    this.Journal
                                    this.ParentId
                                    this.Sessions
                                    this.ChildWorkRecordOf
                                    this.Runtime
                                    this.SendChildPrompt
                                    this.SendBusyNudge
                                    (fun child role -> this.RunStarted child role (this.DirectoryOf agentId))
                                    agentId
                                    childId
                                    role
                                    prompt
                                    agentName
                                    None
            }
