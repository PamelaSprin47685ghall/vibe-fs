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

    let handleTransform (rawOutObj: obj) =
        if not (isNull rawOutObj) && not (isNull rawOutObj?messages) then
            let rawMsgs = unbox<obj array> rawOutObj?messages |> Array.toList

            // MessageV2 WithParts shape: toModelMessages drops system-role
            // entries and requires parts, so context synthetics are user-role.
            let capsMsg =
                createObj
                    [ "info", box (createObj [ "id", box "caps-head"; "role", box "user" ])
                      "parts",
                      box [| createObj [ "type", box "text"; "text", box "[CAPS: coder, inspector, browser]" ] |] ]

            let transformed = Projection.preserveRawTail [ capsMsg ] rawMsgs
            replaceMessagesInPlace rawOutObj transformed

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
        handleTransform rawOutObj

        let rawMsgs = unbox<obj array> rawOutObj?messages |> Array.toList

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

                            let value =
                                new CompanionHost(
                                    SessionId.create sessionId,
                                    sessionPort,
                                    ?durable = durable,
                                    ?bloggerModel = Some bloggerModel,
                                    ?outputBoundary = outputBoundary,
                                    onBloggerCreated =
                                        (fun bloggerId -> sessionRoles.[SessionId.value bloggerId] <- "blogger")
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
                // model context budget (the cheap blogger model usually has a
                // smaller limit than X). We use the blogger budget once the child
                // has reported one via the system.transform hook, else a
                // conservative default. No ReplacementActive gate: X replacement
                // and Y self-rebase are orthogonal. Never synthesize a
                // placeholder: if the Blogger is busy we skip; if it fails we
                // keep the old B.
                match companion.Memory.CurrentB with
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
