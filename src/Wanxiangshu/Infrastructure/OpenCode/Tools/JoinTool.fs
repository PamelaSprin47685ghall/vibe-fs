namespace Wanxiangshu.OpenCode

open System

open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// join() waits for the owning runtime's next physical completion batch.
/// Orchestrator join routes to ManagerJob verdict mailbox by authority role.
/// P0-RECOVERY-JOIN-001: FamilyReady permit → Join.joinAvailable (no bare Join, no AST).
/// EXEC-017: tool abort → JoinInterrupt.Signal only (≠ runtime.Cancel).
module JoinTool =

    let private recoveryBlocked (blocks: NonEmpty<RecoveryBlock>) =
        let head = blocks.Head

        let message =
            match head with
            | RecoveryBlock.RecoveryCoordinatorUnavailable sid ->
                sprintf "family recovery blocked: coordinator unavailable for %s" (SessionId.value sid)
            | RecoveryBlock.SnapshotUnreadable(sid, reason) ->
                sprintf "family recovery blocked: snapshot unreadable %s (%s)" (SessionId.value sid) reason
            | RecoveryBlock.MissingSession sid ->
                sprintf "family recovery blocked: missing session %s" (SessionId.value sid)
            | RecoveryBlock.LinkageConflict(parent, child) ->
                sprintf
                    "family recovery blocked: linkage conflict %s → %s"
                    (SessionId.value parent)
                    (SessionId.value child)
            | RecoveryBlock.RecoveryCycle _ -> "family recovery blocked: recovery cycle"
            | RecoveryBlock.PendingClaimUnknown(sid, _) ->
                sprintf "family recovery blocked: pending claim unknown %s" (SessionId.value sid)
            | RecoveryBlock.ChildRecoveryFailed(sid, reason) ->
                sprintf "family recovery blocked: child %s (%s)" (SessionId.value sid) reason

        let tString = ToolHostCodec.TString
        let tTable = ToolHostCodec.TTable

        ToolHostCodec.tomlObject [ "error", tTable [ "code", tString "RECOVERY_BLOCKED"; "message", tString message ] ]

    let private execute (scope: ToolRuntimeScope) (_args: HostToolArguments) (context: HostToolContext) =
        task {
            if String.IsNullOrWhiteSpace context.SessionId then
                return ToolHostCodec.tomlObject [ "error", ToolHostCodec.TString "Missing sessionID" ]
            else
                let root = SessionId.create context.SessionId
                let! recovery = scope.RequireFamilyRecovery root

                match recovery with
                | FamilyRecovery.FamilyBlocked blocks -> return recoveryBlocked blocks
                | FamilyRecovery.FamilyWaiting _ ->
                    // EXEC-023: no permit while waiting — must not drain durable agent
                    // finals via bare JoinAvailable. Surface retryable RECOVERY_WAITING
                    // so Manager re-invokes join after RestoreHandles advances to Ready
                    // or Blocked. Align ExecutorTool FamilyWaiting → RECOVERY_WAITING.
                    let tString = ToolHostCodec.TString
                    let tTable = ToolHostCodec.TTable

                    return
                        ToolHostCodec.tomlObject
                            [ "error",
                              tTable
                                  [ "code", tString "RECOVERY_WAITING"
                                    "message",
                                    tString "family recovery incomplete: wait for FamilyReady before join drain" ] ]
                | FamilyRecovery.FamilyReady permit ->
                    let interrupt = JoinInterrupt.create ()
                    let detachAbort = context.AttachAbort interrupt.Signal

                    use _cleanup =
                        { new IDisposable with
                            member _.Dispose() = detachAbort () }

                    if scope.IsRole(context, Role.Orchestrator) then
                        let! outcome =
                            scope
                                .OrchestratorHostFor(context.SessionId)
                                .JoinPublishedAvailable(JoinBatch.Max, interrupt.Wait)

                        match outcome with
                        | Error reason ->
                            return
                                ToolHostCodec.tomlObject
                                    [ "error", ToolHostCodec.TString(sprintf "Orchestrator init failed: %s" reason) ]
                        | Ok InterruptedByUserMessage -> return JoinResultRenderer.renderInterrupted ()
                        | Ok(ResultsAvailable batch) -> return JoinResultRenderer.renderOrchestratorBatch batch
                    else
                        match scope.RuntimeFor context with
                        | Error runtimeError ->
                            return ToolHostCodec.tomlObject [ "error", ToolHostCodec.TString runtimeError ]
                        | Ok runtime ->
                            let! joined = Join.joinAvailable runtime permit JoinBatch.Max interrupt.Wait

                            match joined with
                            | Ok InterruptedByUserMessage -> return JoinResultRenderer.renderInterrupted ()
                            | Ok(ResultsAvailable batch) ->
                                return
                                    JoinResultRenderer.renderJoinItemBatch
                                        (fun agentId ->
                                            match runtime.TryFindAgent agentId with
                                            | Some record -> record.Agent
                                            | None -> "")
                                        batch
                            | Error joinError -> return JoinResultRenderer.renderForkError joinError
        }

    let spec scope =
        { Name = "join"
          Description = "Wait for any agent or PTY completion"
          Arguments = []
          Execute = execute scope }
