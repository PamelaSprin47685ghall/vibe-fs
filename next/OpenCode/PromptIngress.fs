namespace Wanxiangshu.Next.OpenCode

open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// chat.message authority policy. Raw host payloads are decoded by
/// PromptIngressCodec; unknown origins remain fail-closed.
[<RequireQualifiedAccess>]
module PromptIngress =

    let private isValidAgent (value: string) =
        match PromptAuthority.parseAgentName value with
        | Ok _ -> true
        | Error _ -> false

    let private handle
        (journal: AgentJournal option)
        (bindUserMessage: string -> string -> unit)
        (bindContinuationMessage: string -> string -> unit)
        (registerOwned: string -> unit)
        (onAuthorityResolved: SessionId -> PromptAuthority.AuthorityExecutionProfile -> unit)
        (message: PromptIngressCodec.DecodedMessage)
        =
        match message.SessionId, message.MessageId with
        | Some sessionId, Some messageId ->
            let runtime =
                match journal with
                | Some value -> PromptDispatcher.forJournal value
                | None -> PromptDispatcher.ephemeral ()

            let knownOrigin =
                runtime.ResolveOrigin messageId message.PromptKey message.IsHostCompaction

            let origin =
                match knownOrigin with
                | PromptAuthority.PromptOrigin.UnknownOrigin ->
                    match message.ExplicitAgent with
                    | Some agent when isValidAgent agent ->
                        PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot
                    | _ -> PromptAuthority.PromptOrigin.UnknownOrigin
                | resolved -> resolved

            match origin with
            | PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot ->
                match message.ExplicitAgent with
                | Some agent ->
                    match runtime.AcceptHumanRoot sessionId messageId (Some agent) with
                    | Ok profile ->
                        onAuthorityResolved sessionId profile
                        bindUserMessage (SessionId.value sessionId) (MessageId.value messageId)
                        registerOwned (SessionId.value sessionId)
                    | Error _ -> ()
                | None -> ()
            | PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot ->
                match message.PromptKey with
                | Some key ->
                    match runtime.AcceptAgentOwnerRoot (PromptKeyRef.value key) sessionId messageId with
                    | Ok profile ->
                        onAuthorityResolved sessionId profile
                        bindUserMessage (SessionId.value sessionId) (MessageId.value messageId)
                        registerOwned (SessionId.value sessionId)
                    | Error _ -> ()
                | None -> ()
            | PromptAuthority.PromptOrigin.Continuation kind ->
                match message.PromptKey with
                | Some key ->
                    match runtime.AcceptContinuation (PromptKeyRef.value key) sessionId messageId with
                    | Ok _ ->
                        // Host first admits plugin prompts as accepted-*; the
                        // real physical user id arrives here via chat.message.
                        // Upgrade ReviewConfirmation so second PERFECT can
                        // match ConfirmationPhysicalMessageId.
                        match journal, kind with
                        | Some durable, PromptAuthority.ContinuationKind.ReviewConfirmation ->
                            let guardKey =
                                sprintf
                                    "review-guard:%s:confirm-upgrade:%s"
                                    (SessionId.value sessionId)
                                    (MessageId.value messageId)

                            AgentJournal.appendAgent
                                (StreamId.Session sessionId)
                                None
                                (AgentFact.GuardPromptAccepted
                                    {| TargetSessionId = sessionId
                                       GuardKey = guardKey
                                       HostMessageId = MessageId.value messageId |})
                                durable
                            |> ignore
                        | _ -> ()

                        bindContinuationMessage (SessionId.value sessionId) (MessageId.value messageId)
                        registerOwned (SessionId.value sessionId)
                    | Error _ -> ()
                | None -> ()
            | PromptAuthority.PromptOrigin.HostInternal
            | PromptAuthority.PromptOrigin.UnknownOrigin -> ()
        | _ -> ()

    let createHook
        (journal: AgentJournal option)
        (bindUserMessage: string -> string -> unit)
        (bindContinuationMessage: string -> string -> unit)
        (registerOwned: string -> unit)
        (onAuthorityResolved: SessionId -> PromptAuthority.AuthorityExecutionProfile -> unit)
        =
        fun (input: obj) (output: obj) ->
            PromptIngressCodec.decode input output
            |> handle journal bindUserMessage bindContinuationMessage registerOwned onAuthorityResolved
