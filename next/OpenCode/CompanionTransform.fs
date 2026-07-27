namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools

module CompanionTransform =

    /// OpenCode reads the original messages array after the trigger, so the
    /// transformed projection must be spliced into that array in place;
    /// replacing output.messages with a new reference is silently dropped.
    let replaceMessagesInPlace (rawOutObj: obj) (transformed: obj list) =
        emitJsExpr (rawOutObj?messages, List.toArray transformed) "$0.length = 0; $0.push(...$1);"
        |> ignore

    /// Frozen Host budget contract: the messages.transform input is empty, so
    /// the real context budget is captured from the later
    /// experimental.chat.system.transform hook ({ sessionID, model }) of the
    /// previous request and keyed by session. estimatedTokens is the
    /// deterministic chars/4 estimator over the canonical messages JSON;
    /// replacement activates once the estimate crosses activationRatio of the
    /// real model limit (never a fixed byte threshold). Before the first
    /// budget capture no activation can happen.
    let activationRatio = 0.8

    /// Conservative default blogger context budget (tokens), used only when the
    /// blogger child has not yet reported its own model limit via the
    /// system.transform hook. The blogger model is cheap and usually smaller
    /// than X, so this is intentionally below typical X limits.
    let defaultBloggerBudgetTokens = 32000

    /// Y self-rebase predicate: the current B's estimated token count has
    /// crossed 0.8 of the blogger model's context budget. Independent of X
    /// prefix replacement; no ReplacementActive gate.
    let bloggerSelfRebaseDue (bloggerBudget: int) (b: string) : bool =
        bloggerBudget > 0
        && float ((String.length b + 3) / 4) >= float bloggerBudget * activationRatio

    let estimateTokens (messages: obj list) =
        let json = Projection.canonicalJson (List.toArray messages)
        (String.length json + 3) / 4

    let handleCompanionTransform
        (companions: Dictionary<string, CompanionHost>)
        (gate: obj)
        (sessionPort: ISessionHostPort)
        (outputBoundary: IEventOutputBoundaryPort option)
        (journal: AgentJournal option)
        (sessionBudgets: Dictionary<string, int>)
        (sessionRoles: Dictionary<string, string>)
        (bloggerModel: Result<OpencodeModel, string>)
        (inObj: obj)
        (rawOutObj: obj)
        =
        let rawMsgs = unbox<obj array> rawOutObj?messages |> Array.toList

        let hasBHead =
            rawMsgs
            |> List.exists (fun msg ->
                if isNull msg || isNull msg?info || isNull msg?info?id then
                    false
                else
                    let id = unbox<string> msg?info?id
                    id = "companion-b-head" || id.StartsWith("companion-b-head-"))

        if hasBHead then
            ()
        else
            let messageContext =
                rawMsgs
                |> List.tryPick (fun message ->
                    if isNull message || isNull message?info then
                        None
                    else
                        let sessionId =
                            if isNull message?info?sessionID then
                                None
                            else
                                Some(unbox<string> message?info?sessionID)

                        let role =
                            if isNull message?info?agent then
                                None
                            else
                                Some(unbox<string> message?info?agent)

                        Some(sessionId, role))

            match messageContext with
            | Some(Some sessionId, _) when not (isNull inObj) && isNull inObj?sessionID -> inObj?sessionID <- sessionId
            | _ -> ()

            let sessionId =
                if isNull inObj || isNull inObj?sessionID then
                    ""
                else
                    unbox<string> inObj?sessionID

            if not (String.IsNullOrWhiteSpace sessionId) && not (isNull rawOutObj?messages) then
                let rawAgentRole =
                    match sessionRoles.TryGetValue sessionId with
                    | true, role -> Some role
                    | _ ->
                        match messageContext |> Option.bind snd with
                        | Some role -> Some role
                        | None when not (isNull inObj) && not (isNull inObj?agent) -> Some(unbox<string> inObj?agent)
                        | None -> None

                let agentRole = rawAgentRole |> Option.bind HostSessionContext.canonicalRole

                if not (sessionRoles.ContainsKey sessionId) then
                    agentRole |> Option.iter (fun role -> sessionRoles.[sessionId] <- role)

                let allowed =
                    match agentRole with
                    | None -> false
                    | Some _ -> Companion.shouldCreateForAgent agentRole

                if allowed then
                    let companion =
                        lock gate (fun () ->
                            match companions.TryGetValue sessionId with
                            | true, value -> value
                            | false, _ ->
                                let durable =
                                    journal
                                    |> Option.map (fun j -> AgentJournalCompanionPort j :> ICompanionDurablePort)

                                // Load the restored blogger child ID from the
                                // journal linkage, if any. On plugin warm-reload
                                // (host process persists), the existing blogger
                                // session is still alive and can be reused
                                // without a reset frame.
                                let restoredBloggerId =
                                    match journal with
                                    | Some j ->
                                        (AgentJournal.snapshot j).AgentProjections.Sessions
                                        |> Map.tryFind (SessionId.create sessionId)
                                        |> Option.bind (fun s -> s.Linkage)
                                        |> Option.bind (fun linkage ->
                                            linkage.LinkedChildren
                                            |> Map.toSeq
                                            |> Seq.tryPick (fun (childId, target) ->
                                                if target = "blogger" then
                                                    Some(ChildId.value childId)
                                                else
                                                    None))
                                    | None -> None

                                let value =
                                    new CompanionHost(
                                        SessionId.create sessionId,
                                        sessionPort,
                                        ?durable = durable,
                                        ?bloggerModel = Some bloggerModel,
                                        ?outputBoundary = outputBoundary,
                                        onBloggerCreated =
                                            (fun bloggerId -> sessionRoles.[SessionId.value bloggerId] <- "blogger"),
                                        ?restoredBloggerId = restoredBloggerId
                                    )

                                companions.[sessionId] <- value
                                value)

                    let rawMsgs = unbox<obj array> rawOutObj?messages |> Array.toList

                    let tokenEstimate = estimateTokens rawMsgs

                    if not companion.Memory.ReplacementActive then
                        match sessionBudgets.TryGetValue sessionId with
                        | true, budget when budget > 0 && float tokenEstimate >= float budget * activationRatio ->
                            companion.EnablePrefixReplacement() |> ignore
                        | _ -> ()

                    // Y self-rebase: independent of X prefix replacement. Triggers
                    // when B's estimated tokens cross 0.8 of the BLOGGER child's own
                    // model context budget. Uses LatestB (Y's mutable working memory),
                    // never the FrozenB from ActivePrefixEpoch.
                    match companion.Memory.LatestB with
                    | Some b ->
                        let bloggerBudget =
                            match companion.BloggerSession with
                            | Some bloggerId ->
                                match sessionBudgets.TryGetValue(SessionId.value bloggerId) with
                                | true, budget when budget > 0 -> budget
                                | _ -> defaultBloggerBudgetTokens
                            | None -> defaultBloggerBudgetTokens

                        if bloggerSelfRebaseDue bloggerBudget b then
                            companion.SelfRebase() |> ignore
                    | None -> ()

                    replaceMessagesInPlace rawOutObj (companion.TransformRaw rawMsgs)
