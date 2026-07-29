namespace Wanxiangshu.Next.OpenCode

open System
open Thoth.Json
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Session

/// join() waits for the owning runtime's next physical completion. Orchestrator
/// join is routed to its ManagerJob publication mailbox by authority role.
module JoinTool =

    let private optionalString value = value |> Option.map Encode.string |> Option.defaultValue Encode.nil

    let private workRecord value =
        value
        |> Option.map (fun record ->
            Encode.object
                [ "text", Encode.string record.Text
                  "digest", Encode.string record.Digest
                  "freshness", Encode.string record.Freshness
                  "coveredThrough", optionalString record.CoveredThrough ])
        |> Option.defaultValue Encode.nil

    let private errorCode = function
        | ForkError.NothingToJoin -> "NOTHING_TO_JOIN"
        | ForkError.Cancelled -> "CANCELLED"
        | ForkError.Empty -> "EMPTY"
        | ForkError.NotFound id -> "NOT_FOUND:" + id

    let private encodeCompletion (runtime: HostForkRuntime) (completion: RunCompletion) =
        let isPty = runtime.IsPtyCompletion completion.RunId

        let value =
            match completion.Outcome with
            | AgentCompleted payload when isPty ->
                Encode.object
                    [ "kind", Encode.string "pty"
                      "status", Encode.string "completed"
                      "agentId", Encode.string payload.AgentId
                      "runId", Encode.string payload.RunId
                      "finalText", Encode.string payload.FinalText
                      "outcome", Encode.string payload.FinalText
                      "closed", Encode.bool true
                      "ptyId", Encode.string completion.RunId ]
            | AgentCompleted payload ->
                let baseFields =
                    [ "kind", Encode.string "agent"
                      "status", Encode.string "completed"
                      "agentId", Encode.string payload.AgentId
                      "childSessionId", Encode.string payload.ChildSessionId
                      "runId", Encode.string payload.RunId
                      "rootUserMessageId", Encode.string payload.RootUserMessageId
                      "assistantMessageId", Encode.string payload.AssistantMessageId
                      "finalText", Encode.string payload.FinalText
                      "workRecord", workRecord payload.WorkRecord
                      "directory", Encode.string payload.Directory ]

                let managed =
                    runtime.TryFindAgent payload.AgentId
                    |> Option.bind (fun record -> ManagedAgent.tryParse record.Agent)

                let identityFields =
                    match managed with
                    | Some agent ->
                        [ "agent", Encode.string agent.Name
                          "role", Encode.string (ManagedAgent.roleName agent.Role)
                          "tier", Encode.string (ManagedAgent.tierName agent.Tier)
                          "fallbackPeer", Encode.string (ManagedAgent.peer agent).Name ]
                    | None -> [ "role", Encode.string (payload.Role.ToString().ToLowerInvariant()) ]

                Encode.object (baseFields @ identityFields)
            | AgentFailed payload
            | AgentAborted payload when isPty ->
                Encode.object
                    [ "kind", Encode.string "pty"
                      "status", Encode.string "failed"
                      "agentId", Encode.string payload.AgentId
                      "runId", Encode.string payload.RunId
                      "outcome", Encode.string payload.Message
                      "closed", Encode.bool true
                      "error",
                      Encode.object [ "code", Encode.string payload.Code; "message", Encode.string payload.Message ]
                      "ptyId", Encode.string completion.RunId ]
            | AgentFailed payload
            | AgentAborted payload ->
                let status =
                    match completion.Outcome with
                    | AgentAborted _ -> "aborted"
                    | _ -> "failed"

                Encode.object
                    [ "kind", Encode.string "agent"
                      "status", Encode.string status
                      "agentId", Encode.string payload.AgentId
                      "childSessionId", optionalString payload.ChildSessionId
                      "runId", Encode.string payload.RunId
                      "role",
                      payload.Role
                      |> Option.map (fun role -> Encode.string (role.ToString().ToLowerInvariant()))
                      |> Option.defaultValue Encode.nil
                      "error",
                      Encode.object [ "code", Encode.string payload.Code; "message", Encode.string payload.Message ] ]

        Encode.toString 0 value

    let private execute (scope: ToolRuntimeScope) (_args: HostToolArguments) context =
        task {
            if scope.IsRole(context, Role.Orchestrator) then
                let! verdict = scope.OrchestratorHostFor(context.SessionId).JoinPublished()
                return ToolHostCodec.jsonObject [ "outcome", Encode.string verdict ]
            else
                match scope.RuntimeFor context with
                | Error runtimeError ->
                    return ToolHostCodec.jsonObject [ "error", Encode.string runtimeError ]
                | Ok runtime ->
                    let detachAbort = context.AttachAbort(fun () -> runtime.Cancel())

                    use _cleanup =
                        { new IDisposable with
                            member _.Dispose() = detachAbort () }

                    match! runtime.Join() with
                    | Ok completion -> return encodeCompletion runtime completion
                    | Error joinError ->
                        return
                            ToolHostCodec.jsonObject
                                [ "error",
                                  Encode.object
                                      [ "code", Encode.string (errorCode joinError)
                                        "message", Encode.string (joinError.ToString()) ] ]
        }

    let spec scope =
        { Name = "join"
          Description = "Wait for any agent or PTY completion"
          Arguments = []
          Execute = execute scope }
