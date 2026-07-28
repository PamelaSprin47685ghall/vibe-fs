namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools

module CompanionTransformHelpers =

    let replaceMessagesInPlace (rawOutObj: obj) (transformed: obj list) =
        emitJsExpr (rawOutObj?messages, List.toArray transformed) "$0.length = 0; $0.push(...$1);"
        |> ignore

    let defaultBloggerBudgetTokens = 32000

    let bloggerBudgetOverride () =
        match System.Environment.GetEnvironmentVariable("WANXIANGSHU_BLOGGER_CONTEXT_LIMIT") with
        | null
        | "" -> defaultBloggerBudgetTokens
        | value ->
            match System.Int32.TryParse value with
            | true, budget when budget > 0 -> budget
            | _ -> defaultBloggerBudgetTokens

    let minReservedOutputTokens = 2048
    let private bloggerBudgetByPrimary = Dictionary<string, int>()

    let rememberBloggerBudget (primarySessionId: string) (budget: int) =
        if budget > 0 then
            bloggerBudgetByPrimary.[primarySessionId] <- budget

    let bloggerBudgetForPrimary (primarySessionId: string) =
        // Always re-read the operator override. A sticky remembered model limit
        // must not permanently pin Y to 32k after a smaller fixture/override is
        // active (or vice versa: remember the min of both when both exist).
        let overrideBudget = bloggerBudgetOverride ()

        match bloggerBudgetByPrimary.TryGetValue primarySessionId with
        | true, budget when budget > 0 -> min budget overrideBudget
        | _ -> overrideBudget

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
            emitJsExpr
                text
                "(typeof Buffer !== 'undefined' && Buffer.byteLength) ? Buffer.byteLength($0, 'utf8') : new TextEncoder().encode($0).length"

    let estimateTokensUtf8 (text: string) =
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

    let bloggerSelfRebaseDue (bloggerBudgetTokens: int) (b: string) : bool =
        bloggerBudgetTokens > 0
        && float (utf8ByteLength b) >= float (bloggerBudgetTokens * 4) * 0.8
