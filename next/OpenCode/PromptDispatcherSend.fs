namespace Wanxiangshu.Next.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact

/// Plugin prompt send protocol (claim → Host send). Accept for continuations is
/// deferred to chat.message; send-admission ids are never durable authority.
[<AutoOpen>]
module PromptDispatcherSend =

    type PromptDispatcher.Runtime with

        member this.SendAgentOwnerRoot
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (agent: string)
            (directory: string option)
            (onAccepted: (MessageId -> unit) option)
            : Task<Result<MessageId, string>> =
            task {
                let key = PromptDispatcher.newPromptKey ()

                match PromptAuthorityRun.claimAgentOwnerRoot key sessionId agent with
                | Error e -> return Error e
                | Ok claim ->
                    let claimFact =
                        AgentFact.PluginPromptClaimed
                            {| PromptKey = PromptKeyRef.value key
                               SessionId = sessionId
                               LogicalRunId = ""
                               AuthorityRootUserMessageId = ""
                               ContinuationKind = "AgentOwnerRoot"
                               EffectiveAgent = Some agent |}

                    match this.Persist sessionId None claimFact with
                    | Error e -> return Error e
                    | Ok() ->
                        this.Update(PromptAuthorityRun.registerClaim claim)

                        use _listener = this.SubscribeNoOp port sessionId

                        let options =
                            { Model = None
                              Agent = Some agent
                              Directory = directory
                              Metadata = Some(this.Metadata key "AgentOwnerRoot" "" "") }

                        let! sendResult = port.SendPrompt(sessionId, text, options)

                        match sendResult with
                        | Error e ->
                            let abandonFact =
                                AgentFact.PluginPromptAbandoned
                                    {| PromptKey = PromptKeyRef.value key
                                       SessionId = sessionId
                                       Reason = e |}

                            this.Persist sessionId None abandonFact |> ignore
                            this.Update(PromptAuthorityRun.abandonClaim key)
                            return Error e
                        | Ok hostMessageId ->
                            // prompt_async may only admit the request as accepted-*.
                            // That transport token is never a durable Authority Root;
                            // chat.message later accepts with the real physical id.
                            let hostId = MessageId.value hostMessageId

                            if hostId.StartsWith("accepted-") then
                                onAccepted |> Option.iter (fun cb -> cb hostMessageId)
                                return Ok hostMessageId
                            else
                                match this.AcceptPhysicalAgentOwnerRoot key sessionId hostMessageId agent with
                                | Error e -> return Error e
                                | Ok _profile ->
                                    onAccepted |> Option.iter (fun cb -> cb hostMessageId)
                                    return Ok hostMessageId
            }

        member this.SendContinuation
            (port: ISessionHostPort)
            (sessionId: SessionId)
            (text: string)
            (continuation: PromptAuthority.ContinuationKind)
            (profile: PromptAuthority.AuthorityExecutionProfile)
            (effectiveAgent: string)
            (directory: string option)
            (onAccepted: (MessageId -> unit) option)
            : Task<Result<MessageId, string>> =
            task {
                let key = PromptDispatcher.newPromptKey ()

                let claim =
                    PromptAuthorityRun.claimContinuation key sessionId continuation profile effectiveAgent

                let claimFact =
                    AgentFact.PluginPromptClaimed
                        {| PromptKey = PromptKeyRef.value key
                           SessionId = sessionId
                           LogicalRunId = profile.LogicalRunId
                           AuthorityRootUserMessageId = MessageId.value profile.AuthorityRootUserMessageId
                           ContinuationKind =
                            PromptDispatcher.originLabel (PromptAuthority.PromptOrigin.Continuation continuation)
                           EffectiveAgent = Some effectiveAgent |}

                match this.Persist sessionId None claimFact with
                | Error e -> return Error e
                | Ok() ->
                    this.Update(PromptAuthorityRun.registerClaim claim)

                    use _listener = this.SubscribeNoOp port sessionId

                    let options =
                        { Model = None
                          Agent = Some effectiveAgent
                          Directory = directory
                          Metadata =
                            Some(
                                this.Metadata
                                    key
                                    (PromptDispatcher.originLabel (
                                        PromptAuthority.PromptOrigin.Continuation continuation
                                    ))
                                    profile.LogicalRunId
                                    (MessageId.value profile.AuthorityRootUserMessageId)
                            ) }

                    let! sendResult = port.SendPrompt(sessionId, text, options)

                    match sendResult with
                    | Error e ->
                        let abandonFact =
                            AgentFact.PluginPromptAbandoned
                                {| PromptKey = PromptKeyRef.value key
                                   SessionId = sessionId
                                   Reason = e |}

                        this.Persist sessionId None abandonFact |> ignore
                        this.Update(PromptAuthorityRun.abandonClaim key)
                        return Error e
                    | Ok hostMessageId ->
                        // Durable PluginPromptAccepted is deferred to chat.message with the
                        // real HostMessageId. The send-admission id never maps the claim.
                        onAccepted |> Option.iter (fun cb -> cb hostMessageId)
                        return Ok hostMessageId
            }
