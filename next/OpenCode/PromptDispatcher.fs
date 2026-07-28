namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

/// Single transport for plugin-generated user-shaped continuations. It claims
/// before send and records the accepted Host message ID before exposing success.
module PromptDispatcher =

    type Dispatcher(?journal: AgentJournal) =
        let gate = obj ()
        let journal = journal
        let mutable authority = PromptAuthority.empty

        member _.Projection = lock gate (fun () -> authority)

        member _.RegisterAuthority(profile: PromptAuthority.AuthorityExecutionProfile) =
            lock gate (fun () -> authority <- PromptAuthority.registerAuthority profile authority)

            journal
            |> Option.iter (fun j ->
                AgentJournal.appendAgent
                    (StreamId.Session profile.SessionId)
                    (Some(TurnId.ofMessageId profile.AuthorityRootUserMessageId))
                    (AgentFact.AuthorityRootAccepted
                        {| SessionId = profile.SessionId
                           LogicalRunId = profile.LogicalRunId
                           HostMessageId = MessageId.value profile.AuthorityRootUserMessageId
                           AuthorityKind = string profile.AuthorityKind
                           Agent = profile.Agent
                           BaseProviderID = profile.BaseModel |> Option.map (fun m -> m.providerID)
                           BaseModelID = profile.BaseModel |> Option.map (fun m -> m.modelID)
                           Variant = profile.Variant |})
                    j
                |> ignore)

        member _.SendContinuation
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

                let persist fact =
                    match journal with
                    | None -> Ok()
                    | Some j ->
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            (Some(TurnId.ofMessageId profile.AuthorityRootUserMessageId))
                            fact
                            j
                        |> Result.map (fun _ -> ())
                        |> Result.mapError string

                match
                    persist (
                        AgentFact.PluginPromptClaimed
                            {| PromptKey = PromptKeyRef.value key
                               SessionId = sessionId
                               LogicalRunId = profile.LogicalRunId
                               AuthorityRootUserMessageId = MessageId.value profile.AuthorityRootUserMessageId
                               ContinuationKind = string continuation
                               Agent = Some profile.Agent
                               EffectiveProviderID = effectiveModel |> Option.map (fun m -> m.providerID)
                               EffectiveModelID = effectiveModel |> Option.map (fun m -> m.modelID)
                               Variant = profile.Variant |}
                    )
                with
                | Error error -> raise (InvalidOperationException error)
                | Ok() -> lock gate (fun () -> authority <- PromptAuthority.registerClaim claim authority)

                let metadata =
                    createObj
                        [ "wanxiangshu_prompt_key", box (PromptKeyRef.value key)
                          "wanxiangshu_origin", box (string continuation)
                          "wanxiangshu_logical_run", box profile.LogicalRunId
                          "wanxiangshu_authority_root", box (MessageId.value profile.AuthorityRootUserMessageId) ]

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
                        // prompt_async often returns a synthetic admission id
                        // (accepted-<session>). That is not a durable physical
                        // user message. Leave the claim pending so chat.message
                        // can map the real HostMessageId via PluginPromptAccepted.
                        let hostId = MessageId.value messageId

                        if hostId.StartsWith("accepted-") then
                            onAccepted |> Option.iter (fun callback -> callback messageId)
                            return Ok messageId
                        else
                            match
                                persist (
                                    AgentFact.PluginPromptAccepted
                                        {| PromptKey = PromptKeyRef.value key
                                           SessionId = sessionId
                                           HostMessageId = hostId |}
                                )
                            with
                            | Error error -> return Error error
                            | Ok() ->
                                lock gate (fun () -> authority <- PromptAuthority.acceptClaim key messageId authority)
                                onAccepted |> Option.iter (fun callback -> callback messageId)
                                return Ok messageId
                    | Error error ->
                        let abandoned =
                            persist (
                                AgentFact.PluginPromptAbandoned
                                    {| PromptKey = PromptKeyRef.value key
                                       SessionId = sessionId
                                       Reason = error |}
                            )

                        lock gate (fun () -> authority <- PromptAuthority.abandonClaim key authority)

                        match abandoned with
                        | Ok() -> return Error error
                        | Error journalError -> return Error journalError
                finally
                    listener.Dispose()
            }
