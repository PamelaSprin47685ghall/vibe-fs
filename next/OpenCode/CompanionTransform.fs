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

    /// Budget is captured from experimental.chat.system.transform
    /// (model.limit.context/input/output). Fail-closed when context is unknown.
    /// Token estimate uses UTF-8 bytes ÷ 3 (SSOT conservative estimator).
    let defaultBloggerBudgetTokens = 32000
    let minReservedOutputTokens = 2048

    type BudgetFacts =
        { ContextLimit: int
          InputLimit: int option
          OutputLimit: int option }

    type EpochCandidate =
        { CutoffMessageIndex: int
          CoveredPrefixDigest: string
          FrozenB: string }

    let utf8ByteLength (text: string) : int =
        if isNull text || text = "" then
            0
        else
            // Fable/JS: TextEncoder / Buffer (no System.Text.Encoding)
            emitJsExpr
                text
                "(typeof Buffer !== 'undefined' && Buffer.byteLength) ? Buffer.byteLength($0, 'utf8') : new TextEncoder().encode($0).length"

    let estimateTokensUtf8 (text: string) =
        // UTF-8 bytes ÷ 3 (SSOT conservative estimator when provider estimator absent)
        let bytes = utf8ByteLength text
        max 0 ((bytes + 2) / 3)

    let estimateTokens (messages: obj list) =
        let json = Projection.canonicalJson (List.toArray messages)
        estimateTokensUtf8 json

    let reservedOutputTokens (budget: BudgetFacts) =
        match budget.OutputLimit with
        | Some out when out > 0 -> max minReservedOutputTokens out
        | _ -> minReservedOutputTokens

    let effectiveContextLimit (budget: BudgetFacts) =
        // ContextLimit = min of known positive bounds; unknown context → fail closed (0)
        if budget.ContextLimit <= 0 then
            0
        else
            match budget.InputLimit with
            | Some inp when inp > 0 -> min budget.ContextLimit inp
            | _ -> budget.ContextLimit

    /// Pure epoch decision (SSOT):
    /// Switch when ProjectedInput + ReservedOutput > ContextLimit
    /// AND tokens(FrozenCandidate)+tokens(rawTail) < tokens(current)
    /// AND coverage digest can be computed at a positive cutoff.
    let shouldSwitchEpoch
        (budget: BudgetFacts)
        (messages: obj list)
        (latestB: string option)
        (cutoffMessageIndex: int)
        (coveredPrefixDigest: string)
        : EpochCandidate option =
        match latestB with
        | None -> None
        | Some frozenB when cutoffMessageIndex <= 0 || cutoffMessageIndex > List.length messages -> None
        | Some frozenB when String.IsNullOrWhiteSpace coveredPrefixDigest -> None
        | Some frozenB ->
            let contextLimit = effectiveContextLimit budget

            if contextLimit <= 0 then
                None
            else
                let projected = estimateTokens messages
                let reserved = reservedOutputTokens budget
                let exceeds = projected + reserved > contextLimit

                if not exceeds then
                    None
                else
                    let tail = messages |> List.skip cutoffMessageIndex
                    let candidateTokens = estimateTokensUtf8 frozenB + estimateTokens tail
                    let currentTokens = projected

                    if candidateTokens >= currentTokens then
                        None
                    else
                        Some
                            { CutoffMessageIndex = cutoffMessageIndex
                              CoveredPrefixDigest = coveredPrefixDigest
                              FrozenB = frozenB }

    let bloggerSelfRebaseDue (bloggerBudget: int) (b: string) : bool =
        bloggerBudget > 0
        && float (estimateTokensUtf8 b) >= float bloggerBudget * 0.8

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

                    let budgetFacts =
                        match sessionBudgets.TryGetValue sessionId with
                        | true, context when context > 0 ->
                            // Optional input/output stored as negative-key conventions are not used;
                            // systemTransformHook may set context only. Fail-closed on missing context.
                            { ContextLimit = context
                              InputLimit = None
                              OutputLimit = None }
                        | _ ->
                            { ContextLimit = 0
                              InputLimit = None
                              OutputLimit = None }

                    // Pure epoch decision.
                    // First freeze: raw projection exceeds budget and BlogBase coverage
                    // gives a positive cutoff; FrozenB = LatestB once.
                    // Later switch: only when the *post-replacement* projection
                    // (FrozenB + rawTail) still exceeds budget and a new LatestB
                    // freezes to a strictly shorter projection. Self-rebase never freezes.
                    let tryApplyEpoch () =
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

                                match shouldSwitchEpoch budgetFacts rawMsgs (Some latestB) coverageCutoff digest with
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
                                    if epoch.CutoffMessageIndex > 0
                                       && epoch.CutoffMessageIndex <= List.length rawMsgs then
                                        let synthetic =
                                            createObj
                                                [ "info",
                                                  box (
                                                      createObj
                                                          [ "id", box "companion-b-head"
                                                            "role", box "user" ]
                                                  )
                                                  "parts",
                                                  box
                                                      [| createObj
                                                             [ "type", box "text"
                                                               "text", box epoch.FrozenB ] |] ]

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

                    replaceMessagesInPlace rawOutObj (companion.TransformRaw rawMsgs)
