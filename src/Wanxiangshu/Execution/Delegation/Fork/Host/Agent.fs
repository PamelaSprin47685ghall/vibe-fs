namespace Wanxiangshu.Execution.Delegation.Fork.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Persona.AgentRoleIdentity

/// Existing-child send must use the session's bound managed agent.
/// Never invent `fast-ROLE` from CanonicalRole; never let the caller overwrite Deep with Fast.
module HostForkBinding =
    let tryName (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Some(value.Trim())

    let private fromHandle (journal: AgentJournal option) (parentId: SessionId) (agentId: string) =
        journal
        |> Option.bind (fun durable ->
            AgentJournal.handleProjection durable parentId
            |> HandleProjection.tryFind (HandleController.agentHandle agentId)
            |> Option.bind (fun handle -> tryName handle.TargetAgent))

    let private fromChildRun (runtime: ForkRuntime) (agentId: string) =
        runtime.List()
        |> fst
        |> List.tryFind (fun record -> record.AgentId = agentId)
        |> Option.bind (fun record -> tryName record.Agent)

    let private fromChildProfile (journal: AgentJournal option) (childId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            let projections = (AgentJournal.snapshot durable).AgentProjections

            PromptAuthorityLedger.activeProfile childId projections
            |> Option.orElseWith (fun () -> PromptAuthorityLedger.lastAuthorityProfile childId projections)
            |> Option.bind (fun profile -> tryName profile.SelectedAgent))

    let managedAgent
        (journal: AgentJournal option)
        (parentId: SessionId)
        (runtime: ForkRuntime)
        (agentId: string)
        (childId: SessionId)
        : string option =
        fromHandle journal parentId agentId
        |> Option.orElseWith (fun () -> fromChildRun runtime agentId)
        |> Option.orElseWith (fun () -> fromChildProfile journal childId)

[<AutoOpen>]
module HostForkAgent =

    let private forkInstructions (sessionId: SessionId) : ForkChildInstructions =
        let lang = ProviderProse.languageOf sessionId

        { Base = ProviderProse.instructionLines lang ForkChildPayload.BasePath Map.empty
          CommissionerRecord = ProviderProse.render lang ForkChildPayload.CommissionerRecordPath Map.empty
          Attachment = ProviderProse.render lang ForkChildPayload.AttachmentPath Map.empty
          Requirements = ProviderProse.render lang ForkChildPayload.RequirementsPath Map.empty }

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

    let private consumeRequirementText (input: ReviewRequirementInput) (messages: SessionMessage list) =
        // The Authority Root was promoted from a physical message, so its
        // value is that message's wire address. Compared as an address
        // because the transcript has no notion of authority.
        let address = AuthorityRootUserMessageId.value input.AuthorityRootUserMessageId

        messages
        |> List.tryFind (fun message -> message.Id = address)
        |> Option.bind userPromptText

    let rec private resolveReviewRequirementInputs
        (port: ISessionSnapshotPort)
        (inputs: ReviewRequirementInput list)
        (cached: Map<SessionId, SessionMessage list>)
        (resolved: string list)
        : Task<Result<string list, string>> =
        task {
            match inputs with
            | [] -> return Ok(List.rev resolved)
            | input :: remaining -> return! stepRequirementInput port input remaining cached resolved
        }

    and private advanceResolved
        (port: ISessionSnapshotPort)
        (remaining: ReviewRequirementInput list)
        (cached: Map<SessionId, SessionMessage list>)
        (resolved: string list)
        (textOpt: string option)
        =
        match textOpt with
        | Some text -> resolveReviewRequirementInputs port remaining cached (text :: resolved)
        | None -> resolveReviewRequirementInputs port remaining cached resolved

    and private afterMessagesLoaded
        (port: ISessionSnapshotPort)
        (input: ReviewRequirementInput)
        (remaining: ReviewRequirementInput list)
        (cached: Map<SessionId, SessionMessage list>)
        (resolved: string list)
        (messagesResult: Result<SessionMessage list, string>)
        =
        match messagesResult with
        | Error err -> Task.FromResult(Error(sprintf "Cannot load original user requirements for reviewer: %s" err))
        | Ok messages ->
            // Host revert cleanup permanently removes the reverted user
            // message. Its HumanRoot requirement is therefore withdrawn,
            // not unavailable: continue with the still-live roots. A
            // snapshot Error above remains fail-closed and cannot be
            // mistaken for a withdrawal.
            let updated = Map.add input.SourceSessionId messages cached
            advanceResolved port remaining updated resolved (consumeRequirementText input messages)

    and private stepRequirementInput
        (port: ISessionSnapshotPort)
        (input: ReviewRequirementInput)
        (remaining: ReviewRequirementInput list)
        (cached: Map<SessionId, SessionMessage list>)
        (resolved: string list)
        =
        task {
            match Map.tryFind input.SourceSessionId cached with
            | Some messages ->
                return! advanceResolved port remaining cached resolved (consumeRequirementText input messages)
            | None ->
                let! messagesResult = port.GetMessages input.SourceSessionId
                return! afterMessagesLoaded port input remaining cached resolved messagesResult
        }

    let private loadReviewerRequirements
        (snapshot: ISessionSnapshotPort option)
        (promptInputs: ReviewRequirementInput list)
        : Task<Result<string list, string>> =
        match snapshot with
        | None ->
            Task.FromResult(
                Error "Cannot start reviewer: original user requirements are unavailable without a session transcript"
            )
        | Some port -> resolveReviewRequirementInputs port promptInputs Map.empty []

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
        let promptInputs = AgentJournal.pendingReviewRequirements journal parentId

        if List.isEmpty promptInputs then
            Task.FromResult(Ok [])
        else
            loadReviewerRequirements snapshot promptInputs

    let private requirementsForRole (runtime: HostForkRuntime) (role: Role) =
        match role with
        | Role.Reviewer -> resolveReviewerRequirements runtime.Journal runtime.SessionSnapshot runtime.ParentId
        | _ -> Task.FromResult(Ok [])

    let private buildRelayEnvelope
        (runtime: HostForkRuntime)
        (role: Role)
        (prompt: string)
        (payload: string option)
        : Task<Result<string list * string, string>> =
        task {
            let! requirementsResult = requirementsForRole runtime role
            let! parentWorkRecord = runtime.ParentWorkRecordOf runtime.ParentId

            return
                requirementsResult
                |> Result.map (fun requirements ->
                    requirements,
                    ForkChildPayload.relay
                        (forkInstructions runtime.ParentId)
                        prompt
                        parentWorkRecord
                        None
                        requirements
                        payload)
        }

    let private enrichFirstPrompt
        (runtime: HostForkRuntime)
        (role: Role)
        (prompt: string)
        (payload: string option)
        (renderedPrompt: string option)
        : Task<Result<string list * string, string>> =
        match renderedPrompt with
        | Some rendered -> Task.FromResult(Ok([], rendered))
        | None -> buildRelayEnvelope runtime role prompt payload

    let private maybeReplaceToolEstimate
        (journal: AgentJournal option)
        (expectedToolCalls: int option)
        (childId: SessionId)
        : Task<unit> =
        match journal, expectedToolCalls with
        | Some journal, Some expected -> DelegatedToolEstimateLedger.replace journal childId expected
        | _ -> Task.FromResult(())

    let private sendFirstPromptOutcome
        (runtime: HostForkRuntime)
        (run: PendingHostRun)
        (agentId: string)
        (childId: SessionId)
        (agentName: string)
        (enrichedPrompt: string)
        (result: ForkResult)
        : Task<Result<ForkResult, string>> =
        task {
            let! sent =
                HostForkAgentOwner.sendFirstPromptObserved
                    runtime.Sessions
                    runtime.Journal
                    childId
                    agentName
                    (runtime.DirectoryOf agentId)
                    enrichedPrompt
                    (fun error -> runtime.FailRun(run, error))

            match sent with
            | Ok _ -> return Ok result
            | Error err ->
                do! runtime.FailRun(run, err)
                return Error err
        }

    let private finishSuccessfulNewChild
        (runtime: HostForkRuntime)
        (agentId: string)
        (childId: SessionId)
        (agentName: string)
        (prompt: string)
        (requirements: string list)
        (enrichedPrompt: string)
        (isFirstPrompt: bool)
        (deferSend: bool)
        (expectedToolCalls: int option)
        (run: PendingHostRun)
        (result: ForkResult)
        : Task<Result<ForkResult, string>> =
        task {
            runtime.MarkReady(run)

            // COMPANION-003 / EXEC-006: the child's OpeningMaterial is the ORIGINAL
            // fork assignment and authoritative requirements, NOT the rendered
            // envelope (which carries commissioner_record and would nest the
            // parent LWR recursively). Captured before the first prompt is sent;
            // idempotent.
            if isFirstPrompt then
                do! XTraceCapture.captureOpening runtime.Journal childId prompt requirements

            do! maybeReplaceToolEstimate runtime.Journal expectedToolCalls childId

            if deferSend && isFirstPrompt then
                runtime.DeferredFirstPrompts.[agentId] <-
                    {| ChildId = childId
                       AgentName = agentName
                       Prompt = enrichedPrompt |}

                return Ok result
            else
                return! sendFirstPromptOutcome runtime run agentId childId agentName enrichedPrompt result
        }

    let private afterRuntimeFork
        (runtime: HostForkRuntime)
        (agentId: string)
        (childId: SessionId)
        (agentName: string)
        (prompt: string)
        (requirements: string list)
        (enrichedPrompt: string)
        (isFirstPrompt: bool)
        (deferSend: bool)
        (expectedToolCalls: int option)
        (run: PendingHostRun)
        (result: ForkResult)
        : Task<Result<ForkResult, string>> =
        match result with
        | ForkResult.NotFound _ ->
            task {
                do! runtime.FailRun(run, "Fork runtime is cancelled")
                return Error "Fork runtime is cancelled"
            }
        | _ ->
            finishSuccessfulNewChild
                runtime
                agentId
                childId
                agentName
                prompt
                requirements
                enrichedPrompt
                isFirstPrompt
                deferSend
                expectedToolCalls
                run
                result

    let private afterLinkage
        (runtime: HostForkRuntime)
        (agentId: string)
        (role: Role)
        (agentName: string)
        (prompt: string)
        (requirements: string list)
        (enrichedPrompt: string)
        (isFirstPrompt: bool)
        (deferSend: bool)
        (expectedToolCalls: int option)
        (childId: SessionId)
        (linkageResult: Result<unit, string>)
        : Task<Result<ForkResult, string>> =
        match linkageResult with
        | Error err ->
            task {
                let! _ = runtime.Sessions.AbortSession childId
                return Error err
            }
        | Ok() ->
            task {
                let run = runtime.InstallRun(agentId, childId, role)

                lock runtime.Gate (fun () -> runtime.Children.[agentId] <- childId)

                runtime.ChildCreated agentId role childId
                runtime.ChildCreatedDir agentId childId (runtime.DirectoryOf agentId)

                // GLORY-033: the fork surface no longer opens review barriers. A
                // Manager cannot fork a Reviewer at all (GLORY-031), and every
                // Host-owned barrier opens at its reverify site
                // (HostReviewProgram / ORCH-006).
                let result =
                    runtime.Runtime.Fork(agentId, role, agentName, runWork = (fun () -> run.Source.Task))

                return!
                    afterRuntimeFork
                        runtime
                        agentId
                        childId
                        agentName
                        prompt
                        requirements
                        enrichedPrompt
                        isFirstPrompt
                        deferSend
                        expectedToolCalls
                        run
                        result
            }

    let private forkNewChild
        (runtime: HostForkRuntime)
        (agentId: string)
        (role: Role)
        (agentName: string)
        (providerByname: string)
        (prompt: string)
        (requirements: string list)
        (enrichedPrompt: string)
        (handleOwnership: HandleOwnership)
        (isFirstPrompt: bool)
        (deferSend: bool)
        (expectedToolCalls: int option)
        : Task<Result<ForkResult, string>> =
        task {
            let! childResult =
                runtime.Sessions.CreateChildSession(
                    runtime.ParentId,
                    { Title = Some agentId
                      Agent = Some agentName
                      Directory = runtime.DirectoryOf agentId }
                )

            match childResult with
            | Error err -> return Error err
            | Ok childId ->
                let! linkageRaw =
                    HandleController.linkNamed
                        runtime.Journal
                        runtime.ParentId
                        agentId
                        childId
                        agentName
                        providerByname
                        role
                        handleOwnership

                let linkageResult =
                    linkageRaw |> Result.mapError (sprintf "Failed to persist HandleLinked: %s")

                return!
                    afterLinkage
                        runtime
                        agentId
                        role
                        agentName
                        prompt
                        requirements
                        enrichedPrompt
                        isFirstPrompt
                        deferSend
                        expectedToolCalls
                        childId
                        linkageResult
        }

    let private forkExistingChild
        (runtime: HostForkRuntime)
        (agentId: string)
        (childId: SessionId)
        (role: Role)
        (agentName: string)
        (prompt: string)
        (enrichedPrompt: string)
        (isFirstPrompt: bool)
        (expectedToolCalls: int option)
        : Task<Result<ForkResult, string>> =
        task {
            do! maybeReplaceToolEstimate runtime.Journal expectedToolCalls childId

            let sendAgent =
                HostForkBinding.managedAgent runtime.Journal runtime.ParentId runtime.Runtime agentId childId
                |> Option.defaultValue agentName

            return!
                HostForkChildDispatch.sendToExistingChild
                    runtime.Gate
                    runtime.PendingRuns
                    runtime.Journal
                    runtime.ParentId
                    runtime.Sessions
                    runtime.ChildWorkRecordOf
                    runtime.TrackOwnedWork
                    runtime.Runtime
                    runtime.SendChildPrompt
                    runtime.SendBusyNudge
                    (fun child role -> runtime.RunStarted child role (runtime.DirectoryOf agentId))
                    agentId
                    childId
                    role
                    prompt
                    sendAgent
                    (if isFirstPrompt then Some enrichedPrompt else None)
        }

    let private forkAfterEnrich
        (runtime: HostForkRuntime)
        (agentId: string)
        (role: Role)
        (agentName: string)
        (providerByname: string)
        (prompt: string)
        (handleOwnership: HandleOwnership)
        (isFirstPrompt: bool)
        (deferSend: bool)
        (expectedToolCalls: int option)
        (retired: bool option)
        (existing: SessionId option)
        (requirements: string list)
        (enrichedPrompt: string)
        : Task<Result<ForkResult, string>> =
        match retired, existing with
        | Some true, _ -> Task.FromResult(Error(sprintf "RetiredHandle: %s" agentId))
        | _, Some childId ->
            forkExistingChild
                runtime
                agentId
                childId
                role
                agentName
                prompt
                enrichedPrompt
                isFirstPrompt
                expectedToolCalls
        | _, None ->
            forkNewChild
                runtime
                agentId
                role
                agentName
                providerByname
                prompt
                requirements
                enrichedPrompt
                handleOwnership
                isFirstPrompt
                deferSend
                expectedToolCalls

    let private resolveReuseEnrichedPrompt
        (runtime: HostForkRuntime)
        (prompt: string)
        (renderedPrompt: string option)
        : Task<string option> =
        match renderedPrompt with
        | Some rendered -> Task.FromResult(Some rendered)
        | None ->
            task {
                let! parentWorkRecord = runtime.ParentWorkRecordOf runtime.ParentId

                return
                    Some(
                        ForkChildPayload.relay (forkInstructions runtime.ParentId) prompt parentWorkRecord None [] None
                    )
            }

    let private reuseAfterRelink
        (runtime: HostForkRuntime)
        (agentId: string)
        (childId: SessionId)
        (role: Role)
        (agentName: string)
        (prompt: string)
        (renderedPrompt: string option)
        (wasDormant: bool)
        (linkResult: Result<unit, string>)
        : Task<Result<ForkResult, string>> =
        match linkResult with
        | Error linkError -> Task.FromResult(Error linkError)
        | Ok() ->
            task {
                runtime.ActivateDormantChildIfNeeded(wasDormant, agentId, childId, role)
                let! enriched = resolveReuseEnrichedPrompt runtime prompt renderedPrompt

                return!
                    HostForkChildDispatch.sendToExistingChild
                        runtime.Gate
                        runtime.PendingRuns
                        runtime.Journal
                        runtime.ParentId
                        runtime.Sessions
                        runtime.ChildWorkRecordOf
                        runtime.TrackOwnedWork
                        runtime.Runtime
                        runtime.SendChildPrompt
                        runtime.SendBusyNudge
                        (fun child role -> runtime.RunStarted child role (runtime.DirectoryOf agentId))
                        agentId
                        childId
                        role
                        prompt
                        agentName
                        enriched
            }

    let private reuseWithManagedAgent
        (runtime: HostForkRuntime)
        (agentId: string)
        (childId: SessionId)
        (role: Role)
        (agentName: string)
        (prompt: string)
        (renderedPrompt: string option)
        (wasDormant: bool)
        : Task<Result<ForkResult, string>> =
        task {
            let providerByname =
                runtime.Journal
                |> Option.bind (fun durable ->
                    AgentJournal.handleProjection durable runtime.ParentId
                    |> HandleProjection.tryFind (HandleController.agentHandle agentId))
                |> Option.map (fun handle -> handle.Byname)
                |> Option.filter (String.IsNullOrWhiteSpace >> not)
                |> Option.defaultValue agentName

            let activeRun =
                lock runtime.Gate (fun () -> runtime.PendingRuns.ContainsKey agentId)

            if activeRun then
                return!
                    HostForkChildDispatch.sendToExistingChild
                        runtime.Gate
                        runtime.PendingRuns
                        runtime.Journal
                        runtime.ParentId
                        runtime.Sessions
                        runtime.ChildWorkRecordOf
                        runtime.TrackOwnedWork
                        runtime.Runtime
                        runtime.SendChildPrompt
                        runtime.SendBusyNudge
                        (fun child role -> runtime.RunStarted child role (runtime.DirectoryOf agentId))
                        agentId
                        childId
                        role
                        prompt
                        agentName
                        None
            else
                let! linkResult =
                    HandleController.linkNamed
                        runtime.Journal
                        runtime.ParentId
                        agentId
                        childId
                        agentName
                        providerByname
                        role
                        runtime.HandleOwnership

                return!
                    reuseAfterRelink runtime agentId childId role agentName prompt renderedPrompt wasDormant linkResult
        }

    let private reuseLiveChild
        (runtime: HostForkRuntime)
        (agentId: string)
        (childId: SessionId)
        (wasDormant: bool)
        (prompt: string)
        (renderedPrompt: string option)
        (expectedToolCalls: int option)
        : Task<Result<ForkResult, string>> =
        task {
            do! maybeReplaceToolEstimate runtime.Journal expectedToolCalls childId

            // The record carries the managed name this handle was forked with.
            // Rebuilding it from the role would silently downgrade a deep-* agent
            // to fast-* on reuse.
            let recordOpt =
                runtime.Runtime.List()
                |> fst
                |> List.tryFind (fun agent -> agent.AgentId = agentId)

            let boundAgent =
                HostForkBinding.managedAgent runtime.Journal runtime.ParentId runtime.Runtime agentId childId

            match recordOpt, boundAgent with
            | None, _ -> return Error(sprintf "Unknown agent id: %s" agentId)
            | _, None -> return Error(sprintf "Agent handle '%s' has no managed agent name" agentId)
            | Some record, Some agentName ->
                return!
                    reuseWithManagedAgent runtime agentId childId record.Role agentName prompt renderedPrompt wasDormant
        }

    type HostForkRuntime with

        /// Durable binding for an already-linked child. Handle.TargetAgent first,
        /// then ChildRun.Agent, then the child's Authority SelectedAgent.
        member this.BoundManagedAgent(agentId: string, childId: SessionId) : string option =
            HostForkBinding.managedAgent this.Journal this.ParentId this.Runtime agentId childId

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
                ?ownership: HandleOwnership,
                ?deferSend: bool,
                ?byname: string,
                ?expectedToolCalls: int
            ) : Task<Result<ForkResult, string>> =
            let agentName = agent.Trim()

            let providerByname =
                match byname with
                | Some value when not (String.IsNullOrWhiteSpace value) -> value.Trim()
                | _ -> agentName

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
                        enrichFirstPrompt this role prompt payload renderedPrompt
                    else
                        Task.FromResult(Ok([], prompt))

                match enrichedResult with
                | Error err -> return Error err
                | Ok(requirements, enrichedPrompt) ->
                    return!
                        forkAfterEnrich
                            this
                            agentId
                            role
                            agentName
                            providerByname
                            prompt
                            handleOwnership
                            isFirstPrompt
                            deferSend
                            expectedToolCalls
                            retired
                            existing
                            requirements
                            enrichedPrompt
            }

        member this.Reuse
            (agentId: string, prompt: string, ?renderedPrompt: string, ?expectedToolCalls: int)
            : Task<Result<ForkResult, string>> =
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

                let existing = this.TryReusableChild agentId

                match abandoned, existing with
                | Some true, _ -> return Error(sprintf "RetiredHandle: %s" agentId)
                | _, None -> return Error(sprintf "Unknown agent id: %s" agentId)
                | _, Some(childId, wasDormant) ->
                    return! reuseLiveChild this agentId childId wasDormant prompt renderedPrompt expectedToolCalls
            }
