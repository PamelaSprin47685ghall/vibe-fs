namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session.AgentRoleIdentity

[<AutoOpen>]
module HostForkRuntimeFork =

    let private userPromptText (message: SessionMessage) =
        message.Parts
        |> Array.choose (function
            | MessagePart.Text text -> Some text
            | _ -> None)
        |> String.concat "\n"
        |> fun text -> if String.IsNullOrWhiteSpace text then None else Some text

    let private missingReviewRequirement (input: ReviewRequirementInput) : Result<string list, string> =
        Error(
            sprintf
                "Cannot load original user requirement message %s for reviewer"
                (MessageId.value input.MessageId)
        )

    let rec private resolveReviewRequirementInputs
        (port: ISessionSnapshotPort)
        (inputs: ReviewRequirementInput list)
        (cached: Map<SessionId, SessionMessage list>)
        (resolved: string list)
        : Task<Result<string list, string>> =
        task {
            match inputs with
            | [] -> return Ok(List.rev resolved)
            | input :: remaining ->
                let consume (messages: SessionMessage list) =
                    messages
                    |> List.tryFind (fun message -> message.Id = input.MessageId)
                    |> Option.bind userPromptText

                match Map.tryFind input.SourceSessionId cached with
                | Some messages ->
                    match consume messages with
                    | Some text -> return! resolveReviewRequirementInputs port remaining cached (text :: resolved)
                    | None -> return missingReviewRequirement input
                | None ->
                    let! messagesResult = port.GetMessages input.SourceSessionId

                    match messagesResult with
                    | Error err ->
                        return Error(sprintf "Cannot load original user requirements for reviewer: %s" err)
                    | Ok messages ->
                        let updated = Map.add input.SourceSessionId messages cached

                        match consume messages with
                        | Some text ->
                            return! resolveReviewRequirementInputs port remaining updated (text :: resolved)
                        | None -> return missingReviewRequirement input
        }

    let private enrichReviewerPrompt
        (journal: AgentJournal option)
        (snapshot: ISessionSnapshotPort option)
        (parentId: SessionId)
        (assignment: string)
        : Task<Result<string, string>> =
        task {
            let promptInputs = AgentJournal.pendingReviewRequirements journal parentId

            if List.isEmpty promptInputs then
                return Ok assignment
            else
                match snapshot with
                | None ->
                    return Error "Cannot start reviewer: original user requirements are unavailable without a session transcript"
                | Some port ->
                    let! resolved = resolveReviewRequirementInputs port promptInputs Map.empty []

                    match resolved with
                    | Error err -> return Error err
                    | Ok texts ->
                        let requirements =
                            texts
                            |> List.mapi (fun index text -> sprintf "User prompt %d:\n%s" (index + 1) text)
                            |> String.concat "\n\n"

                        return
                            Ok(
                                "[Original user requirements — authoritative review scope]\n"
                                + "These are verified HumanRoot prompts received since the prior review completed its double-PERFECT barrier and reached terminal idle. Verify every applicable requirement. The manager request below is supplementary and must not narrow or override this scope.\n\n"
                                + requirements
                                + "\n\n[Manager review request — supplementary]\n"
                                + assignment
                            )
        }

    type HostForkRuntime with

        member this.Fork
            (agentId: string, role: AgentRole, prompt: string, ?agent: string)
            : Task<Result<ForkResult, string>> =
            let agentName =
                match agent with
                | Some name when not (System.String.IsNullOrWhiteSpace name) -> name.Trim()
                | _ -> defaultFastManagedName role

            task {
                do! this.AwaitRecovery()

                let existing =
                    lock this.Gate (fun () ->
                        match this.Children.TryGetValue agentId with
                        | true, childId -> Some childId
                        | false, _ -> None)

                match existing with
                | Some childId ->
                    match HostPendingRun.sessionDeadRefusal this.Journal childId with
                    | Some refusal -> return Error refusal
                    | None ->
                        return!
                            HostForkChildDispatch.sendToExistingChild
                                this.Gate
                                this.PendingRuns
                                this.Sessions
                                this.ChildWorkRecordOf
                                this.Runtime
                                this.SendChildPrompt
                                this.SendBusyNudge
                                (fun child role -> this.RunStarted child role (this.DirectoryOf agentId))
                                agentId
                                childId
                                role
                                prompt
                                agentName
                | None ->
                    let! promptResult =
                        match role with
                        | AgentRole.Reviewer ->
                            enrichReviewerPrompt this.Journal this.SessionSnapshot this.ParentId prompt
                        | _ -> Task.FromResult(Ok prompt)

                    match promptResult with
                    | Error err -> return Error err
                    | Ok reviewerPrompt ->
                        let! childResult =
                            this.Sessions.CreateChildSession(
                                this.ParentId,
                                { Title = Some agentId
                                  Agent = Some agentName
                                  Directory = this.DirectoryOf agentId }
                            )

                        match childResult with
                        | Error err -> return Error err
                        | Ok childId ->
                            let linkageResult =
                                match this.Journal with
                                | None -> Ok()
                                | Some journal ->
                                    let fact =
                                        AgentFact.AgentForked
                                            {| ParentId = this.ParentId
                                               ChildId = ChildId.create (SessionId.value childId)
                                               TargetAgent = agentId
                                               Role = Some agentName |}

                                    match AgentJournal.appendAgent (StreamId.Session this.ParentId) None fact journal with
                                    | Ok _ -> Ok()
                                    | Error failure -> Error(sprintf "Failed to persist AgentForked: %A" failure.Failure)

                            match linkageResult with
                            | Error err ->
                                let! _ = this.Sessions.AbortSession childId
                                return Error err
                            | Ok() ->
                                let run = this.InstallRun(agentId, childId, role)

                                lock this.Gate (fun () -> this.Children.[agentId] <- childId)

                                this.ChildCreated agentId role childId
                                this.ChildCreatedDir agentId childId (this.DirectoryOf agentId)

                                let result =
                                    this.Runtime.Fork(
                                        agentId,
                                        role,
                                        runWork = (fun () -> run.Source.Task),
                                        agent = agentName
                                    )

                                match result with
                                | ForkResult.NotFound _ ->
                                    this.FailRun(run, "Fork runtime is cancelled")
                                    return Error "Fork runtime is cancelled"
                                | _ ->
                                    this.MarkReady(run)

                                    let enrichedPrompt =
                                        match this.ParentWorkRecordOf this.ParentId with
                                        | Some workRecord when not (System.String.IsNullOrWhiteSpace workRecord) ->
                                            sprintf
                                                "[Parent work record — background only; B preferred, else session A]\n%s\n\n[Assignment]\n%s\n\n[Required final report]\nResult:\nFiles changed:\nTests run:\nEvidence:\nRemaining risks:\nBlockers:"
                                                workRecord
                                                reviewerPrompt
                                        | _ -> reviewerPrompt

                                    let! sent =
                                        HostForkAgentOwner.sendFirstPrompt
                                            this.Sessions
                                            this.Journal
                                            childId
                                            agentName
                                            (this.DirectoryOf agentId)
                                            enrichedPrompt

                                    match sent with
                                    | Ok _ -> return Ok result
                                    | Error err ->
                                        this.FailRun(run, err)
                                        return Error err
            }

        member this.Reuse(agentId: string, prompt: string) : Task<Result<ForkResult, string>> =
            task {
                do! this.AwaitRecovery()

                let existing =
                    lock this.Gate (fun () ->
                        match this.Children.TryGetValue agentId with
                        | true, childId -> Some childId
                        | false, _ -> None)

                match existing with
                | None -> return Error(sprintf "Unknown agent id: %s" agentId)
                | Some childId ->
                    match HostPendingRun.sessionDeadRefusal this.Journal childId with
                    | Some refusal -> return Error refusal
                    | None ->
                        let roleOpt =
                            this.Runtime.List()
                            |> fst
                            |> List.tryFind (fun agent -> agent.AgentId = agentId)
                            |> Option.map (fun agent -> agent.Role)

                        match roleOpt with
                        | None -> return Error(sprintf "Unknown agent id: %s" agentId)
                        | Some role ->
                            let agentName =
                                this.Runtime.List()
                                |> fst
                                |> List.tryFind (fun a -> a.AgentId = agentId)
                                |> Option.map (fun a -> a.Agent)
                                |> Option.defaultValue (defaultFastManagedName role)

                            return!
                                HostForkChildDispatch.sendToExistingChild
                                    this.Gate
                                    this.PendingRuns
                                    this.Sessions
                                    this.ChildWorkRecordOf
                                    this.Runtime
                                    this.SendChildPrompt
                                    this.SendBusyNudge
                                    (fun child role -> this.RunStarted child role (this.DirectoryOf agentId))
                                    agentId
                                    childId
                                    role
                                    prompt
                                    agentName
            }
