namespace Wanxiangshu.Repository.Investigation.WarmStart

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection

open System
open Wanxiangshu.Foundation

/// AGENT-032 neutral repository-orientation DTO. Infrastructure-owned Semble hits
/// are mapped here before any provider-facing rendering.
type RepositoryWarmStartHint =
    { KeywordOrdinal: int
      LocalRank: int
      FilePath: string
      StartLine: int
      EndLine: int
      Content: string
      Score: float
      TotalLines: int }

type RepositoryWarmStartSearch =
    { Ordinal: int
      Query: string
      Hints: RepositoryWarmStartHint list }

/// AGENT-032: canonical low-trust Repository Warm Start prompt renderer.
/// Instruction prose is loaded by call sites (PROMPT-019); this module assembles only.
[<RequireQualifiedAccess>]
module RepositoryWarmStartPrompt =

    let ChargeEnvelope = "lifecycle/warm-start/charge-envelope"
    let Appendix = "lifecycle/warm-start/appendix"

    let MaxKeywords = 8
    let TopKPerKeyword = 4
    let MaxHintsTotal = 24
    let MaxWarmStartBytes = 64 * 1024

    let isDirectConsumer (role: Role) =
        match role with
        | Role.Coder
        | Role.Inspector
        | Role.DevOps -> true
        | _ -> false

    /// Newline normalization + trim + blank removal + stable exact dedupe.
    /// Exact means case-sensitive by design.
    let private collectKeywordItem acc seen index (items: string array) =
        let item = items.[index].Trim()

        if item = "" || Set.contains item seen then
            acc, seen
        else
            item :: acc, Set.add item seen

    let private collectKeywords (items: string array) =
        let rec loop acc seen index =
            if index >= items.Length || List.length acc >= MaxKeywords then
                List.rev acc
            else
                let nextAcc, nextSeen = collectKeywordItem acc seen index items
                loop nextAcc nextSeen (index + 1)

        loop [] Set.empty 0

    let normalizeKeywords (raw: string) : string list =
        if String.IsNullOrWhiteSpace raw then
            []
        else
            raw
            |> LlmFacing.normalizeNewlines
            |> fun normalized -> normalized.Split '\n'
            |> collectKeywords

    let private hintIdentity (hint: RepositoryWarmStartHint) =
        hint.FilePath, hint.StartLine, hint.EndLine, hint.Content

    let stableDedupeHints (hints: RepositoryWarmStartHint list) : RepositoryWarmStartHint list =
        hints
        |> List.fold
            (fun (acc, seen) head ->
                let key = hintIdentity head

                if Set.contains key seen then
                    acc, seen
                else
                    head :: acc, Set.add key seen)
            ([], Set.empty)
        |> fst
        |> List.rev

    let private renderSearch (search: RepositoryWarmStartSearch) =
        LlmFacing.Data.tableArray
            "repository_search"
            [ LlmFacing.Data.intMember "ordinal" search.Ordinal
              LlmFacing.Data.stringMember "query" search.Query
              LlmFacing.Data.intMember "candidate_count" (List.length search.Hints) ]

    let private renderHint (hint: RepositoryWarmStartHint) =
        LlmFacing.Data.tableArray
            "repository_hint"
            [ LlmFacing.Data.intMember "keyword_ordinal" hint.KeywordOrdinal
              LlmFacing.Data.intMember "local_rank" hint.LocalRank
              LlmFacing.Data.stringMember "file_path" hint.FilePath
              LlmFacing.Data.intMember "start_line" hint.StartLine
              LlmFacing.Data.intMember "end_line" hint.EndLine
              LlmFacing.Data.stringMember "content" hint.Content
              LlmFacing.Data.floatMember "score" hint.Score
              LlmFacing.Data.intMember "total_lines" hint.TotalLines ]

    let private bodyBlocks (searches: RepositoryWarmStartSearch list) (hints: RepositoryWarmStartHint list) omitted =
        [ if omitted > 0 then
              yield LlmFacing.Data.intField "repository_hint_omitted" omitted
          yield! searches |> List.map renderSearch
          yield! hints |> List.map renderHint ]

    let private document
        (instructions: string list)
        (searches: RepositoryWarmStartSearch list)
        (hints: RepositoryWarmStartHint list)
        omitted
        =
        LlmFacing.instructions instructions
        |> LlmFacing.withData (bodyBlocks searches hints omitted)

    let rec private fitToBudget build fallback kept omitted =
        let candidate = build kept omitted

        if candidate |> LlmFacing.render |> LlmFacing.byteCount <= MaxWarmStartBytes then
            candidate
        else
            trimOverBudget build fallback kept omitted

    and private trimOverBudget build fallback kept omitted =
        match List.rev kept with
        | [] -> fallback
        | _ :: restRev -> fitToBudget build fallback (List.rev restRev) (omitted + 1)

    let private orderedHintsAndOmitted searches =
        let orderedHints =
            searches
            |> List.collect (fun search -> search.Hints)
            |> stableDedupeHints
            |> List.truncate MaxHintsTotal

        let originalCount = searches |> List.sumBy (fun search -> List.length search.Hints)
        orderedHints, max 0 (originalCount - List.length orderedHints)

    let buildDocument
        (instructions: string list)
        (fallbackInstruction: string)
        (searches: RepositoryWarmStartSearch list)
        : LlmFacing.Document =
        let orderedHints, initialOmitted = orderedHintsAndOmitted searches

        fitToBudget
            (fun kept omitted -> document instructions searches kept omitted)
            (LlmFacing.instruction fallbackInstruction)
            orderedHints
            initialOmitted

    /// Render while preserving whole TOML entries. If the authority header alone
    /// exceeds the warm-start byte budget, fail open to the raw charge rather than
    /// truncating authority text.
    let render (instructions: string list) (charge: string) (searches: RepositoryWarmStartSearch list) : string =
        buildDocument instructions charge searches |> LlmFacing.render

    /// Add low-trust repository tables before the one final render. The 64 KiB
    /// budget applies only to this appendix document.
    let appendToDocument
        (appendixInstructions: string list)
        (baseDocument: LlmFacing.Document)
        (searches: RepositoryWarmStartSearch list)
        : LlmFacing.Document =
        let orderedHints, initialOmitted = orderedHintsAndOmitted searches

        let appendix =
            fitToBudget
                (fun kept omitted -> document appendixInstructions searches kept omitted)
                LlmFacing.empty
                orderedHints
                initialOmitted

        LlmFacing.combine [ baseDocument; appendix ]
