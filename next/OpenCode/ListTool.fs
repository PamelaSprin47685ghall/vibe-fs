namespace Wanxiangshu.Next.OpenCode

open System
open Thoth.Json
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// list() is a pure projection of the owning ForkRuntime's agent and PTY maps.
module ListTool =

    let private optionalString value =
        value |> Option.map Encode.string |> Option.defaultValue Encode.nil

    let private agentEntry (record: AgentRecord) =
        let baseFields =
            [ "kind", Encode.string "agent"
              "agentId", Encode.string record.AgentId
              "childSessionId", optionalString (record.ChildSessionId |> Option.map SessionId.value)
              "status", Encode.string (record.Status.ToString().ToLowerInvariant())
              "currentRunId", optionalString record.CurrentRunId
              "hasPendingCompletion", Encode.bool record.HasPendingCompletion
              "lastCompletionStatus", optionalString record.LastCompletionStatus ]

        let identity =
            match ManagedAgent.tryParse record.Agent with
            | Some managed ->
                [ "agent", Encode.string managed.Name
                  "role", Encode.string (ManagedAgent.roleName managed.Role)
                  "tier", Encode.string (ManagedAgent.tierName managed.Tier)
                  "fallbackPeer", Encode.string (ManagedAgent.peer managed).Name ]
            | None when not (String.IsNullOrWhiteSpace record.Agent) ->
                [ "agent", Encode.string record.Agent
                  "role", Encode.string (record.Role.ToString().ToLowerInvariant()) ]
            | None -> [ "role", Encode.string (record.Role.ToString().ToLowerInvariant()) ]

        Encode.object (baseFields @ identity)

    let private ptyEntry (record: PtyRecord) =
        Encode.object
            [ "kind", Encode.string "pty"
              "ptyId", Encode.string record.PtyId
              "command", Encode.string record.Command
              "startedAt", Encode.string (record.StartedAt.ToString("O")) ]

    let private execute (scope: ToolRuntimeScope) (_args: HostToolArguments) context =
        task {
            match scope.RuntimeFor context with
            | Error runtimeError -> return ToolHostCodec.jsonObject [ "error", Encode.string runtimeError ]
            | Ok runtime ->
                let agents, ptys = runtime.List()

                return
                    List.append
                        (agents |> List.sortBy (fun record -> record.AgentId) |> List.map agentEntry)
                        (ptys |> List.sortBy (fun record -> record.PtyId) |> List.map ptyEntry)
                    |> ToolHostCodec.jsonArray
        }

    let spec scope =
        { Name = "list"
          Description = "List active agents and PTYs"
          Arguments = []
          Execute = execute scope }
