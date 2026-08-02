namespace Wanxiangshu.Next.OpenCode

open System
open Thoth.Json
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

/// list() is the durable handle view joined with physical PTY records.
module ListTool =

    let private optionalString value =
        value |> Option.map Encode.string |> Option.defaultValue Encode.nil

    let private agentEntry (handle: HandleRecord) (runtimeRecord: AgentRecord option) =
        let agentId =
            match HandleId.tryAgent handle.Handle with
            | Some value -> AgentHandleId.value value
            | None -> invalidArg "handle" "listable handle is not an agent"

        let status =
            match handle.Lifecycle with
            | HandleLifecycle.CompletedAwaitingJoin _ -> "completed-awaiting-join"
            | HandleLifecycle.Active ->
                runtimeRecord
                |> Option.map (fun record -> record.Status.ToString().ToLowerInvariant())
                |> Option.defaultValue "running"
            | HandleLifecycle.Retired -> invalidArg "handle" "retired handle is not listable"

        let baseFields =
            [ "kind", Encode.string "agent"
              "agentId", Encode.string agentId
              "childSessionId", Encode.string (SessionId.value handle.ChildSessionId)
              "status", Encode.string status
              "currentRunId", optionalString (runtimeRecord |> Option.bind (fun record -> record.CurrentRunId))
              "hasPendingCompletion",
              Encode.bool (
                  match handle.Lifecycle with
                  | HandleLifecycle.CompletedAwaitingJoin _ -> true
                  | HandleLifecycle.Active -> runtimeRecord |> Option.exists (fun record -> record.HasPendingCompletion)
                  | HandleLifecycle.Retired -> false
              )
              "lastCompletionStatus", optionalString (runtimeRecord |> Option.bind (fun record -> record.LastCompletionStatus)) ]

        let identity =
            match ManagedAgent.tryParse handle.TargetAgent with
            | Some managed ->
                [ "agent", Encode.string managed.Name
                  "role", Encode.string (ManagedAgent.roleName managed.Role)
                  "tier", Encode.string (ManagedAgent.tierName managed.Tier)
                  "fallbackPeer", Encode.string (ManagedAgent.peer managed).Name ]
            | None ->
                [ "agent", Encode.string handle.TargetAgent
                  "role", Encode.string (handle.CanonicalRole.ToString().ToLowerInvariant()) ]

        Encode.object (baseFields @ identity)

    let private ptyEntry (record: PtyRecord) =
        Encode.object
            [ "kind", Encode.string "pty"
              "ptyId", Encode.string record.PtyId
              "command", Encode.string record.Command
              "startedAt", Encode.string (record.StartedAt.ToString("O")) ]

    let private execute (scope: ToolRuntimeScope) (_args: HostToolArguments) context =
        task {
            match scope.Journal with
            | None ->
                return ToolHostCodec.jsonObject [ "error", Encode.string "HandleProjection unavailable: durable journal is not configured" ]
            | Some journal ->
                match scope.RuntimeFor context with
                | Error runtimeError -> return ToolHostCodec.jsonObject [ "error", Encode.string runtimeError ]
                | Ok runtime ->
                    let agents, ptys = runtime.List()
                    let durableHandles = AgentJournal.handleProjection journal (SessionId.create context.SessionId)

                    let runtimeByAgentId = agents |> List.map (fun record -> record.AgentId, record) |> Map.ofList

                    let listableAgents =
                        HandleProjection.listable durableHandles
                        |> List.choose (fun handle ->
                            match HandleId.tryAgent handle.Handle with
                            | Some handleId ->
                                let agentId = AgentHandleId.value handleId
                                Some(agentEntry handle (Map.tryFind agentId runtimeByAgentId))
                            | None -> None)

                    return
                        List.append
                            listableAgents
                            (ptys |> List.sortBy (fun record -> record.PtyId) |> List.map ptyEntry)
                        |> ToolHostCodec.jsonArray
        }

    let spec scope =
        { Name = "list"
          Description = "List active agents and PTYs"
          Arguments = []
          Execute = execute scope }
