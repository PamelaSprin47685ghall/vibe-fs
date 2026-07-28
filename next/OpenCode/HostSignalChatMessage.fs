namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

module HostSignalChatMessage =

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

                let agent =
                    if not (isNull inputObj?agent) then
                        Some(unbox<string> inputObj?agent)
                    else
                        None

                registerOwned sessionId

                let canonicalAgent = agent |> Option.bind HostSessionContext.canonicalRole

                canonicalAgent |> Option.iter (fun role -> sessionRoles.[sessionId] <- role)

                // `chat.message` is the only external prompt-acceptance
                // boundary. A plugin continuation has a pre-recorded key;
                // it must not replace the active Authority Root here.
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

                let selectedModel =
                    if isNull inputObj?model then
                        None
                    elif not (isNull inputObj?model?providerID) && not (isNull inputObj?model?modelID) then
                        Some(unbox<string> inputObj?model?providerID, unbox<string> inputObj?model?modelID)
                    else
                        None

                // Session does not own a permanent model. Host continues the last
                // user prompt's model. When the user omits model and durable
                // Fallback Side is B (or LastAuthority BaseModel is set), inject
                // that model into this user message so the Host continues it.
                // Explicit user model always wins and starts a fresh Authority Root.
                let selectedModel =
                    match selectedModel, modelConfig, journal, promptKey with
                    | Some _, _, _, _ -> selectedModel
                    | None, Some cfg, Some j, None when not (String.IsNullOrWhiteSpace sessionId) ->
                        let sid = SessionId.create sessionId
                        let proj = AgentJournal.snapshot j

                        match ModelResolver.resolveForSession cfg sid proj with
                        | Some m ->
                            let modelObj =
                                createObj
                                    [ "providerID", box m.providerID
                                      "modelID", box m.modelID
                                      "id", box m.modelID ]

                            inputObj?model <- modelObj

                            if not (isNull outputObj) && not (isNull outputObj?message) then
                                outputObj?message?model <- modelObj

                            Some(m.providerID, m.modelID)
                        | None -> None
                    | _ -> selectedModel

                // Authority Root only when there is no plugin PromptKey. A keyed message is
                // always a claimed continuation (or abandoned claim), never HumanRoot.
                match journal, canonicalAgent, promptKey with
                | Some j, Some role, None when
                    not (String.IsNullOrWhiteSpace sessionId)
                    && not (String.IsNullOrWhiteSpace messageId)
                    ->
                    match
                        AgentJournal.appendAgent
                            (StreamId.Session(SessionId.create sessionId))
                            (Some(TurnId.ofMessageId (MessageId.create messageId)))
                            (AgentFact.AuthorityRootAccepted
                                {| SessionId = SessionId.create sessionId
                                   LogicalRunId = Guid.NewGuid().ToString("N")
                                   HostMessageId = messageId
                                   AuthorityKind = "HumanRoot"
                                   Agent = role
                                   BaseProviderID = selectedModel |> Option.map fst
                                   BaseModelID = selectedModel |> Option.map snd
                                   Variant = None |})
                            j
                    with
                    | Ok _ -> bindUserMessage sessionId messageId
                    | Error failure ->
                        raise (
                            InvalidOperationException(
                                sprintf "Authority root persistence failed: %A" failure.Failure
                            )
                        )
                | None, _, None when
                    not (String.IsNullOrWhiteSpace sessionId)
                    && not (String.IsNullOrWhiteSpace messageId)
                    ->
                    // No journal: still bind physical user for fallback identity only.
                    bindUserMessage sessionId messageId
                | Some j, _, Some key when
                    not (String.IsNullOrWhiteSpace sessionId)
                    && not (String.IsNullOrWhiteSpace messageId)
                    ->
                    // Map Host physical id → claimed continuation. This is the
                    // only durable proof after Host strips transport metadata.
                    match
                        AgentJournal.appendAgent
                            (StreamId.Session(SessionId.create sessionId))
                            None
                            (AgentFact.PluginPromptAccepted
                                {| PromptKey = key
                                   SessionId = SessionId.create sessionId
                                   HostMessageId = messageId |})
                            j
                    with
                    | Error failure ->
                        raise (
                            InvalidOperationException(
                                sprintf "Continuation acceptance persistence failed: %A" failure.Failure
                            )
                        )
                    | Ok _ ->
                        // Content marker only for review confirmation (not every Guard).
                        if continuationOrigin = Some "ReviewConfirmation" then
                            match
                                AgentJournal.appendAgent
                                    (StreamId.Session(SessionId.create sessionId))
                                    None
                                    (AgentFact.GuardPromptAccepted
                                        {| TargetSessionId = SessionId.create sessionId
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
                        else
                            bindContinuationMessage sessionId messageId
                | _, _, Some _ ->
                    // Keyed continuation without journal: still not an Authority Root.
                    bindContinuationMessage sessionId messageId
                | _ ->
                    // UnknownOrigin / incomplete context: fail-closed — do not
                    // invent HumanRoot, do not bind authority-changing user root.
                    ()

                // Fallback is attempt-local. A newly accepted authority root
                // must not be rewritten to a prior run's Side B selection.
                ())
