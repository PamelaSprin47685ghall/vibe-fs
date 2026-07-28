namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact

module PromptAuthoritySend =

    type ClaimOps =
        { ClaimAndPersist:
            SessionId -> TurnId option -> AgentFact -> PromptAuthority.PromptClaim -> Result<unit, string>
          AbandonClaim: SessionId -> PromptKeyRef -> string -> Result<unit, string>
          AcceptPhysicalClaim:
              SessionId
                  -> PromptKeyRef
                  -> MessageId
                  -> PromptAuthority.AuthorityExecutionProfile option
                  -> Result<unit, string> }

    /// Host Contract: managed agents always send Agent=Some effectiveAgent; Model=None.
    let private promptOptions (effectiveAgent: string) (directory: string option) (metadata: obj option) =
        { Model = None
          Agent = Some effectiveAgent
          Directory = directory
          Metadata = metadata }

    let sendAgentOwnerRoot
        (ops: ClaimOps)
        (runtimeId: string)
        (port: ISessionHostPort)
        (sessionId: SessionId)
        (text: string)
        (agent: string)
        (directory: string option)
        (onAccepted: (MessageId -> unit) option)
        : Task<Result<MessageId * PromptAuthority.AuthorityExecutionProfile, string>> =
        task {
            let key = PromptAuthority.newPromptKey ()

            match PromptAuthority.claimAgentOwnerRoot key sessionId agent with
            | Error error -> return Error error
            | Ok claim ->
                match
                    ops.ClaimAndPersist
                        sessionId
                        None
                        (AgentFact.PluginPromptClaimed
                            {| PromptKey = PromptKeyRef.value key
                               SessionId = sessionId
                               LogicalRunId = ""
                               AuthorityRootUserMessageId = ""
                               ContinuationKind = "AgentOwnerRoot"
                               EffectiveAgent = Some agent |})
                        claim
                with
                | Error error -> return Error error
                | Ok() ->
                    let metadata =
                        createObj
                            [ "wanxiangshu_prompt_key", box (PromptKeyRef.value key)
                              "wanxiangshu_origin", box "AgentOwnerRoot"
                              "wanxiangshu_logical_run", box ""
                              "wanxiangshu_authority_root", box "" ]

                    let listener = port.SubscribeTerminal(sessionId, (fun _ _ -> ()))

                    try
                        let! result = port.SendPrompt(sessionId, text, promptOptions agent directory (Some metadata))

                        match result with
                        | Ok messageId ->
                            let hostId = MessageId.value messageId

                            match
                                PromptAuthority.createAuthorityRoot
                                    runtimeId
                                    sessionId
                                    PromptAuthority.AgentOwnerRoot
                                    messageId
                                    agent
                            with
                            | Error error -> return Error error
                            | Ok profile ->
                                match ops.AcceptPhysicalClaim sessionId key messageId (Some profile) with
                                | Error error -> return Error error
                                | Ok() ->
                                    onAccepted |> Option.iter (fun callback -> callback messageId)
                                    return Ok(messageId, profile)
                        | Error error ->
                            match ops.AbandonClaim sessionId key error with
                            | Ok() -> return Error error
                            | Error journalError -> return Error journalError
                    finally
                        listener.Dispose()
        }

    let sendContinuation
        (ops: ClaimOps)
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
            let key = PromptAuthority.newPromptKey ()

            let claim =
                PromptAuthority.claimContinuation key sessionId continuation profile effectiveAgent

            match
                ops.ClaimAndPersist
                    sessionId
                    (Some(TurnId.ofMessageId profile.AuthorityRootUserMessageId))
                    (AgentFact.PluginPromptClaimed
                        {| PromptKey = PromptKeyRef.value key
                           SessionId = sessionId
                           LogicalRunId = profile.LogicalRunId
                           AuthorityRootUserMessageId = MessageId.value profile.AuthorityRootUserMessageId
                           ContinuationKind = PromptAuthority.originLabel (PromptAuthority.Continuation continuation)
                           EffectiveAgent = Some effectiveAgent |})
                    claim
            with
            | Error error -> return Error error
            | Ok() ->
                let metadata =
                    createObj
                        [ "wanxiangshu_prompt_key", box (PromptKeyRef.value key)
                          "wanxiangshu_origin",
                          box (PromptAuthority.originLabel (PromptAuthority.Continuation continuation))
                          "wanxiangshu_logical_run", box profile.LogicalRunId
                          "wanxiangshu_authority_root", box (MessageId.value profile.AuthorityRootUserMessageId) ]

                let listener = port.SubscribeTerminal(sessionId, (fun _ _ -> ()))

                try
                    let! result =
                        port.SendPrompt(sessionId, text, promptOptions effectiveAgent directory (Some metadata))

                    match result with
                    | Ok messageId ->
                        let hostId = MessageId.value messageId

                        if hostId.StartsWith("accepted-") then
                            onAccepted |> Option.iter (fun callback -> callback messageId)
                            return Ok messageId
                        else
                            match ops.AcceptPhysicalClaim sessionId key messageId None with
                            | Error error -> return Error error
                            | Ok() ->
                                onAccepted |> Option.iter (fun callback -> callback messageId)
                                return Ok messageId
                    | Error error ->
                        match ops.AbandonClaim sessionId key error with
                        | Ok() -> return Error error
                        | Error journalError -> return Error journalError
                finally
                    listener.Dispose()
        }
