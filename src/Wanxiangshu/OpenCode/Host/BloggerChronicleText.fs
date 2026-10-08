namespace Wanxiangshu.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

module BloggerChronicleText =

    let private bloggerChronicleTextSemanticPath =
        "cognitive-environment/blogger-chronicle-text"

    let private bloggerChronicleText (language: ProviderLanguage) =
        ProviderProse.render language bloggerChronicleTextSemanticPath Map.empty

    let private bloggerChronicleTextModelPrefixes: string list = [ "step-3.5-flash" ]

    let private bloggerChronicleTextEnabled (sessionId: string) (physicalUserMessageId: PhysicalUserMessageId) =
        ModelRouting.tryReadExecution
            { SessionId = SessionId.create sessionId
              PhysicalUserMessageId = physicalUserMessageId }
        |> Option.map (fun lease -> ModelRouting.toOpenCodeModel lease.Identity.Target)
        |> Option.exists (fun model ->
            List.exists
                (fun prefix -> model.modelID.StartsWith(prefix, StringComparison.Ordinal))
                bloggerChronicleTextModelPrefixes)

    let private rawMessageRole (message: obj) =
        if isNull message then
            ""
        elif not (isNull message?info) && not (isNull message?info?role) then
            string message?info?role
        elif not (isNull message?role) then
            string message?role
        else
            ""

    let private rawMessageParts (message: obj) : obj array =
        if isNull message || isNull message?parts then
            [||]
        else
            unbox<obj array> message?parts

    let private isBloggerChronicleTextPart (part: obj) =
        if isNull part || isNull part?``type`` || isNull part?text then
            false
        else
            let text = string part?text

            string part?``type`` = "text"
            && (text = bloggerChronicleText ProviderLanguage.SimplifiedChinese
                || text = bloggerChronicleText ProviderLanguage.English)

    let private isBloggerChronicleTextMessage (message: obj) =
        let parts = rawMessageParts message

        rawMessageRole message = "assistant"
        && parts.Length = 1
        && isBloggerChronicleTextPart parts.[0]

    let private bloggerChronicleTextMessageId (sessionId: string) (physicalUserMessageId: PhysicalUserMessageId) =
        let digest =
            HostDigest.sha256Hex (
                String.concat
                    "\u001f"
                    [ sessionId
                      "blogger-chronicle-text"
                      PhysicalUserMessageId.value physicalUserMessageId ]
            )

        "text-" + digest.Substring(0, 24)

    let private bloggerChronicleTextMessage (messageId: string) (text: string) =
        let part = createObj [ "type", box "text"; "text", box text ]

        createObj
            [ "info", box (createObj [ "id", box messageId; "role", box "assistant" ])
              "parts", box [| part |] ]

    let private insertBloggerChronicleTextAtFrontier (marker: obj) (messages: obj list) =
        match List.rev messages with
        | last :: rest when rawMessageRole last = "user" -> List.rev rest @ [ marker; last ]
        | _ -> messages @ [ marker ]

    let maybeInject
        (journal: AgentJournal option)
        (projectionSessionIdOpt: string option)
        (physicalUserMessageId: PhysicalUserMessageId option)
        (language: ProviderLanguage)
        (outObj: obj)
        =
        match journal, projectionSessionIdOpt, physicalUserMessageId with
        | Some durable, Some sessionId, Some physical when
            bloggerChronicleTextEnabled sessionId physical
            && SessionAssociationProjection.isCompanion
                (SessionId.create sessionId)
                (AgentJournal.snapshot durable).AgentProjections.Associations
            ->
            let messages =
                unbox<obj array> outObj?messages
                |> Array.toList
                |> List.filter (isBloggerChronicleTextMessage >> not)

            let messageId = bloggerChronicleTextMessageId sessionId physical
            let text = bloggerChronicleText language
            let marker = bloggerChronicleTextMessage messageId text

            HostMessageProjection.replaceMessagesInPlace outObj (insertBloggerChronicleTextAtFrontier marker messages)
        | _ -> ()
