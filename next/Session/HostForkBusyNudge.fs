namespace Wanxiangshu.Next.Session

open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Busy-agent nudge via PromptDispatcher (KISS-N12 Continuation).
module HostForkBusyNudge =

    /// Continuation of the child's active Logical Run. Never creates a new
    /// Authority Root / RunId / completion.
    let send
        (sessions: ISessionHostPort)
        (parentId: SessionId)
        (journal: AgentJournal option)
        (childId: SessionId)
        (role: AgentRole)
        (model: OpencodeModel option)
        (directory: string option)
        (prompt: string)
        : Task<Result<unit, string>> =
        task {
            match journal with
            | None ->
                // Unit-host / no journal: fire-and-forget without inventing AuthorityRoot.
                return!
                    sessions.SendChildPromptFireAndForget(
                        parentId,
                        childId,
                        prompt,
                        { Model = model
                          Agent = Some(role.ToString().ToLowerInvariant())
                          Directory = directory
                          Metadata = None }
                    )
            | Some j ->
                let profileOpt =
                    match Map.tryFind childId (AgentJournal.snapshot j).AgentProjections.Sessions with
                    | None -> None
                    | Some session ->
                        session.PromptAuthority
                        |> Option.bind (fun authority -> authority.ActiveLogicalRun)
                        |> Option.map (fun durable ->
                            let baseModel =
                                match durable.BaseProviderID, durable.BaseModelID with
                                | Some providerID, Some modelID ->
                                    Some
                                        { providerID = providerID
                                          modelID = modelID
                                          variant = durable.Variant }
                                | _ -> model

                            let authorityKind =
                                match durable.AuthorityKind with
                                | "AgentOwnerRoot" -> PromptAuthority.AgentOwnerRoot
                                | _ -> PromptAuthority.HumanRoot

                            ({ SessionId = childId
                               LogicalRunId = durable.LogicalRunId
                               AuthorityRootUserMessageId =
                                   MessageId.create durable.AuthorityRootUserMessageId
                               AuthorityKind = authorityKind
                               Agent = durable.Agent
                               BaseModel = baseModel
                               Variant = durable.Variant }
                             : PromptAuthority.AuthorityExecutionProfile))

                match profileOpt with
                | None ->
                    return Error "Busy nudge requires ActiveLogicalRun on child session"
                | Some profile ->
                    let dispatcher = PromptDispatcher.Dispatcher(?journal = journal)

                    let! sent =
                        dispatcher.SendContinuation
                            sessions
                            childId
                            prompt
                            PromptAuthority.BusyAgentNudge
                            profile
                            (model |> Option.orElse profile.BaseModel)
                            directory
                            None

                    match sent with
                    | Ok _ -> return Ok()
                    | Error err -> return Error err
        }

    let sender sessions parentId modelResolver journal directoryOf =
        fun agentId childId role prompt ->
            send
                sessions
                parentId
                journal
                childId
                role
                (HostPendingRun.resolveModel modelResolver journal childId)
                (directoryOf agentId)
                prompt
