namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Session

/// horizon() — natural-language roster of who remains at the caller's horizon.
module HorizonTool =

    let private lineForHandle (handle: HandleRecord) (runtimeRecord: AgentRecord option) : string =
        let label =
            if not (String.IsNullOrWhiteSpace handle.TargetAgent) then
                handle.TargetAgent
            else
                match ManagedAgent.tryParse handle.TargetAgent with
                | Some managed -> managed.Name
                | None -> "someone"

        match handle.Lifecycle with
        | HandleLifecycle.CompletedAwaitingJoin _ -> sprintf "# %s has returned." label
        | HandleLifecycle.Active ->
            match runtimeRecord with
            | Some record when record.CompletionCellSettled -> sprintf "# %s has returned." label
            | _ -> sprintf "# %s is still away." label
        | HandleLifecycle.Abandoned _
        | HandleLifecycle.Retired -> sprintf "# %s did not return from this charge." label

    let private lineForPty (record: PtyRecord) : string =
        let label =
            if String.IsNullOrWhiteSpace record.Command then
                "Terminal"
            else
                record.Command.Trim()

        sprintf "# %s remains open." label

    let private unavailable message =
        ToolHostCodec.tomlObjectWithInstructions [ "# " + message ] []

    let private execute (scope: ToolRuntimeScope) (_args: HostToolArguments) context =
        task {
            match scope.Journal with
            | None -> return unavailable "The horizon is unavailable from this execution context."
            | Some journal ->
                match scope.RuntimeFor context with
                | Error _ -> return unavailable "The horizon cannot be seen from this execution context."
                | Ok runtime ->
                    let agents, ptys = runtime.List()

                    let durableHandles =
                        AgentJournal.handleProjection journal (SessionId.create context.SessionId)

                    let runtimeByAgentId =
                        agents |> List.map (fun record -> record.AgentId, record) |> Map.ofList

                    let agentLines =
                        HandleProjection.listable durableHandles
                        |> List.choose (fun handle ->
                            match HandleId.tryAgent handle.Handle with
                            | Some handleId ->
                                let agentId = AgentHandleId.value handleId
                                Some(lineForHandle handle (Map.tryFind agentId runtimeByAgentId))
                            | None -> None)

                    let ptyLines =
                        ptys |> List.sortBy (fun record -> record.PtyId) |> List.map lineForPty

                    let lines = List.append agentLines ptyLines

                    let instructions =
                        if List.isEmpty lines then
                            [ "# Nothing beyond your immediate sight presently asks for your attention." ]
                        else
                            lines

                    return ToolHostCodec.tomlObjectWithInstructions instructions []
        }

    let spec scope =
        { Name = "horizon"
          Description =
            "Orient to what remains at your horizon — who is still away, who has returned, which terminals remain open."
          Arguments = []
          Execute = execute scope }
