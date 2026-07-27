namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools
open CompanionTransformHelpers

module CompanionTransform =

    let bloggerSelfRebaseDue = CompanionTransformHelpers.bloggerSelfRebaseDue
    let shouldSwitchEpoch = CompanionTransformHelpers.shouldSwitchEpoch
    let estimateTokensUtf8 = CompanionTransformHelpers.estimateTokensUtf8

    let handleCompanionTransform
        (companions: Dictionary<string, CompanionHost>)
        (gate: obj)
        (sessionPort: ISessionHostPort)
        (outputBoundary: IEventOutputBoundaryPort option)
        (journal: AgentJournal option)
        (sessionBudgets: Dictionary<string, int>)
        (sessionOutputLimits: Dictionary<string, int>)
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

                    let budgetFacts =
                        let outputLimit =
                            match sessionOutputLimits.TryGetValue sessionId with
                            | true, out when out > 0 -> Some out
                            | _ -> None

                        match sessionBudgets.TryGetValue sessionId with
                        | true, context when context > 0 ->
                            { ContextLimit = context
                              InputLimit = None
                              OutputLimit = outputLimit }
                        | _ ->
                            { ContextLimit = 0
                              InputLimit = None
                              OutputLimit = outputLimit }

                    let replacementDisabled =
                        Environment.GetEnvironmentVariable("WANXIANGSHU_DISABLE_COMPANION_REPLACEMENT") = "1"

                    let replacementEnabled = not replacementDisabled

                    // Pure epoch decision.
                    // First freeze: raw projection exceeds budget and BlogBase coverage
                    // gives a positive cutoff; FrozenB = LatestB once.
                    // Later switch: only when the *post-replacement* projection
                    // (FrozenB + rawTail) still exceeds budget and a new LatestB
                    // freezes to a strictly shorter projection. Self-rebase never freezes.
                    let tryApplyEpoch () =
                        if replacementEnabled then
                            let memory = companion.Memory

                            match memory.LatestB with
                            | None -> ()
                            | Some latestB ->
                                let current = CompanionDelta.jsonOfMessages Projection.canonicalJson rawMsgs

                                let coverageCutoff =
                                    match memory.LastSuccessfulProjection with
                                    | Some previous ->
                                        CompanionDelta.prefixLength
                                            Projection.messageId
                                            Projection.sameCanonicalMessage
                                            previous
                                            current
                                            (List.length rawMsgs)
                                    | None -> 0

                                match memory.ActivePrefixEpoch with
                                | None ->
                                    let digest =
                                        if coverageCutoff <= 0 || coverageCutoff > List.length rawMsgs then
                                            ""
                                        else
                                            CompanionDelta.prefixDigest Projection.canonicalJson rawMsgs coverageCutoff

                                    match
                                        shouldSwitchEpoch budgetFacts rawMsgs (Some latestB) coverageCutoff digest
                                    with
                                    | None -> ()
                                    | Some candidate ->
                                        if not memory.ReplacementActive then
                                            companion.EnablePrefixReplacement() |> ignore

                                        companion.FreezeEpoch(
                                            candidate.CutoffMessageIndex,
                                            candidate.CoveredPrefixDigest
                                        )
                                        |> ignore

                                | Some epoch ->
                                    // Projected form under current freeze: FrozenB + messages[cutoff..]
                                    let projected =
                                        if
                                            epoch.CutoffMessageIndex > 0
                                            && epoch.CutoffMessageIndex <= List.length rawMsgs
                                        then
                                            let synthetic =
                                                createObj
                                                    [ "info",
                                                      box (
                                                          createObj [ "id", box "companion-b-head"; "role", box "user" ]
                                                      )
                                                      "parts",
                                                      box
                                                          [| createObj [ "type", box "text"; "text", box epoch.FrozenB ] |] ]

                                            synthetic :: List.skip epoch.CutoffMessageIndex rawMsgs
                                        else
                                            rawMsgs

                                    let projectedTokens = estimateTokens projected
                                    let reserved = reservedOutputTokens budgetFacts
                                    let contextLimit = effectiveContextLimit budgetFacts

                                    if contextLimit > 0 && projectedTokens + reserved > contextLimit then
                                        // Re-freeze LatestB covering full current raw list only when
                                        // that candidate is strictly shorter than the projected form.
                                        let newCutoff = List.length rawMsgs

                                        let digest =
                                            if newCutoff <= 0 then
                                                ""
                                            else
                                                CompanionDelta.prefixDigest Projection.canonicalJson rawMsgs newCutoff

                                        let candidateTokens = estimateTokensUtf8 latestB // empty tail after full cutoff

                                        if candidateTokens < projectedTokens && digest <> "" then
                                            companion.SwitchEpoch(newCutoff, digest) |> ignore

                    tryApplyEpoch ()

                    // Y self-rebase: LatestB only (never ActivePrefixEpoch).
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

                    let finalMsgs =
                        // Submit/Blogger delta accumulation (companion.TransformRaw's
                        // internal companion.Submit call) must run unconditionally so
                        // LatestB keeps advancing even while replacement is disabled --
                        // only the epoch-based injection depends on replacementEnabled,
                        // and it naturally no-ops here because tryApplyEpoch() (above)
                        // never freezes/switches an epoch while replacementEnabled=false,
                        // so memory.ReplacementActive stays false and TransformRaw's
                        // injection branches never match.
                        companion.TransformRaw rawMsgs

                    replaceMessagesInPlace rawOutObj finalMsgs
