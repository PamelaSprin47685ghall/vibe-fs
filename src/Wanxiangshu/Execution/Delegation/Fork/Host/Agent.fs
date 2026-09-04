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
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.OpenCode

/// Existing-child send must use the session's bound managed agent.
/// Never invent a managed name from CanonicalRole; never let the caller overwrite the bound agent.
module HostForkBinding =
    let private tryName (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Some(value.Trim())

    let managedAgent (journal: AgentJournal option) (childId: SessionId) : string option =
        journal
        |> Option.bind (fun durable ->
            let projections = (AgentJournal.snapshot durable).AgentProjections

            PromptAuthorityLedger.activeProfile childId projections
            |> Option.orElseWith (fun () -> PromptAuthorityLedger.lastAuthorityProfile childId projections)
            |> Option.bind (fun profile -> tryName profile.SelectedAgent))

[<AutoOpen>]
module HostForkAgent =

    let private forkInstructions (sessionId: SessionId) : ForkChildInstructions =
        let lang = SessionProviderLanguage.languageOf sessionId

        { Base = ProviderProse.instructionLines lang ForkChildPayload.BasePath Map.empty
          CommissionerRecord = ProviderProse.render lang ForkChildPayload.CommissionerRecordPath Map.empty
          Attachment = ProviderProse.render lang ForkChildPayload.AttachmentPath Map.empty
          Requirements = ProviderProse.render lang ForkChildPayload.RequirementsPath Map.empty }

    let private buildRelayEnvelope
        (runtime: HostForkRuntime)
        (_role: Role)
        (prompt: string)
        (payload: string option)
        : Task<Result<string list * string, string>> =
        task {
            let! parentWorkRecord = runtime.ParentWorkRecordOf runtime.ParentId

            let requirements = []

            return
                Ok(
                    requirements,
                    ForkChildPayload.relay
                        (forkInstructions runtime.ParentId)
                        prompt
                        parentWorkRecord
                        None
                        requirements
                        payload
                )
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
        (identitySeed: PromptAuthority.IdentitySeed)
        (enrichedPrompt: string)
        (result: ForkResult)
        : Task<Result<ForkResult, string>> =
        task {
            let! sent =
                HostForkAgentOwner.sendFirstPromptObserved
                    runtime.Sessions
                    runtime.Journal
                    childId
                    identitySeed
                    (runtime.DirectoryOf agentId)
                    enrichedPrompt
                    (HostForkRunLifecycle.bindAuthorityRoot run)

            match sent with
            | HostForkRunLifecycle.AgentOwnerDispatchOutcome.Accepted -> return Ok result
            | HostForkRunLifecycle.AgentOwnerDispatchOutcome.AcceptanceUncertain _ ->
                return Ok(ForkResult.DispatchUncertain result.AgentId)
            | HostForkRunLifecycle.AgentOwnerDispatchOutcome.Rejected err ->
                do! runtime.FailRun(run, err)
                return Error err
        }

    let private finishSuccessfulNewChild
        (runtime: HostForkRuntime)
        (agentId: string)
        (childId: SessionId)
        (identitySeed: PromptAuthority.IdentitySeed)
        (prompt: string)
        (requirements: string list)
        (enrichedPrompt: string)
        (isFirstPrompt: bool)
        (deferSend: bool)
        (expectedToolCalls: int option)
        (run: PendingHostRun)
        (result: ForkResult)
        : Task<Result<ForkResult, string>> =
        taskResult {
            runtime.MarkReady(run)

            // COMPANION-003 / EXEC-006: the child's OpeningMaterial is the ORIGINAL
            // fork assignment and authoritative requirements, NOT the rendered
            // envelope (which carries commissioner_record and would nest the
            // parent LWR recursively). Captured before the first prompt is sent;
            // idempotent.
            if isFirstPrompt then
                let! _ =
                    XTraceCapture.captureOpeningWithReceipt runtime.Journal childId prompt requirements
                    |> TaskResult.mapError (fun error -> sprintf "fork opening trace capture failed: %A" error)

                ()

            do!
                maybeReplaceToolEstimate runtime.Journal expectedToolCalls childId
                |> TaskResultCE.ofTask

            if deferSend && isFirstPrompt then
                runtime.DeferredFirstPrompts.[agentId] <-
                    {| ChildId = childId
                       IdentitySeed = identitySeed
                       Prompt = enrichedPrompt |}

                return result
            else
                return! sendFirstPromptOutcome runtime run agentId childId identitySeed enrichedPrompt result
        }

    let private afterRuntimeFork
        (runtime: HostForkRuntime)
        (agentId: string)
        (childId: SessionId)
        (identitySeed: PromptAuthority.IdentitySeed)
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
                identitySeed
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
        (identitySeed: PromptAuthority.IdentitySeed)
        (prompt: string)
        (requirements: string list)
        (enrichedPrompt: string)
        (isFirstPrompt: bool)
        (deferSend: bool)
        (expectedToolCalls: int option)
        (preparedHandoff: PreparedDelegationHandoff option)
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
                let run =
                    runtime.InstallRun(agentId, childId, role, ?preparedHandoff = preparedHandoff)

                lock runtime.Gate (fun () -> runtime.Children.[agentId] <- childId)

                runtime.ChildCreated agentId role childId
                runtime.ChildCreatedDir agentId childId (runtime.DirectoryOf agentId)

                // GLORY-033: the fork surface no longer opens barriers. An
                // incumbent cannot fork an unauthorized role, and every
                // Host-owned barrier opens at its dedicated site.
                let result =
                    runtime.Runtime.Fork(agentId, role, agentName, runWork = (fun () -> run.Source.Task))

                return!
                    afterRuntimeFork
                        runtime
                        agentId
                        childId
                        identitySeed
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
        (preparedHandoff: PreparedDelegationHandoff option)
        : Task<Result<ForkResult, string>> =
        let interpretChildResult =
            function
            | Error error -> Task.FromResult(Error error)
            | Ok(identitySeed, childId) ->
                task {
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
                            identitySeed
                            prompt
                            requirements
                            enrichedPrompt
                            isFirstPrompt
                            deferSend
                            expectedToolCalls
                            preparedHandoff
                            childId
                            linkageResult
                }

        task {
            let! childResult =
                taskResult {
                    let! identitySeed =
                        HostForkRunLifecycle.issueCurrentOwnerIdentitySeed runtime.Journal runtime.ParentId agentName

                    let! childId =
                        runtime.Sessions.CreateChildSession(
                            runtime.ParentId,
                            { Title = Some agentId
                              Agent = Some agentName
                              Directory = runtime.DirectoryOf agentId }
                        )

                    return identitySeed, childId
                }

            return! interpretChildResult childResult
        }

    let private forkExistingChild
        (runtime: HostForkRuntime)
        (agentId: string)
        (childId: SessionId)
        (role: Role)
        (prompt: string)
        (enrichedPrompt: string)
        (isFirstPrompt: bool)
        (expectedToolCalls: int option)
        (preparedHandoff: PreparedDelegationHandoff option)
        : Task<Result<ForkResult, string>> =
        task {
            match HostForkBinding.managedAgent runtime.Journal childId with
            | None -> return Error(sprintf "Agent handle '%s' has no active managed agent identity" agentId)
            | Some sendAgent ->
                do! maybeReplaceToolEstimate runtime.Journal expectedToolCalls childId

                return!
                    HostForkChildDispatch.sendToExistingChild
                        runtime.Gate
                        runtime.PendingRuns
                        runtime.Journal
                        runtime.ParentId
                        runtime.Sessions
                        runtime.ChildWorkRecordOfRun
                        runtime.XTraceHead
                        runtime.TrackOwnedWork
                        runtime.Runtime
                        runtime.HandoffPort
                        runtime.SendChildPrompt
                        runtime.SendBusyNudge
                        (fun child role -> runtime.RunStarted child role (runtime.DirectoryOf agentId))
                        preparedHandoff
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
        (preparedHandoff: PreparedDelegationHandoff option)
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
                prompt
                enrichedPrompt
                isFirstPrompt
                expectedToolCalls
                preparedHandoff
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
                preparedHandoff

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
        (preparedHandoff: PreparedDelegationHandoff option)
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
                        runtime.ChildWorkRecordOfRun
                        runtime.XTraceHead
                        runtime.TrackOwnedWork
                        runtime.Runtime
                        runtime.HandoffPort
                        runtime.SendChildPrompt
                        runtime.SendBusyNudge
                        (fun child role -> runtime.RunStarted child role (runtime.DirectoryOf agentId))
                        preparedHandoff
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
        (preparedHandoff: PreparedDelegationHandoff option)
        : Task<Result<ForkResult, string>> =
        task {
            let providerBynameOpt =
                runtime.Journal
                |> Option.bind (fun durable ->
                    AgentJournal.handleProjection durable runtime.ParentId
                    |> HandleProjection.tryFind (HandleController.agentHandle agentId))
                |> Option.map (fun handle -> handle.Byname)
                |> Option.filter (String.IsNullOrWhiteSpace >> not)

            match providerBynameOpt with
            | None -> return Error(sprintf "Agent handle '%s' has no provider byname" agentId)
            | Some providerByname ->
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
                    reuseAfterRelink
                        runtime
                        agentId
                        childId
                        role
                        agentName
                        prompt
                        renderedPrompt
                        wasDormant
                        preparedHandoff
                        linkResult
        }

    let private reuseLiveChild
        (runtime: HostForkRuntime)
        (agentId: string)
        (childId: SessionId)
        (wasDormant: bool)
        (prompt: string)
        (renderedPrompt: string option)
        (expectedToolCalls: int option)
        (preparedHandoff: PreparedDelegationHandoff option)
        : Task<Result<ForkResult, string>> =
        task {
            let recordOpt =
                runtime.Runtime.List()
                |> fst
                |> List.tryFind (fun agent -> agent.AgentId = agentId)

            let boundAgent = HostForkBinding.managedAgent runtime.Journal childId

            match recordOpt, boundAgent with
            | None, _ -> return Error(sprintf "Unknown agent id: %s" agentId)
            | _, None -> return Error(sprintf "Agent handle '%s' has no active managed agent identity" agentId)
            | Some record, Some agentName ->
                do! maybeReplaceToolEstimate runtime.Journal expectedToolCalls childId

                return!
                    reuseWithManagedAgent
                        runtime
                        agentId
                        childId
                        record.Role
                        agentName
                        prompt
                        renderedPrompt
                        wasDormant
                        preparedHandoff
        }

    type HostForkRuntime with

        /// Current durable Authority Root binding for an already-linked child.
        member this.BoundManagedAgent(childId: SessionId) : string option =
            HostForkBinding.managedAgent this.Journal childId

        /// PROMPT-008: `agent` is the managed agent name the caller selected, and
        /// it is required. It is never defaulted, and the selected name travels to the
        /// Host send boundary as chosen.
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
                ?expectedToolCalls: int,
                ?preparedHandoff: PreparedDelegationHandoff
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
                // the envelope cannot tell which path produced the request.
                // Continuations (busy nudge, nudge, manager resume) opt out.
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
                            preparedHandoff
                            retired
                            existing
                            requirements
                            enrichedPrompt
            }

        member this.Reuse
            (
                agentId: string,
                prompt: string,
                ?renderedPrompt: string,
                ?expectedToolCalls: int,
                ?preparedHandoff: PreparedDelegationHandoff
            ) : Task<Result<ForkResult, string>> =
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
                let active = lock this.Gate (fun () -> this.PendingRuns.ContainsKey agentId)

                match active, abandoned, existing with
                | true, _, _ -> return Error(sprintf "Agent already has an active assignment: %s" agentId)
                | _, Some true, _ -> return Error(sprintf "RetiredHandle: %s" agentId)
                | _, _, None -> return Error(sprintf "Unknown agent id: %s" agentId)
                | _, _, Some(childId, wasDormant) ->
                    return!
                        reuseLiveChild
                            this
                            agentId
                            childId
                            wasDormant
                            prompt
                            renderedPrompt
                            expectedToolCalls
                            preparedHandoff
            }
