namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools
open CompanionProjection

module CompanionTransform =

    let handleCompanionTransform
        (companions: Dictionary<string, CompanionHost>)
        (gate: obj)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (onBloggerCreated: (SessionId -> unit) option)
        (inObj: obj)
        (rawOutObj: obj)
        (arming: SlotArming option)
        : Task<unit> =
        task {
            let rawMessages = unbox<obj array> rawOutObj?messages |> Array.toList

            let alreadyHasBHead =
                rawMessages
                |> List.exists (fun message ->
                    not (isNull message)
                    && not (isNull message?info)
                    && not (isNull message?info?id)
                    && (unbox<string> message?info?id).StartsWith("companion-b-head"))

            let messageContext =
                rawMessages
                |> List.tryPick (fun message ->
                    if isNull message || isNull message?info then
                        None
                    else
                        let messageSessionId =
                            if isNull message?info?sessionID then None
                            else Some(unbox<string> message?info?sessionID)

                        let role =
                            if isNull message?info?agent then None
                            else Some(unbox<string> message?info?agent)

                        Some(messageSessionId, role))

            match messageContext with
            | Some(Some messageSessionId, _) when not (isNull inObj) && isNull inObj?sessionID ->
                inObj?sessionID <- messageSessionId
            | _ -> ()

            let sessionId =
                if isNull inObj || isNull inObj?sessionID then ""
                else unbox<string> inObj?sessionID

            if
                not alreadyHasBHead
                && not (String.IsNullOrWhiteSpace sessionId)
                && not (isNull rawOutObj?messages)
            then
                let isCompanionSession =
                    match journal with
                    | None -> true
                    | Some j ->
                        SessionAssociationProjection.isCompanion
                            (SessionId.create sessionId)
                            (AgentJournal.snapshot j).AgentProjections.Associations

                if not isCompanionSession then
                    let companion =
                        lock gate (fun () ->
                            match companions.TryGetValue sessionId with
                            | true, value -> value
                            | false, _ ->
                                let durable =
                                    journal
                                    |> Option.map (fun j -> AgentJournalCompanionPort j :> ICompanionDurablePort)

                                let restoredBloggerId =
                                    match journal with
                                    | Some j ->
                                        (AgentJournal.snapshot j).AgentProjections.Sessions
                                        |> Map.tryFind (SessionId.create sessionId)
                                        |> Option.bind (fun s -> s.Companion)
                                        |> Option.bind (fun companion -> companion.BloggerSessionId)
                                        |> Option.map SessionId.value
                                    | None -> None

                                let value =
                                    new CompanionHost(
                                        SessionId.create sessionId,
                                        sessionPort,
                                        ?durable = durable,
                                        onBloggerCreated =
                                            (fun bloggerId ->
                                                onBloggerCreated
                                                |> Option.iter (fun callback -> callback bloggerId)),
                                        ?restoredBloggerId = restoredBloggerId,
                                        ?journal = journal
                                    )

                                companions.[sessionId] <- value
                                value)

                    companion.TransformRaw rawMessages |> replaceMessagesInPlace rawOutObj
                else
                    // CTX-006: a Y Session in an armed recovery slot may squash its frames
                    // before its main request. The arming is a control-flow fact from a real
                    // failure; an unarmed or unmaterial slot proceeds with the original messages.
                    let companion =
                        lock gate (fun () ->
                            match companions.TryGetValue sessionId with
                            | true, value -> value
                            | false, _ ->
                                let durable =
                                    journal
                                    |> Option.map (fun j -> AgentJournalCompanionPort j :> ICompanionDurablePort)

                                let restoredBloggerId =
                                    match journal with
                                    | Some j ->
                                        (AgentJournal.snapshot j).AgentProjections.Sessions
                                        |> Map.tryFind (SessionId.create sessionId)
                                        |> Option.bind (fun s -> s.Companion)
                                        |> Option.bind (fun companion -> companion.BloggerSessionId)
                                        |> Option.map SessionId.value
                                    | None -> None

                                let value =
                                    new CompanionHost(
                                        SessionId.create sessionId,
                                        sessionPort,
                                        ?durable = durable,
                                        onBloggerCreated =
                                            (fun bloggerId ->
                                                onBloggerCreated
                                                |> Option.iter (fun callback -> callback bloggerId)),
                                        ?restoredBloggerId = restoredBloggerId,
                                        ?journal = journal
                                    )

                                companions.[sessionId] <- value
                                value)

                    let armed =
                        match arming with
                        | Some SlotArming.ArmedByAdvance -> true
                        | _ -> false

                    do! companion.SquashIfArmedAsync armed

                    // Squash is itself the provider request for this transform; the main
                    // Blogger request will follow on the next natural turn after the fold
                    // has updated the blog projection.
                    rawOutObj?messages <- [||]

            return ()
        }
