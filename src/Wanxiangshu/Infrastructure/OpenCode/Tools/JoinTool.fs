namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// join() waits for the owning runtime's next physical completion. Orchestrator
/// join is routed to its ManagerJob publication mailbox by authority role.
module JoinTool =

    let private tString = ToolHostCodec.TString
    let private tBool = ToolHostCodec.TBool
    let private tTable = ToolHostCodec.TTable

    let private errorCode =
        function
        | ForkError.NothingToJoin -> "NOTHING_TO_JOIN"
        | ForkError.Cancelled -> "CANCELLED"
        | ForkError.Empty -> "EMPTY"
        | ForkError.Abandoned(id, reason) -> "ABANDONED:" + id + ":" + reason
        | ForkError.NotFound id -> "NOT_FOUND:" + id
        | ForkError.TimedOut -> "TIMED_OUT"
        | ForkError.TerminalMaterializationFailed id -> "TERMINAL_MATERIALIZATION_FAILED:" + id

    let private encodeCompletion (runtime: HostForkRuntime) (completion: RunCompletion) =
        let isPty = runtime.IsPtyCompletion completion.RunId

        match completion.Outcome with
        | AgentCompleted payload when isPty ->
            // EXEC-004: a PTY completion is not an LWR; it keeps its own minimal
            // schema (kind/status/outcome/closed/pty_id).
            ToolHostCodec.tomlObject
                [ "kind", tString "pty"
                  "status", tString "completed"
                  "outcome", tString payload.WorkRecord
                  "closed", tBool true
                  "pty_id", tString completion.RunId ]
        | AgentCompleted payload ->
            // EXEC-004: the success wire is status + agent + work_record. The
            // work record is the opaque final LWR — one value, no digest /
            // freshness / coverage metadata, no runtime-only identities.
            let managed =
                runtime.TryFindAgent payload.AgentId
                |> Option.bind (fun record -> ManagedAgent.tryParse record.Agent)

            let agentName =
                match managed with
                | Some agent -> agent.Name
                | None -> payload.AgentId

            ToolHostCodec.tomlObject
                [ "status", tString "completed"
                  "agent", tString agentName
                  "work_record", tString payload.WorkRecord ]
        | AgentFailed payload
        | AgentAborted payload when isPty ->
            ToolHostCodec.tomlObject
                [ "kind", tString "pty"
                  "status", tString "failed"
                  "outcome", tString payload.Message
                  "closed", tBool true
                  "error", tTable [ "code", tString payload.Code; "message", tString payload.Message ]
                  "pty_id", tString completion.RunId ]
        | AgentFailed payload
        | AgentAborted payload ->
            let status =
                match completion.Outcome with
                | AgentAborted _ -> "aborted"
                | _ -> "failed"

            // EXEC-004: the failure wire is status + agent + error. No runtime
            // identity fields reach the LLM.
            let managed =
                runtime.TryFindAgent payload.AgentId
                |> Option.bind (fun record -> ManagedAgent.tryParse record.Agent)

            let agentName =
                match managed with
                | Some agent -> agent.Name
                | None -> payload.AgentId

            ToolHostCodec.tomlObject
                [ "status", tString status
                  "agent", tString agentName
                  "error", tTable [ "code", tString payload.Code; "message", tString payload.Message ] ]

    let private execute (scope: ToolRuntimeScope) (_args: HostToolArguments) context =
        task {
            if scope.IsRole(context, Role.Orchestrator) then
                let! verdict = scope.OrchestratorHostFor(context.SessionId).JoinPublished()
                return ToolHostCodec.tomlObject [ "outcome", tString verdict ]
            else
                match scope.RuntimeFor context with
                | Error runtimeError -> return ToolHostCodec.tomlObject [ "error", tString runtimeError ]
                | Ok runtime ->
                    let detachAbort = context.AttachAbort(fun () -> runtime.Cancel())

                    use _cleanup =
                        { new IDisposable with
                            member _.Dispose() = detachAbort () }

                    match! runtime.Join() with
                    | Ok completion -> return encodeCompletion runtime completion
                    | Error joinError ->
                        return
                            ToolHostCodec.tomlObject
                                [ "error",
                                  tTable
                                      [ "code", tString (errorCode joinError)
                                        "message", tString (joinError.ToString()) ] ]
        }

    let spec scope =
        { Name = "join"
          Description = "Wait for any agent or PTY completion"
          Arguments = []
          Execute = execute scope }
