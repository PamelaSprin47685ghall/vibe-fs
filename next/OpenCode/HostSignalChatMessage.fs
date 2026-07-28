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

    let private parseModel (inputObj: obj) : OpencodeModel option =
        if isNull inputObj?model then
            None
        elif not (isNull inputObj?model?providerID) && not (isNull inputObj?model?modelID) then
            Some
                { providerID = unbox<string> inputObj?model?providerID
                  modelID = unbox<string> inputObj?model?modelID
                  variant =
                    if isNull inputObj?model?variant then
                        None
                    else
                        Some(unbox<string> inputObj?model?variant) }
        else
            None

    let private injectModel (inputObj: obj) (outputObj: obj) (model: OpencodeModel) =
        let modelObj =
            match model.variant with
            | Some variant ->
                createObj
                    [ "providerID", box model.providerID
                      "modelID", box model.modelID
                      "id", box model.modelID
                      "variant", box variant ]
            | None ->
                createObj
                    [ "providerID", box model.providerID
                      "modelID", box model.modelID
                      "id", box model.modelID ]

        inputObj?model <- modelObj

        if not (isNull outputObj) && not (isNull outputObj?message) then
            outputObj?message?model <- modelObj

    let private acceptKeyedPrompt
        (journal: AgentJournal option)
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
                sessionRoles.[sessionId] <- profile.Agent
                bindUserMessage sessionId messageId
            | Error failure -> raise (InvalidOperationException(sprintf "AgentOwnerRoot acceptance failed: %s" failure))
        | _ ->
            match svc.AcceptContinuation key sid mid with
            | Error failure -> raise (InvalidOperationException(sprintf "Continuation acceptance failed: %s" failure))
            | Ok kind ->
                if
                    kind = Some PromptAuthority.ReviewConfirmation
                    || continuationOrigin = Some "ReviewConfirmation"
                then
                    match journal with
                    | Some j ->
                        match
                            AgentJournal.appendAgent
                                (StreamId.Session sid)
                                None
                                (AgentFact.GuardPromptAccepted
                                    {| TargetSessionId = sid
                                       GuardKey = sprintf "review-guard:%s:confirm-perfect" sessionId
                                       HostMessageId = messageId |})
                                j
                        with
                        | Ok _ -> bindContinuationMessage sessionId messageId
                        | Error failure ->
                            raise (
                                InvalidOperationException(
                                    sprintf "Reviewer guard acceptance persistence failed: %A" failure.Failure
                                )
                            )
                    | None -> bindContinuationMessage sessionId messageId
                else
                    bindContinuationMessage sessionId messageId

    let createHook
        (journal: AgentJournal option)
        (sessionRoles: Dictionary<string, string>)
        (bindUserMessage: string -> string -> unit)
        (bindContinuationMessage: string -> string -> unit)
        (registerOwned: string -> unit)
        (modelConfig: ModelResolver.ModelConfig option)
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

                let explicitModel = parseModel inputObj
                let hostModel = parseModel outputObj

                let hostVariant =
                    if isNull outputObj || isNull outputObj?message || isNull outputObj?message?variant then
                        None
                    else
                        Some(unbox<string> outputObj?message?variant)

                let hostAgent =
                    if
                        not (isNull outputObj)
                        && not (isNull outputObj?message)
                        && not (isNull outputObj?message?agent)
                    then
                        HostSessionContext.canonicalRole (unbox<string> outputObj?message?agent)
                    else
                        None

                match journal, promptKey with
                | Some _, Some key when
                    not (String.IsNullOrWhiteSpace sessionId)
                    && not (String.IsNullOrWhiteSpace messageId)
                    ->
                    acceptKeyedPrompt
                        journal
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

                    let selectedModel =
                        match explicitModel with
                        | Some model -> Some model
                        | None ->
                            match journal with
                            | Some j -> ModelResolver.resolveAuthorityDefault modelConfig sid (AgentJournal.snapshot j)
                            | None -> modelConfig |> Option.map (fun c -> c.SideA)

                    match selectedModel, explicitModel with
                    | Some model, None -> injectModel inputObj outputObj model
                    | _ -> ()

                    match
                        svc.AcceptHumanRoot
                            sid
                            mid
                            canonicalAgent
                            explicitModel
                            None
                            hostAgent
                            (selectedModel |> Option.orElse hostModel)
                            hostVariant
                    with
                    | Ok profile ->
                        sessionRoles.[sessionId] <- profile.Agent
                        bindUserMessage sessionId messageId
                    | Error _ when journal.IsNone -> bindUserMessage sessionId messageId
                    | Error failure ->
                        raise (InvalidOperationException(sprintf "Authority root acceptance failed: %s" failure))
                | _ -> ())
