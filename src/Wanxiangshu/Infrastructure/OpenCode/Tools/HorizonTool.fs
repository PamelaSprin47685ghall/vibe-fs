namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Host
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
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

        [<Literal>]
        let LatestWork = "tool/horizon/latest-work"

        [<Literal>]
        let NoWorkYet = "tool/horizon/no-work-yet"

        [<Literal>]
        let LatestWorkUnavailable = "tool/horizon/latest-work-unavailable"

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private labeled language path label =
        ProviderProse.render language path (Map [ "label", label ])

    let private labelForHandle language (handle: HandleRecord) =
        if not (String.IsNullOrWhiteSpace handle.Byname) then
            handle.Byname.Trim()
        elif not (String.IsNullOrWhiteSpace handle.TargetAgent) then
            match ManagedAgent.tryParse handle.TargetAgent with
            | Some managed -> managed.Name
            | None -> handle.TargetAgent.Trim()
        else
            ProviderProse.render language Path.Someone Map.empty

    let private lineForHandle language (handle: HandleRecord) (runtimeRecord: AgentRecord option) : string =
        let label = labelForHandle language handle

        match handle.Lifecycle with
        | HandleLifecycle.CompletedAwaitingJoin _ -> labeled language Path.Returned label
        | HandleLifecycle.Active ->
            match runtimeRecord with
            | Some record when record.CompletionCellSettled -> labeled language Path.Returned label
            | _ -> labeled language Path.StillAway label
        | HandleLifecycle.Abandoned _
        | HandleLifecycle.Retired -> labeled language Path.DidNotReturn label

    let private workRecordForHandle
        language
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (handle: HandleRecord)
        : Task<string> =
        task {
            let label = labelForHandle language handle

            let latestFrame =
                AgentProjection.tryFind handle.ChildSessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.Blog)
                |> Option.bind (BlogProjection.frames >> List.tryLast)

            match latestFrame with
            | None -> return labeled language Path.NoWorkYet label
            | Some frame ->
                match! journal.Writer.BlobWriter.Read frame.TextRef with
                | Ok text when HostDigest.sha256Hex text = BlobDigest.value frame.Digest ->
                    return ProviderProse.render language Path.LatestWork (Map [ "label", label; "record", text ])
                | _ -> return labeled language Path.LatestWorkUnavailable label
        }

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
                    let snapshot = AgentJournal.snapshot journal
                    let parentSessionId =
                        SessionId.create context.SessionId
                        |> scope.LogicalOwnerFor

                    let durableHandles =
                        AgentProjection.tryFind parentSessionId snapshot.AgentProjections
                        |> Option.bind (fun session -> session.Handles)
                        |> Option.defaultValue HandleProjection.empty

                    let runtimeByAgentId =
                        agents |> List.map (fun record -> record.AgentId, record) |> Map.ofList

                    let agentLines = ResizeArray<string>()

                    for handle in HandleProjection.listable durableHandles do
                        match HandleId.tryAgent handle.Handle with
                        | Some handleId ->
                            let agentId = AgentHandleId.value handleId
                            agentLines.Add(lineForHandle language handle (Map.tryFind agentId runtimeByAgentId))
                            let! workRecord = workRecordForHandle language journal snapshot handle
                            agentLines.Add workRecord
                        | None -> ()

                    let agentLines = agentLines |> Seq.toList

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
