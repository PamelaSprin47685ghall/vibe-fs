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
        // Host schema rejects null variant; omit the field when None.
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

                // sessionRoles is display/tool-surface cache only; never authority source.
                canonicalAgent |> Option.iter (fun role -> sessionRoles.[sessionId] <- role)

                let promptKey =
                    if isNull inputObj?metadata || isNull inputObj?metadata?wanxiangshu_prompt_key then
                        None
                    else
                        Some(unbox<string> inputObj?metadata?wanxiangshu_prompt_key)

                let continuationOrigin =
                    if isNull inputObj?metadata || isNull inputObj?metadata?wanxiangshu_origin then
                        None
                    else
                        Some(unbox<string> inputObj?metadata?wanxiangshu_origin)

                let explicitModel = parseModel inputObj
                let hostModel = parseModel outputObj
                let hostVariant =
                    if isNull outputObj || isNull outputObj?message || isNull outputObj?message?variant then
                        None
                    else
                        Some(unbox<string> outputObj?message?variant)

                let hostAgent =
                    if not (isNull outputObj) && not (isNull outputObj?message) && not (isNull outputObj?message?agent) then
                        HostSessionContext.canonicalRole (unbox<string> outputObj?message?agent)
                    else
                        None

                let svc = service journal

                match journal, promptKey with
                | Some _, Some key when
                    not (String.IsNullOrWhiteSpace sessionId)
                    && not (String.IsNullOrWhiteSpace messageId)
                    ->
                    let sid = SessionId.create sessionId
                    let mid = MessageId.create messageId

                    match continuationOrigin with
                    | Some "AgentOwnerRoot" ->
                        match svc.AcceptAgentOwnerRoot key sid mid with
                        | Ok profile ->
                            sessionRoles.[sessionId] <- profile.Agent
                            bindUserMessage sessionId messageId
                        | Error failure ->
                            raise (InvalidOperationException(sprintf "AgentOwnerRoot acceptance failed: %s" failure))
                    | _ ->
                        match svc.AcceptContinuation key sid mid with
                        | Error failure ->
                            raise (
                                InvalidOperationException(
                                    sprintf "Continuation acceptance failed: %s" failure
                                )
                            )
                        | Ok kind ->
                            if kind = Some PromptAuthority.ReviewConfirmation
                               || continuationOrigin = Some "ReviewConfirmation" then
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
                                                sprintf
                                                    "Reviewer guard acceptance persistence failed: %A"
                                                    failure.Failure
                                            )
                                        )
                                | None -> bindContinuationMessage sessionId messageId
                            else
                                bindContinuationMessage sessionId messageId
                | None, Some _ ->
                    // Keyed continuation without journal: still not an Authority Root.
                    bindContinuationMessage sessionId messageId
                | _, None when
                    not (String.IsNullOrWhiteSpace sessionId)
                    && not (String.IsNullOrWhiteSpace messageId)
                    ->
                    let sid = SessionId.create sessionId
                    let mid = MessageId.create messageId

                    // Omit-model inherits LastAuthority.BaseModel only, never Side B.
                    let selectedModel =
                        match explicitModel with
                        | Some model -> Some model
                        | None ->
                            match journal with
                            | Some j ->
                                ModelResolver.resolveAuthorityDefault
                                    modelConfig
                                    sid
                                    (AgentJournal.snapshot j)
                            | None -> modelConfig |> Option.map (fun c -> c.SideA)

                    match selectedModel, explicitModel with
                    | Some model, None -> injectModel inputObj outputObj model
                    | _ -> ()

                    match
                        svc.AcceptHumanRoot
                            sid
                            mid
                            (canonicalAgent)
                            explicitModel
                            None
                            hostAgent
                            (selectedModel |> Option.orElse hostModel)
                            hostVariant
                    with
                    | Ok profile ->
                        sessionRoles.[sessionId] <- profile.Agent
                        bindUserMessage sessionId messageId
                    | Error _ when journal.IsNone ->
                        // No journal: still bind physical user for fallback identity only.
                        bindUserMessage sessionId messageId
                    | Error failure ->
                        // Missing agent on first root without host default is fail-closed.
                        raise (
                            InvalidOperationException(
                                sprintf "Authority root acceptance failed: %s" failure
                            )
                        )
                | _ ->
                    // UnknownOrigin / incomplete context: fail-closed — do not
                    // invent HumanRoot, do not bind authority-changing user root.
                    ()
        )
