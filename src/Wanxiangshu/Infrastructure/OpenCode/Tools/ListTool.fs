namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Session

/// list() is the durable handle view joined with physical PTY records.
module ListTool =

    let private tString = ToolHostCodec.TString
    let private tBool = ToolHostCodec.TBool

    let private optionalField (name: string) (value: string option) : (string * ToolHostCodec.TomlValue) list =
        match value with
        | Some v when not (String.IsNullOrWhiteSpace v) -> [ name, tString v ]
        | _ -> []

    let private agentEntry
        (handle: HandleRecord)
        (runtimeRecord: AgentRecord option)
        : (string * ToolHostCodec.TomlValue) list =
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
            | HandleLifecycle.Abandoned _
            | HandleLifecycle.Retired -> invalidArg "handle" "retired or abandoned handle is not listable"

        let baseFields =
            [ "kind", tString "agent"
              "agent_id", tString agentId
              "child_session_id", tString (SessionId.value handle.ChildSessionId)
              "status", tString status
              "has_pending_completion",
              tBool (
                  match handle.Lifecycle with
                  | HandleLifecycle.CompletedAwaitingJoin _ -> true
                  | HandleLifecycle.Active ->
                      runtimeRecord |> Option.exists (fun record -> record.CompletionCellSettled)
                  | HandleLifecycle.Abandoned _
                  | HandleLifecycle.Retired -> false
              ) ]

        let optionalFields =
            optionalField "current_run_id" (runtimeRecord |> Option.bind (fun record -> record.CurrentRunId))
            @ optionalField
                "last_completion_status"
                (runtimeRecord |> Option.bind (fun record -> record.TerminalStatusLabel))

        let identity =
            match ManagedAgent.tryParse handle.TargetAgent with
            | Some managed ->
                [ "agent", tString managed.Name
                  "role", tString (ManagedAgent.roleName managed.Role)
                  "tier", tString (ManagedAgent.tierName managed.Tier)
                  "fallback_peer", tString (ManagedAgent.peer managed).Name ]
            | None ->
                [ "agent", tString handle.TargetAgent
                  "role", tString (handle.CanonicalRole.ToString().ToLowerInvariant()) ]

        baseFields @ optionalFields @ identity

    let private ptyEntry (record: PtyRecord) : (string * ToolHostCodec.TomlValue) list =
        [ "kind", tString "pty"
          "pty_id", tString record.PtyId
          "command", tString record.Command
          "started_at", tString (record.StartedAt.ToString("O")) ]

    let private execute (scope: ToolRuntimeScope) (_args: HostToolArguments) context =
        task {
            match scope.Journal with
            | None ->
                return
                    ToolHostCodec.tomlObject
                        [ "error", tString "HandleProjection unavailable: durable journal is not configured" ]
            | Some journal ->
                match scope.RuntimeFor context with
                | Error runtimeError -> return ToolHostCodec.tomlObject [ "error", tString runtimeError ]
                | Ok runtime ->
                    let agents, ptys = runtime.List()

                    let durableHandles =
                        AgentJournal.handleProjection journal (SessionId.create context.SessionId)

                    let runtimeByAgentId =
                        agents |> List.map (fun record -> record.AgentId, record) |> Map.ofList

                    let listableAgents =
                        HandleProjection.listable durableHandles
                        |> List.choose (fun handle ->
                            match HandleId.tryAgent handle.Handle with
                            | Some handleId ->
                                let agentId = AgentHandleId.value handleId
                                Some(agentEntry handle (Map.tryFind agentId runtimeByAgentId))
                            | None -> None)

                    let ptyEntries =
                        ptys |> List.sortBy (fun record -> record.PtyId) |> List.map ptyEntry

                    return ToolHostCodec.tomlTable "item" (List.append listableAgents ptyEntries)
        }

    let spec scope =
        { Name = "list"
          Description = "List active agents and PTYs"
          Arguments = []
          Execute = execute scope }
