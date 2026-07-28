namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

module HostSignalChatMessage =

    let private service (journal: AgentJournal option) =
        match journal with
        | Some j -> PromptDispatcher.forJournal j
        | None -> PromptDispatcher.ephemeral ()

    let private acceptKeyedPrompt
        (svc: PromptAuthorityService)
        (sessionRoles: Dictionary<string, string>)
        (bindUserMessage: string -> string -> unit)
        (bindContinuationMessage: string -> string -> unit)
        (sessionId: string)
        (messageId: string)
        (key: string)
        (continuationOrigin: string option)
        =
        let sid = SessionId.create sessionId
        let mid = MessageId.create messageId

        match continuationOrigin with
        | Some "AgentOwnerRoot" ->
            match svc.AcceptAgentOwnerRoot key sid mid with
            | Ok profile ->
                sessionRoles.[sessionId] <- PromptAuthority.roleLabel profile.CanonicalRole
                bindUserMessage sessionId messageId
            | Error failure -> raise (InvalidOperationException(sprintf "AgentOwnerRoot acceptance failed: %s" failure))
        | _ ->
            match svc.AcceptContinuation key sid mid with
            | Error failure -> raise (InvalidOperationException(sprintf "Continuation acceptance failed: %s" failure))
            | Ok _kind ->
                // GuardPromptAccepted is written by HostReviewGuard on successful
                // send. chat.message only accepts the claim and binds the physical
                // confirmation message — re-appending double-counts.
                bindContinuationMessage sessionId messageId

    let createHook
        (journal: AgentJournal option)
        (sessionRoles: Dictionary<string, string>)
        (bindUserMessage: string -> string -> unit)
        (bindContinuationMessage: string -> string -> unit)
        (registerOwned: string -> unit)
        : obj =
        box (fun (inputObj: obj) (outputObj: obj) ->
            if not (isNull inputObj) then
                let sessionId =
                    if isNull inputObj?sessionID then
                        ""
                    else
                        unbox<string> inputObj?sessionID

                let messageId =
                    if not (isNull inputObj?messageID) then
                        unbox<string> inputObj?messageID
                    elif not (isNull inputObj?messageId) then
                        unbox<string> inputObj?messageId
                    elif
                        not (isNull outputObj)
                        && not (isNull outputObj?message)
                        && not (isNull outputObj?message?id)
                    then
                        unbox<string> outputObj?message?id
                    elif
                        not (isNull outputObj)
                        && not (isNull outputObj?info)
                        && not (isNull outputObj?info?id)
                    then
                        unbox<string> outputObj?info?id
                    elif not (isNull outputObj) && not (isNull outputObj?id) then
                        unbox<string> outputObj?id
                    else
                        ""

                let explicitAgent =
                    if not (isNull inputObj?agent) then
                        Some(unbox<string> inputObj?agent)
                    else
                        None

                registerOwned sessionId

                let canonicalAgent = explicitAgent |> Option.bind HostSessionContext.canonicalRole
                canonicalAgent |> Option.iter (fun role -> sessionRoles.[sessionId] <- role)

                let svc = service journal

                let promptKey, continuationOrigin =
                    ChatMessageOrigin.extractPromptKey inputObj outputObj svc sessionId

                let hostAgent =
                    if
                        not (isNull outputObj)
                        && not (isNull outputObj?message)
                        && not (isNull outputObj?message?agent)
                    then
                        Some(unbox<string> outputObj?message?agent)
                    else
                        None

                match journal, promptKey with
                | Some _, Some key when
                    not (String.IsNullOrWhiteSpace sessionId)
                    && not (String.IsNullOrWhiteSpace messageId)
                    ->
                    acceptKeyedPrompt
                        svc
                        sessionRoles
                        bindUserMessage
                        bindContinuationMessage
                        sessionId
                        messageId
                        key
                        continuationOrigin
                | None, Some _ -> bindContinuationMessage sessionId messageId
                | _, None when
                    not (String.IsNullOrWhiteSpace sessionId)
                    && not (String.IsNullOrWhiteSpace messageId)
                    ->
                    let sid = SessionId.create sessionId
                    let mid = MessageId.create messageId

                    match svc.AcceptHumanRoot sid mid explicitAgent hostAgent with
                    | Ok profile ->
                        sessionRoles.[sessionId] <- PromptAuthority.roleLabel profile.CanonicalRole
                        bindUserMessage sessionId messageId
                    | Error _ when journal.IsNone -> bindUserMessage sessionId messageId
                    | Error failure ->
                        raise (InvalidOperationException(sprintf "Authority root acceptance failed: %s" failure))
                | _ -> ())
