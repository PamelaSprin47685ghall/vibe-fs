namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.OpenCode

/// Per-plugin Blogger budget cache; never shared across plugin instances.
type CompanionBudgetStore() =
    let byPrimary = Dictionary<string, int>()

    let overrideBudget () =
        match Environment.GetEnvironmentVariable("WANXIANGSHU_BLOGGER_CONTEXT_LIMIT") with
        | null
        | "" -> 32000
        | value ->
            match Int32.TryParse value with
            | true, budget when budget > 0 -> budget
            | _ -> 32000

    member _.Remember(primarySessionId: string, budget: int) =
        if budget > 0 then
            byPrimary.[primarySessionId] <- budget

    member _.TryFind(primarySessionId: string) =
        match byPrimary.TryGetValue primarySessionId with
        | true, budget -> Some budget
        | _ -> None

    member _.BudgetFor(primarySessionId: string) =
        let configured = overrideBudget ()

        match byPrimary.TryGetValue primarySessionId with
        | true, budget when budget > 0 -> min budget configured
        | _ -> configured

module CompanionProjection =

    let defaultBloggerBudgetTokens = 32000

    type BudgetFacts =
        { ContextLimit: int
          InputLimit: int option
          OutputLimit: int option }

    type ValidatedEpochCandidate =
        { CutoffMessageIndex: int
          CoveredPrefixDigest: string
          FrozenB: string
          UncoveredRawTailCount: int
          CandidateTokenCount: int }

    let utf8ByteLength (text: string) : int =
        if String.IsNullOrEmpty text then
            0
        else
            emitJsExpr text "new TextEncoder().encode($0).length"

    let estimateTokensUtf8 (text: string) =
        let bytes = utf8ByteLength text
        max 0 ((bytes + 2) / 3)

    let estimateTokens (messages: obj list) =
        let json = CanonicalJson.canonicalJson (List.toArray messages)
        estimateTokensUtf8 json

    let minReservedOutputTokens = 2048

    let reservedOutputTokens (budget: BudgetFacts) =
        match budget.OutputLimit with
        | Some out when out > 0 -> max minReservedOutputTokens out
        | _ -> minReservedOutputTokens

    let effectiveContextLimit (budget: BudgetFacts) =
        if budget.ContextLimit <= 0 then
            0
        else
            match budget.InputLimit with
            | Some inp when inp > 0 -> min budget.ContextLimit inp
            | _ -> budget.ContextLimit

    let shouldSwitchEpoch
        (budget: BudgetFacts)
        (messages: obj list)
        (latestB: string option)
        (cutoffMessageIndex: int)
        (coveredPrefixDigest: string)
        : ValidatedEpochCandidate option =
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
                              FrozenB = frozenB
                              UncoveredRawTailCount = List.length tail
                              CandidateTokenCount = candidateTokens }

    let bloggerSelfRebaseDue (bloggerBudgetTokens: int) (b: string) : bool =
        bloggerBudgetTokens > 0
        && float (utf8ByteLength b) >= float (bloggerBudgetTokens * 4) * 0.8

    let replaceMessagesInPlace (rawOutObj: obj) (transformed: obj list) =
        emitJsExpr (rawOutObj?messages, List.toArray transformed) "$0.length = 0; $0.push(...$1);"
        |> ignore
