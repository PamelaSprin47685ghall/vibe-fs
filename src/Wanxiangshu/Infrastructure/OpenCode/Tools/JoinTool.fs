namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Process
open Wanxiangshu.Session

open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Process
open Wanxiangshu.Session

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

    let private execute (scope: ToolRuntimeScope) (_args: HostToolArguments) (context: HostToolContext) =
        task {
            let language = lang context

            if String.IsNullOrWhiteSpace context.SessionId then
                return consequence (ProviderProse.instructionLines language Path.UnavailableUntilAuthority Map.empty)
            else
                let sessionId = SessionId.create context.SessionId

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
                    // NEVER Cancel mailbox/runtime on user wake (EXEC-017); the attempt's
                    // Wait is the only interrupt channel. Completion still beats interrupt.
                    if scope.IsRole(context, Role.Orchestrator) then
                        let joinDescriptor =
                            DiagnosticWait.create
                                "orchestrator-join"
                                (CausalOwner.create "OrchestratorWorkflow" [ "session", SessionId.value sessionId ])
                                [ "session", SessionId.value sessionId; "tool", "join" ]
                                (WorkflowProducer(CausalOwner.create "ManagerWorkflow" []))
                                [ WaitEscape.CancelledBy(
                                      CausalOwner.create "JoinAttempt" [ "session", SessionId.value sessionId ]
                                  )
                                  WaitEscape.SessionLifetime ]
                                "JoinTool.Orchestrator.JoinPublishedAvailable"

                        let! outcome =
                            CausalAwait.awaitTask
                                CausalWaitHub.observer
                                joinDescriptor
                                (scope
                                    .OrchestratorHostFor(context.SessionId)
                                    .JoinPublishedAvailable(JoinBatch.Max, attempt.Wait))

                        match outcome with
                        | Error _ ->
                            return
                                consequence (
                                    ProviderProse.instructionLines language Path.OrchestratorNotReady Map.empty
                                )
                        | Ok(Interrupted reason) -> return JoinResultRenderer.renderInterrupted language reason
                        | Ok(ResultsAvailable batch) -> return JoinResultRenderer.renderOrchestratorBatch language batch
                    else
                        match scope.RuntimeFor context with
                        | Error _ ->
                            return
                                consequence (
                                    ProviderProse.instructionLines language Path.UnavailableFromContext Map.empty
                                )
                        | Ok runtime ->
                            let isDevOps = scope.IsRole(context, Role.DevOps)

                            let waitTask: Task<JoinInterruptReason> =
                                if isDevOps then
                                    let timerTask = PtyTiming.timerTask DevOpsJoinTimeoutMs

                                    emitJsExpr
                                        (attempt.Wait,
                                         emitJsExpr timerTask "$0.then(function(){return'DeadlineExpired';})")
                                        "Promise.race([$0,$1])"
                                else
                                    attempt.Wait

                            let joinDescriptor =
                                DiagnosticWait.create
                                    "agent-join"
                                    (CausalOwner.create "JoinTool" [ "session", SessionId.value sessionId ])
                                    [ "session", SessionId.value sessionId; "tool", "join" ]
                                    (ExternalProducer("child-completion", [ "session", SessionId.value sessionId ]))
                                    [ WaitEscape.CancelledBy(
                                          CausalOwner.create "JoinAttempt" [ "session", SessionId.value sessionId ]
                                      )
                                      WaitEscape.SessionLifetime ]
                                    "JoinTool.joinAvailable"

                            let fissionMembership =
                                FissionRuntime.tryLane sessionId
                                |> Option.map (fun binding -> binding.GroupId, binding.LaneIndex)
                                |> Option.orElseWith (fun () ->
                                    scope.Journal
                                    |> Option.bind (fun durable ->
                                        FissionProjection.tryMembershipOfLane
                                            sessionId
                                            (AgentJournal.snapshot durable).AgentProjections.Fission
                                        |> Option.map (fun (group, laneIndex) -> group.GroupId, laneIndex)))

                            let joinTask =
                                match fissionMembership with
                                | Some(groupId, laneIndex) ->
                                    HostForkJoin.joinAvailableForFissionLane
                                        runtime
                                        groupId
                                        laneIndex
                                        JoinBatch.Max
                                        waitTask
                                | None -> Join.joinAvailable runtime permit JoinBatch.Max waitTask

                            let! joined =
                                CausalAwait.awaitTask CausalWaitHub.observer joinDescriptor joinTask

                            let resolveAgentName agentId =
                                let durableByname =
                                    scope.Journal
                                    |> Option.bind (fun journal ->
                                        AgentJournal.handleProjection journal root
                                        |> HandleProjection.tryFind (HandleController.agentHandle agentId))
                                    |> Option.map (fun handle -> handle.Byname)
                                    |> Option.filter (String.IsNullOrWhiteSpace >> not)

                                match durableByname with
                                | Some byname -> byname.Trim()
                                | None ->
                                    match runtime.TryFindAgent agentId with
                                    | Some record -> record.Agent
                                    | None -> ""

                            let resolveTerminalLabel ptyId =
                                let _, ptys = runtime.List()

                                match ptys |> List.tryFind (fun record -> record.PtyId = ptyId) with
                                | Some record when not (String.IsNullOrWhiteSpace record.Command) ->
                                    record.Command.Trim()
                                | _ ->
                                    lock runtime.Gate (fun () ->
                                        runtime.TerminalByName
                                        |> Seq.tryPick (fun (KeyValue(name, id)) ->
                                            if id = ptyId then Some name else None))
                                    |> Option.defaultValue ""

                            match joined with
                            | Ok(Interrupted reason) ->
                                match reason with
                                | JoinInterruptReason.OperatorAbort
                                | JoinInterruptReason.UserMessageArrived ->
                                    return JoinResultRenderer.renderInterrupted language reason
                                | JoinInterruptReason.DeadlineExpired ->
                                    return
                                        JoinResultRenderer.renderInterrupted
                                            language
                                            JoinInterruptReason.DeadlineExpired
                            | Ok(ResultsAvailable batch) ->
                                // Render before releasing names: this Join result is the
                                // moment the old terminal ending becomes heard.
                                let rendered =
                                    JoinResultRenderer.renderJoinItemBatch
                                        language
                                        resolveAgentName
                                        batch
                                        resolveTerminalLabel

                                NonEmptyBatch.toList batch
                                |> List.iter (function
                                    | JoinItem.PtyItem item -> runtime.UntrackPtyRun(PtyJoinItem.ptyId item)
                                    | JoinItem.AgentItem _ -> ())

                                return rendered
                            | Error joinError ->
                                return JoinResultRenderer.renderForkError language joinError resolveAgentName
        }

    let spec scope =
        { Name = "join"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = []
          Execute = execute scope }
