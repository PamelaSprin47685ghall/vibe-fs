namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session.AgentRoleHelpers

[<AutoOpen>]
module HostForkRuntimeFork =
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
                                | Error failure -> Error(sprintf "Failed to persist AgentLinked: %A" failure.Failure)

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
                                            prompt
                                    | _ -> prompt

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
