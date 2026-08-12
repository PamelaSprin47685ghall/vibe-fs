namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Session

/// horizon() — natural-language roster of who remains at the caller's horizon.
module HorizonTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/horizon/description"

        [<Literal>]
        let Returned = "tool/horizon/returned"

        [<Literal>]
        let StillAway = "tool/horizon/still-away"

        [<Literal>]
        let DidNotReturn = "tool/horizon/did-not-return"

        [<Literal>]
        let RemainsOpen = "tool/horizon/remains-open"

        [<Literal>]
        let Someone = "tool/horizon/someone"

        [<Literal>]
        let TerminalLabel = "tool/horizon/terminal-label"

        [<Literal>]
        let EmptyRoster = "tool/horizon/empty-roster"

        [<Literal>]
        let UnavailableFromContext = "tool/horizon/unavailable-from-context"

        [<Literal>]
        let CannotBeSeen = "tool/horizon/cannot-be-seen"

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private labeled language path label =
        ProviderProse.render language path (Map [ "label", label ])

    let private lineForHandle language (handle: HandleRecord) (runtimeRecord: AgentRecord option) : string =
        let label =
            if not (String.IsNullOrWhiteSpace handle.Byname) then
                handle.Byname.Trim()
            elif not (String.IsNullOrWhiteSpace handle.TargetAgent) then
                match ManagedAgent.tryParse handle.TargetAgent with
                | Some managed -> managed.Name
                | None -> handle.TargetAgent.Trim()
            else
                ProviderProse.render language Path.Someone Map.empty

        match handle.Lifecycle with
        | HandleLifecycle.CompletedAwaitingJoin _ -> labeled language Path.Returned label
        | HandleLifecycle.Active ->
            match runtimeRecord with
            | Some record when record.CompletionCellSettled -> labeled language Path.Returned label
            | _ -> labeled language Path.StillAway label
        | HandleLifecycle.Abandoned _
        | HandleLifecycle.Retired -> labeled language Path.DidNotReturn label

    let private lineForPty language (record: PtyRecord) : string =
        let label =
            if String.IsNullOrWhiteSpace record.Command then
                ProviderProse.render language Path.TerminalLabel Map.empty
            else
                record.Command.Trim()

        labeled language Path.RemainsOpen label

    let private unavailable language path =
        ToolHostCodec.tomlObjectWithInstructions [ ProviderProse.render language path Map.empty ] []

    let private execute (scope: ToolRuntimeScope) (_args: HostToolArguments) context =
        task {
            let language = lang context

            match scope.Journal with
            | None -> return unavailable language Path.UnavailableFromContext
            | Some journal ->
                match scope.RuntimeFor context with
                | Error _ -> return unavailable language Path.CannotBeSeen
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
                                Some(lineForHandle language handle (Map.tryFind agentId runtimeByAgentId))
                            | None -> None)

                    let ptyLines =
                        ptys
                        |> List.sortBy (fun record -> record.PtyId)
                        |> List.map (lineForPty language)

                    let lines = List.append agentLines ptyLines

                    let instructions =
                        if List.isEmpty lines then
                            ProviderProse.instructionLines language Path.EmptyRoster Map.empty
                        else
                            lines

                    return ToolHostCodec.tomlObjectWithInstructions instructions []
        }

    let spec scope =
        { Name = "horizon"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = []
          Execute = execute scope }
