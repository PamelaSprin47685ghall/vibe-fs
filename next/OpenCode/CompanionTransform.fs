namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools
open CompanionTransformHelpers

module CompanionTransform =

    let bloggerSelfRebaseDue = CompanionTransformHelpers.bloggerSelfRebaseDue
    let shouldSwitchEpoch = CompanionTransformHelpers.shouldSwitchEpoch
    let rememberedBloggerBudget = CompanionTransformHelpers.rememberBloggerBudget
    let bloggerBudgetForPrimary = CompanionTransformHelpers.bloggerBudgetForPrimary
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
        (onBloggerCreated: (SessionId -> unit) option)
        (inObj: obj)
        (rawOutObj: obj)
        =
        let rawMessages = unbox<obj array> rawOutObj?messages |> Array.toList

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

            if Companion.shouldCreateForAgent agentRole then
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
                                        (fun bloggerId ->
                                            let key = SessionId.value bloggerId
                                            sessionRoles.[key] <- "blogger"
                                            // Own + bind the blogger run so idle
                                            // reconcile can NotifyTerminal and
                                            // complete the pending blog Submit.
                                            onBloggerCreated |> Option.iter (fun callback -> callback bloggerId)),
                                    ?restoredBloggerId = restoredBloggerId
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
                            let current = CompanionDelta.jsonOfMessages Projection.canonicalJson rawMessages

                            let coverageCutoff =
                                match memory.LastSuccessfulProjection with
                                | Some previous ->
                                    CompanionDelta.prefixLength
                                        Projection.messageId
                                        Projection.sameCanonicalMessage
                                        previous
                                        current
                                        (List.length rawMessages)
                                | None -> 0

                            let digest =
                                if coverageCutoff <= 0 || coverageCutoff > List.length rawMessages then
                                    ""
                                else
                                    CompanionDelta.prefixDigest Projection.canonicalJson rawMessages coverageCutoff

                            match memory.ActivePrefixEpoch with
                            | None ->
                                match
                                    shouldSwitchEpoch budgetFacts rawMessages (Some latestB) coverageCutoff digest
                                with
                                | Some candidate ->
                                    if not memory.ReplacementActive then
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
                let bloggerBudget = bloggerBudgetForPrimary sessionId

                // If the host never recorded a blogger-model budget for this
                // primary (e.g. system.transform omitted parentID), still honour
                // the operator/test override so threshold canaries stay deterministic.
                rememberBloggerBudget sessionId bloggerBudget

                match companion.Memory.LatestB with
                | Some blog when bloggerSelfRebaseDue bloggerBudget blog -> companion.SelfRebase() |> ignore
                | _ -> ()

                // When a self-rebase is in flight, Submit is busy-skipped and the
                // delta is naturally deferred to the next free transform. That is
                // correct: condense first, then absorb the pending delta onto B'.
                companion.TransformRaw rawMessages |> replaceMessagesInPlace rawOutObj
