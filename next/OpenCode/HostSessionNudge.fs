namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module HostSessionNudge =

    let private tryActiveProfile (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | None -> None
        | Some j ->
            match Map.tryFind sessionId (AgentJournal.snapshot j).AgentProjections.Sessions with
            | None -> None
            | Some session ->
                match
                    session.PromptAuthority
                    |> Option.bind (fun authority -> authority.ActiveLogicalRun)
                with
                | None -> None
                | Some durable ->
                    let model =
                        match durable.BaseProviderID, durable.BaseModelID with
                        | Some providerID, Some modelID ->
                            Some
                                { providerID = providerID
                                  modelID = modelID
                                  variant = durable.Variant }
                        | _ -> None

                    let authorityKind =
                        match durable.AuthorityKind with
                        | "AgentOwnerRoot" -> PromptAuthority.AgentOwnerRoot
                        | _ -> PromptAuthority.HumanRoot

                    Some(
                        { SessionId = sessionId
                          LogicalRunId = durable.LogicalRunId
                          AuthorityRootUserMessageId = MessageId.create durable.AuthorityRootUserMessageId
                          AuthorityKind = authorityKind
                          Agent = durable.Agent
                          BaseModel = model
                          Variant = durable.Variant }
                        : PromptAuthority.AuthorityExecutionProfile
                    )

    /// Reconciled linked children have a host-proven root user message even when
    /// the host omitted agent metadata from `chat.message`. Register that real
    /// AgentOwner authority once; never use this for an unlinked/unknown session.
    let ensureAgentOwnerAuthority
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (rootUserMessageId: MessageId)
        (agent: AgentRole)
        (model: OpencodeModel option)
        =
        match tryActiveProfile journal sessionId with
        | Some _ -> ()
        | None ->
            let dispatcher = PromptDispatcher.Dispatcher(?journal = journal)

            let profile =
                PromptAuthority.createAuthorityRoot
                    sessionId
                    PromptAuthority.AgentOwnerRoot
                    rootUserMessageId
                    (agent.ToString().ToLowerInvariant())
                    model
                    None

            dispatcher.RegisterAuthority profile

    /// Sends a continuation only when a durable Authority Root exists. Unknown
    /// physical user messages fail closed rather than manufacturing a new root.
    let sendContinuation
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (kind: PromptAuthority.ContinuationKind)
        (options: SessionPromptOptions)
        (journal: AgentJournal option)
        (onAccepted: (MessageId -> unit) option)
        =
        match tryActiveProfile journal sessionId with
        | None -> ()
        | Some profile ->
            let dispatcher = PromptDispatcher.Dispatcher(?journal = journal)

            task {
                let! _ =
                    dispatcher.SendContinuation
                        sessionPort
                        sessionId
                        prompt
                        kind
                        profile
                        (options.Model |> Option.orElse profile.BaseModel)
                        options.Directory
                        onAccepted

                ()
            }
            |> ignore
