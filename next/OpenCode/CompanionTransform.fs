namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools
open CompanionProjection

module CompanionTransform =

    let bloggerSelfRebaseDue = CompanionProjection.bloggerSelfRebaseDue
    let shouldSwitchEpoch = CompanionProjection.shouldSwitchEpoch
    let estimateTokensUtf8 = CompanionProjection.estimateTokensUtf8

    let handleCompanionTransform
        (companions: Dictionary<string, CompanionHost>)
        (gate: obj)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (sessionBudgets: Dictionary<string, int>)
        (sessionOutputLimits: Dictionary<string, int>)
        (budgetStore: CompanionBudgetStore)
        (onBloggerCreated: (SessionId -> unit) option)
        (inObj: obj)
        (rawOutObj: obj)
        =
        let rawMessages = unbox<obj array> rawOutObj?messages |> Array.toList

        // COMPANION-013 idempotency: never stack a second synthetic head on a
        // message array that already carries one.
        //
        // This used to be load-bearing for a real defect: the plugin registered the
        // transform under two hook names, so both fired over the same array. That is
        // fixed at the registration site (HOST-009), and the guard remains as the
        // invariant itself — one B head per request view, whoever calls.
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
                        if isNull message?info?sessionID then
                            None
                        else
                            Some(unbox<string> message?info?sessionID)

                    let role =
                        if isNull message?info?agent then
                            None
                        else
                            Some(unbox<string> message?info?agent)

                    Some(messageSessionId, role))

        match messageContext with
        | Some(Some messageSessionId, _) when not (isNull inObj) && isNull inObj?sessionID ->
            inObj?sessionID <- messageSessionId
        | _ -> ()

        let sessionId =
            if isNull inObj || isNull inObj?sessionID then
                ""
            else
                unbox<string> inObj?sessionID

        if
            not alreadyHasBHead
            && not (String.IsNullOrWhiteSpace sessionId)
            && not (isNull rawOutObj?messages)
        then
            // COMPANION-002 eligibility source of truth: ActiveLogicalRun only.
            // Neither the message's `agent` field nor the transform input is a
            // production source — both describe what the Host is about to send,
            // not what an Authority Root fixed.
            //
            // The profile is passed whole to `hasCompanion`. The previous version
            // extracted `SelectedAgent` and re-parsed a Role out of that string,
            // which reintroduced exactly the agent-string inference the clause
            // forbids — one field away from the durable CanonicalRole it needed.
            //
            // Fully qualified: the OpenCode `PromptAuthority` facade shadows the
            // Domain module here, and adding a re-export there would be a second
            // definition of this decision.
            let eligible =
                match journal with
                | None -> false
                | Some j ->
                    (AgentJournal.snapshot j).AgentProjections.Sessions
                    |> Map.tryFind (SessionId.create sessionId)
                    |> Option.bind (fun s -> s.PromptAuthority)
                    |> Option.bind (fun auth -> auth.ActiveLogicalRun)
                    |> Option.exists Wanxiangshu.Next.Domain.PromptAuthority.hasCompanion

            if eligible then
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
                                    // COMPANION-003: Y is recorded as its own identity.
                                    // The previous version searched the parent's handle
                                    // links for the literal target `"blogger"`, which is
                                    // agent-string matching standing in for an identity —
                                    // and it also put an internal agent into the EXEC-005
                                    // resource view that AGENT-008 keeps it out of.
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
                                            // Own + bind the blogger run so idle
                                            // reconcile can NotifyTerminal and
                                            // complete the pending blog Submit.
                                            onBloggerCreated |> Option.iter (fun callback -> callback bloggerId)),
                                    ?restoredBloggerId = restoredBloggerId,
                                    ?journal = journal
                                )

                            companions.[sessionId] <- value
                            value)

                let budgetFacts =
                    let outputLimit =
                        match sessionOutputLimits.TryGetValue sessionId with
                        | true, output when output > 0 -> Some output
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

                let replacementEnabled =
                    Environment.GetEnvironmentVariable("WANXIANGSHU_DISABLE_COMPANION_REPLACEMENT")
                    <> "1"

                let applyEpoch () =
                    if replacementEnabled then
                        let memory = companion.Memory

                        match memory.LatestB with
                        | None -> ()
                        | Some latestB ->
                            let current = CompanionDelta.jsonOfMessages CanonicalJson.canonicalJson rawMessages

                            let coverageCutoff =
                                match memory.LastSuccessfulProjection with
                                | Some previous ->
                                    CompanionDelta.prefixLength
                                        CanonicalJson.equal
                                        previous
                                        current
                                        (List.length rawMessages)
                                | None -> 0

                            let digest =
                                if coverageCutoff <= 0 || coverageCutoff > List.length rawMessages then
                                    ""
                                else
                                    CompanionDelta.prefixDigest CanonicalJson.canonicalJson rawMessages coverageCutoff

                            match memory.ActivePrefixEpoch with
                            | None ->
                                match
                                    shouldSwitchEpoch budgetFacts rawMessages (Some latestB) coverageCutoff digest
                                with
                                | Some candidate ->
                                    if not companion.ReplacementActive then
                                        companion.EnablePrefixReplacement() |> ignore

                                    companion.FreezeEpoch(candidate.CutoffMessageIndex, candidate.CoveredPrefixDigest)
                                    |> ignore
                                | None -> ()
                            | Some epoch ->
                                let projected =
                                    if
                                        epoch.CutoffMessageIndex > 0
                                        && epoch.CutoffMessageIndex <= List.length rawMessages
                                    then
                                        let head =
                                            createObj
                                                [ "info",
                                                  box (
                                                      createObj
                                                          [ "id",
                                                            box (CompanionDelta.bHeadDigest sessionId epoch.EpochId)
                                                            "role", box "user" ]
                                                  )
                                                  "parts",
                                                  box [| createObj [ "type", box "text"; "text", box epoch.FrozenB ] |] ]

                                        head :: List.skip epoch.CutoffMessageIndex rawMessages
                                    else
                                        rawMessages

                                match
                                    shouldSwitchEpoch budgetFacts rawMessages (Some latestB) coverageCutoff digest
                                with
                                | Some candidate ->
                                    let currentProjectedTokens = estimateTokens projected

                                    let candidateProjectedTokens =
                                        estimateTokensUtf8 candidate.FrozenB
                                        + estimateTokens (List.skip candidate.CutoffMessageIndex rawMessages)

                                    if candidateProjectedTokens < currentProjectedTokens then
                                        companion.SwitchEpoch(
                                            candidate.CutoffMessageIndex,
                                            candidate.CoveredPrefixDigest
                                        )
                                        |> ignore
                                | None -> ()

                applyEpoch ()

                // Y self-rebase must be evaluated against the already-accumulated
                // LatestB *before* submitting the next delta. TransformRaw.Submit
                // sets the companion busy for that delta; if we check afterwards,
                // every non-empty turn permanently skips self-rebase and Y never
                // condenses even after crossing its own budget.
                let bloggerBudget = budgetStore.BudgetFor sessionId

                // If the host never recorded a blogger-model budget for this
                // primary (e.g. system.transform omitted parentID), still honour
                // the operator/test override so threshold canaries stay deterministic.
                budgetStore.Remember(sessionId, bloggerBudget)

                match companion.Memory.LatestB with
                | Some blog when bloggerSelfRebaseDue bloggerBudget blog -> companion.SelfRebase() |> ignore
                | _ -> ()

                // When a self-rebase is in flight, Submit is busy-skipped and the
                // delta is naturally deferred to the next free transform. That is
                // correct: condense first, then absorb the pending delta onto B'.
                companion.TransformRaw rawMessages |> replaceMessagesInPlace rawOutObj
