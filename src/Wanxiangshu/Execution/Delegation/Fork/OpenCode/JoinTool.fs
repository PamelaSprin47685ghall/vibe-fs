namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// join() waits for the owning runtime's next physical completion batch.
/// Orchestrator join routes to ManagerJob verdict mailbox by authority role.
/// P0-RECOVERY-JOIN-001: FamilyReady permit → Join.joinAvailable (no bare Join, no AST).
/// EXEC-017: tool abort → JoinInterrupt.Signal only (≠ runtime.Cancel).
/// DevOps join: 10s timeout budget (PtyTiming.timerTask 10000). Orch/Manager join remains untimed.
module JoinTool =

    [<Literal>]
    let DevOpsJoinTimeoutMs = 10_000

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/join/description"

        [<Literal>]
        let RecoveryBlocked = "tool/join/recovery-blocked"

        [<Literal>]
        let RecoveryWaiting = "tool/join/recovery-waiting"

        [<Literal>]
        let UnavailableUntilAuthority = "tool/join/unavailable-until-authority"

        [<Literal>]
        let OrchestratorNotReady = "tool/join/orchestrator-not-ready"

        [<Literal>]
        let UnavailableFromContext = "tool/join/unavailable-from-context"

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private consequence lines =
        ToolHostCodec.tomlObjectWithInstructions lines []

    let private recoveryBlocked language (_blocks: NonEmpty<RecoveryBlock>) =
        consequence (ProviderProse.instructionLines language Path.RecoveryBlocked Map.empty)

    let private renderOrchestratorOutcome language outcome =
        match outcome with
        | Error _ -> consequence (ProviderProse.instructionLines language Path.OrchestratorNotReady Map.empty)
        | Ok(Interrupted reason) -> JoinResultRenderer.renderInterrupted language reason
        | Ok(ResultsAvailable batch) -> JoinResultRenderer.renderOrchestratorBatch language batch

    let private executeOrchestratorJoin
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        language
        (sessionId: SessionId)
        (attempt: JoinAttemptLease)
        =
        task {
            let joinDescriptor =
                DiagnosticWait.create
                    "orchestrator-join"
                    (CausalOwner.create "OrchestratorWorkflow" [ "session", SessionId.value sessionId ])
                    [ "session", SessionId.value sessionId; "tool", "join" ]
                    (WorkflowProducer(CausalOwner.create "ManagerWorkflow" []))
                    [ WaitEscape.CancelledBy(CausalOwner.create "JoinAttempt" [ "session", SessionId.value sessionId ])
                      WaitEscape.SessionLifetime ]
                    "JoinTool.Orchestrator.JoinPublishedAvailable"

            let! outcome =
                CausalAwait.awaitTask
                    CausalWaitHub.observer
                    joinDescriptor
                    (scope
                        .OrchestratorHostFor(context.SessionId)
                        .JoinPublishedAvailable(JoinBatch.Max, attempt.Wait))

            return renderOrchestratorOutcome language outcome
        }

    let private devopsOrPlainWait (isDevOps: bool) (attemptWait: Task<JoinInterruptReason>) =
        if isDevOps then
            let timerTask = PtyTiming.timerTask DevOpsJoinTimeoutMs

            emitJsExpr
                (attemptWait, emitJsExpr timerTask "$0.then(function(){return'DeadlineExpired';})")
                "Promise.race([$0,$1])"
        else
            attemptWait

    let private joinTaskForMembership
        (runtime: HostForkRuntime)
        (permit: FamilyRecoveryPermit)
        (waitTask: Task<JoinInterruptReason>)
        (fissionMembership: (string * int) option)
        =
        match fissionMembership with
        | Some(groupId, laneIndex) ->
            HostForkJoin.joinAvailableForFissionLane runtime groupId laneIndex JoinBatch.Max waitTask
        | None -> Join.joinAvailable runtime permit JoinBatch.Max waitTask

    let private liveAgentName (runtime: HostForkRuntime) (agentId: string) =
        match runtime.TryFindAgent agentId with
        | Some record -> record.Agent
        | None -> ""

    let private resolveAgentName
        (scope: ToolRuntimeScope)
        (root: SessionId)
        (runtime: HostForkRuntime)
        (agentId: string)
        =
        let durableByname =
            scope.Journal
            |> Option.bind (fun journal ->
                AgentJournal.handleProjection journal root
                |> HandleProjection.tryFind (HandleController.agentHandle agentId))
            |> Option.map (fun handle -> handle.Byname)
            |> Option.filter (String.IsNullOrWhiteSpace >> not)

        match durableByname with
        | Some byname -> byname.Trim()
        | None -> liveAgentName runtime agentId

    let private terminalNameFromMap (runtime: HostForkRuntime) (ptyId: string) =
        lock runtime.Gate (fun () ->
            runtime.TerminalByName
            |> Seq.tryPick (fun (KeyValue(name, id)) -> if id = ptyId then Some name else None))
        |> Option.defaultValue ""

    let private resolveTerminalLabel (runtime: HostForkRuntime) (ptyId: string) =
        let _, ptys = runtime.List()

        match ptys |> List.tryFind (fun record -> record.PtyId = ptyId) with
        | Some record when not (String.IsNullOrWhiteSpace record.Command) -> record.Command.Trim()
        | _ -> terminalNameFromMap runtime ptyId

    let private renderInterruptedReason language (reason: JoinInterruptReason) =
        match reason with
        | JoinInterruptReason.OperatorAbort
        | JoinInterruptReason.UserMessageArrived -> JoinResultRenderer.renderInterrupted language reason
        | JoinInterruptReason.DeadlineExpired ->
            JoinResultRenderer.renderInterrupted language JoinInterruptReason.DeadlineExpired

    let private untrackJoinItem (runtime: HostForkRuntime) (item: JoinItem) =
        match item with
        | JoinItem.PtyItem ptyItem -> runtime.UntrackPtyRun(PtyJoinItem.ptyId ptyItem)
        | JoinItem.AgentItem _ -> ()

    let private releasePtyTracks (runtime: HostForkRuntime) (batch: NonEmptyBatch<JoinItem>) =
        NonEmptyBatch.toList batch |> List.iter (untrackJoinItem runtime)

    let private renderJoined
        language
        (runtime: HostForkRuntime)
        (resolveName: string -> string)
        (resolveLabel: string -> string)
        (joined: Result<JoinWaitOutcome<JoinItem>, ForkError>)
        =
        match joined with
        | Ok(Interrupted reason) -> renderInterruptedReason language reason
        | Ok(ResultsAvailable batch) ->
            // Render before releasing names: this Join result is the moment the
            // old terminal ending becomes heard.
            let rendered =
                JoinResultRenderer.renderJoinItemBatch language resolveName batch resolveLabel

            releasePtyTracks runtime batch
            rendered
        | Error joinError -> JoinResultRenderer.renderForkError language joinError resolveName

    let private executeAgentWithRuntime
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        language
        (sessionId: SessionId)
        (attempt: JoinAttemptLease)
        (permit: FamilyRecoveryPermit)
        (runtime: HostForkRuntime)
        =
        task {
            let isDevOps = scope.IsRole(context, Role.DevOps)
            let waitTask = devopsOrPlainWait isDevOps attempt.Wait

            let joinDescriptor =
                DiagnosticWait.create
                    "agent-join"
                    (CausalOwner.create "JoinTool" [ "session", SessionId.value sessionId ])
                    [ "session", SessionId.value sessionId; "tool", "join" ]
                    (ExternalProducer("child-completion", [ "session", SessionId.value sessionId ]))
                    [ WaitEscape.CancelledBy(CausalOwner.create "JoinAttempt" [ "session", SessionId.value sessionId ])
                      WaitEscape.SessionLifetime ]
                    "JoinTool.joinAvailable"

            // Only process-local Fission bindings are active. A durable Open
            // group from a previous process is a broken tool record, not an
            // implicit lane to resume.
            let fissionMembership =
                FissionRuntime.tryLane sessionId
                |> Option.map (fun binding -> binding.GroupId, binding.LaneIndex)

            let joinTask = joinTaskForMembership runtime permit waitTask fissionMembership
            let! joined = CausalAwait.awaitTask CausalWaitHub.observer joinDescriptor joinTask
            let root = scope.LogicalOwnerFor sessionId

            return
                renderJoined
                    language
                    runtime
                    (resolveAgentName scope root runtime)
                    (resolveTerminalLabel runtime)
                    joined
        }

    let private executeAgentJoin
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        language
        (sessionId: SessionId)
        (attempt: JoinAttemptLease)
        (permit: FamilyRecoveryPermit)
        =
        task {
            match scope.RuntimeFor context with
            | Error _ ->
                return consequence (ProviderProse.instructionLines language Path.UnavailableFromContext Map.empty)
            | Ok runtime -> return! executeAgentWithRuntime scope context language sessionId attempt permit runtime
        }

    let private executeWhenReady
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        language
        (sessionId: SessionId)
        (attempt: JoinAttemptLease)
        (permit: FamilyRecoveryPermit)
        =
        // NEVER Cancel mailbox/runtime on user wake (EXEC-017); the attempt's
        // Wait is the only interrupt channel. Completion still beats interrupt.
        if scope.IsRole(context, Role.Orchestrator) then
            executeOrchestratorJoin scope context language sessionId attempt
        else
            executeAgentJoin scope context language sessionId attempt permit

    let private executeAfterSession
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        language
        (sessionId: SessionId)
        =
        task {
            // EXEC-017: Begin the attempt first — before RequireFamilyRecovery and
            // before the mailbox wait — so a user-message signal that lands while
            // recovery or setup is still running is recorded on THIS attempt's own
            // TCS. There is no session-level future latch; Dispose unregisters.
            let attempt = scope.JoinAttempts.Begin(sessionId, context.ToolCallId)
            let detachAbort = context.AttachAbort attempt.SignalOperatorAbort

            use _attempt = attempt

            use _cleanup =
                { new IDisposable with
                    member _.Dispose() = detachAbort () }

            let root = scope.LogicalOwnerFor sessionId
            let! recovery = scope.RequireFamilyRecovery root

            match recovery with
            | FamilyRecovery.FamilyBlocked blocks -> return recoveryBlocked language blocks
            | FamilyRecovery.FamilyWaiting _ ->
                // EXEC-023: no permit while waiting — must not drain durable agent
                // finals via bare JoinAvailable. Surface retryable RECOVERY_WAITING
                // so Manager re-invokes join after RestoreHandles advances to Ready
                // or Blocked. Align ExecutorTool FamilyWaiting → RECOVERY_WAITING.
                return consequence (ProviderProse.instructionLines language Path.RecoveryWaiting Map.empty)
            | FamilyRecovery.FamilyReady permit ->
                return! executeWhenReady scope context language sessionId attempt permit
        }

    let private execute (scope: ToolRuntimeScope) (_args: HostToolArguments) (context: HostToolContext) =
        task {
            let language = lang context

            if String.IsNullOrWhiteSpace context.SessionId then
                return consequence (ProviderProse.instructionLines language Path.UnavailableUntilAuthority Map.empty)
            else
                return! executeAfterSession scope context language (SessionId.create context.SessionId)
        }

    let spec scope =
        { Name = "join"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = []
          Execute = execute scope }
