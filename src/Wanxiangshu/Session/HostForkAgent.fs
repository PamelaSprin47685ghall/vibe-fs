namespace Wanxiangshu.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel
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
                role: Role,
                agent: string,
                prompt: string,
                payload: string option,
                ?firstPrompt: bool,
                ?renderedPrompt: string,
                ?ownership: Fact.HandleOwnership,
                ?deferSend: bool
            ) : Task<Result<ForkResult, string>> =
            let agentName = agent.Trim()
            let isFirstPrompt = defaultArg firstPrompt true
            let deferSend = defaultArg deferSend false
            let handleOwnership = defaultArg ownership this.HandleOwnership

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
                                    | Role.Reviewer ->
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
                                HandleController.link
                                    this.Journal
                                    this.ParentId
                                    agentId
                                    childId
                                    agentName
                                    role
                                    handleOwnership
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

                                // GLORY-033: the fork surface no longer opens review
                                // barriers. A Manager cannot fork a Reviewer at all
                                // (GLORY-031), and every Host-owned barrier opens at
                                // its reverify site (HostReviewProgram / ORCH-006).
                                let result =
                                    this.Runtime.Fork(agentId, role, agentName, runWork = (fun () -> run.Source.Task))

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

                                    if deferSend && isFirstPrompt then
                                        this.DeferredFirstPrompts.[agentId] <-
                                            {| ChildId = childId
                                               AgentName = agentName
                                               Prompt = enrichedPrompt |}

                                        return Ok result
                                    else
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
                // After join retires the prior work unit, reuse of the same
                // agent id reopens Labor on the same child session (GLORY-068 /
                // "十年修得同船渡"). Abandoned handles stay unusable.
                let abandoned =
                    this.Journal
                    |> Option.map (fun durable ->
                        let projection = AgentJournal.handleProjection durable this.ParentId
                        let handle = HandleController.agentHandle agentId
                        HandleProjection.isAbandoned handle projection)

                let existing =
                    lock this.Gate (fun () ->
                        match this.Children.TryGetValue agentId with
                        | true, childId -> Some childId
                        | false, _ -> None)

                match abandoned, existing with
                | Some true, _ -> return Error(sprintf "RetiredHandle: %s" agentId)
                | _, None -> return Error(sprintf "Unknown agent id: %s" agentId)
                | _, Some childId ->
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

                        // Re-open a joined (Retired) handle before the new send so
                        // the next completion has an Active cell to claim.
                        match
                            HandleController.link
                                this.Journal
                                this.ParentId
                                agentId
                                childId
                                agentName
                                role
                                this.HandleOwnership
                        with
                        | Error linkError -> return Error linkError
                        | Ok() ->
                            // A reuse after join is a new work unit on the same
                            // child session — same first-prompt envelope a brand-new
                            // fork would receive (ARCH-010). Busy-nudge continues
                            // still go through sendToExistingChild's active-run path
                            // with the raw prompt when a run is already live.
                            let activeRun =
                                lock this.Gate (fun () ->
                                    match this.PendingRuns.TryGetValue agentId with
                                    | true, _ -> true
                                    | false, _ -> false)

                            let enriched =
                                if activeRun then
                                    None
                                else
                                    Some(ForkChildPayload.relay prompt (this.ParentWorkRecordOf this.ParentId) [] None)

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
                                    enriched
            }
