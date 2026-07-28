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

    let sendAgentOwnerRoot
        (ops: ClaimOps)
        (runtimeId: string)
        (port: ISessionHostPort)
        (sessionId: SessionId)
        (text: string)
        (agent: string)
        (baseModel: OpencodeModel option)
        (variant: string option)
        (directory: string option)
        (onAccepted: (MessageId -> unit) option)
        : Task<Result<MessageId * PromptAuthority.AuthorityExecutionProfile, string>> =
        task {
            let key = PromptAuthority.newPromptKey ()
            let claim = PromptAuthority.claimAgentOwnerRoot key sessionId agent baseModel variant

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
                           Agent = Some agent
                           EffectiveProviderID = baseModel |> Option.map (fun m -> m.providerID)
                           EffectiveModelID = baseModel |> Option.map (fun m -> m.modelID)
                           Variant = variant |})
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
                    let! result =
                        port.SendPrompt(
                            sessionId,
                            text,
                            { Model = baseModel
                              Agent = Some agent
                              Directory = directory
                              Metadata = Some metadata }
                        )

                    match result with
                    | Ok messageId ->
                        let hostId = MessageId.value messageId

                        if hostId.StartsWith("accepted-") then
                            onAccepted |> Option.iter (fun callback -> callback messageId)

                            let provisional =
                                PromptAuthority.createAuthorityRoot
                                    runtimeId
                                    sessionId
                                    PromptAuthority.AgentOwnerRoot
                                    messageId
                                    agent
                                    baseModel
                                    variant

                            return Ok(messageId, provisional)
                        else
                            let profile =
                                PromptAuthority.createAuthorityRoot
                                    runtimeId
                                    sessionId
                                    PromptAuthority.AgentOwnerRoot
                                    messageId
                                    agent
                                    baseModel
                                    variant

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
        (effectiveModel: OpencodeModel option)
        (directory: string option)
        (onAccepted: (MessageId -> unit) option)
        : Task<Result<MessageId, string>> =
        task {
            let key = PromptAuthority.newPromptKey ()

            let claim =
                PromptAuthority.claimContinuation key sessionId continuation profile effectiveModel

            match
                ops.ClaimAndPersist
                    sessionId
                    (Some(TurnId.ofMessageId profile.AuthorityRootUserMessageId))
                    (AgentFact.PluginPromptClaimed
                        {| PromptKey = PromptKeyRef.value key
                           SessionId = sessionId
                           LogicalRunId = profile.LogicalRunId
                           AuthorityRootUserMessageId = MessageId.value profile.AuthorityRootUserMessageId
                           ContinuationKind =
                            PromptAuthority.originLabel (PromptAuthority.Continuation continuation)
                           Agent = Some profile.Agent
                           EffectiveProviderID = effectiveModel |> Option.map (fun m -> m.providerID)
                           EffectiveModelID = effectiveModel |> Option.map (fun m -> m.modelID)
                           Variant = profile.Variant |})
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
                          "wanxiangshu_authority_root",
                          box (MessageId.value profile.AuthorityRootUserMessageId) ]

                let listener = port.SubscribeTerminal(sessionId, (fun _ _ -> ()))

                try
                    let! result =
                        port.SendPrompt(
                            sessionId,
                            text,
                            { Model = effectiveModel
                              Agent = Some profile.Agent
                              Directory = directory
                              Metadata = Some metadata }
                        )

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
