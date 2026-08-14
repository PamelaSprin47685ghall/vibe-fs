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
open Wanxiangshu.Interaction.Dispatch
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
    let normalizeKeywords (raw: string) : string list =
        if String.IsNullOrWhiteSpace raw then
            []
        else
            let normalized = SyntheticToml.normalizeNewlines raw

            let rec loop acc seen index (items: string array) =
                if index >= items.Length || List.length acc >= MaxKeywords then
                    List.rev acc
                else
                    let item = items.[index].Trim()

                    if item = "" || Set.contains item seen then
                        loop acc seen (index + 1) items
                    else
                        loop (item :: acc) (Set.add item seen) (index + 1) items

            loop [] Set.empty 0 (normalized.Split '\n')

    let private hintIdentity (hint: RepositoryWarmStartHint) =
        hint.FilePath, hint.StartLine, hint.EndLine, hint.Content

    let stableDedupeHints (hints: RepositoryWarmStartHint list) : RepositoryWarmStartHint list =
        let rec loop acc seen remaining =
            match remaining with
            | [] -> List.rev acc
            | head :: tail ->
                let key = hintIdentity head

                if Set.contains key seen then
                    loop acc seen tail
                else
                    loop (head :: acc) (Set.add key seen) tail

        loop [] Set.empty hints

    let private renderSearch (search: RepositoryWarmStartSearch) =
        SyntheticToml.tableArrayEntry
            "repository_search"
            [ SyntheticToml.field "ordinal" (string search.Ordinal)
              SyntheticToml.field "query" (SyntheticToml.renderString search.Query)
              SyntheticToml.field "candidate_count" (string (List.length search.Hints)) ]

    let private renderHint (hint: RepositoryWarmStartHint) =
        SyntheticToml.tableArrayEntry
            "repository_hint"
            [ SyntheticToml.field "keyword_ordinal" (string hint.KeywordOrdinal)
              SyntheticToml.field "local_rank" (string hint.LocalRank)
              SyntheticToml.field "file_path" (SyntheticToml.renderString hint.FilePath)
              SyntheticToml.field "start_line" (string hint.StartLine)
              SyntheticToml.field "end_line" (string hint.EndLine)
              SyntheticToml.field "content" (SyntheticToml.renderString hint.Content)
              SyntheticToml.field "score" (SyntheticToml.renderFloat hint.Score)
              SyntheticToml.field "total_lines" (string hint.TotalLines) ]

    let private bodyBlocks (searches: RepositoryWarmStartSearch list) (hints: RepositoryWarmStartHint list) omitted =
        [ if omitted > 0 then
              yield SyntheticToml.field "repository_hint_omitted" (string omitted)
          yield! searches |> List.map renderSearch
          yield! hints |> List.map renderHint ]

    let private renderDocument
        (instructions: string list)
        (searches: RepositoryWarmStartSearch list)
        (hints: RepositoryWarmStartHint list)
        omitted
        =
        SyntheticToml.document instructions (bodyBlocks searches hints omitted)

    /// Render while preserving whole TOML entries. If the authority header alone
    /// exceeds the warm-start byte budget, fail open to the raw charge rather than
    /// truncating authority text.
    let render (instructions: string list) (charge: string) (searches: RepositoryWarmStartSearch list) : string =
        let orderedHints =
            searches
            |> List.collect (fun search -> search.Hints)
            |> stableDedupeHints
            |> List.truncate MaxHintsTotal

        let originalCount = searches |> List.sumBy (fun search -> List.length search.Hints)
        let initialOmitted = max 0 (originalCount - List.length orderedHints)

        let rec fit kept omitted =
            let rendered = renderDocument instructions searches kept omitted

            if SyntheticToml.byteCount rendered <= MaxWarmStartBytes then
                rendered
            else
                match List.rev kept with
                | [] -> charge
                | _ :: restRev -> fit (List.rev restRev) (omitted + 1)

        fit orderedHints initialOmitted

    /// Add low-trust repository tables to an already-rendered authoritative
    /// provider prompt (ForkChildPayload). The 64 KiB budget applies only to the
    /// warm-start appendix, so a large pre-existing charge is never truncated.
    let appendToProviderPrompt
        (appendixInstructions: string list)
        (basePrompt: string)
        (searches: RepositoryWarmStartSearch list)
        : string =
        let orderedHints =
            searches
            |> List.collect (fun search -> search.Hints)
            |> stableDedupeHints
            |> List.truncate MaxHintsTotal

        let originalCount = searches |> List.sumBy (fun search -> List.length search.Hints)
        let initialOmitted = max 0 (originalCount - List.length orderedHints)

        let rec fit kept omitted =
            let appendix =
                SyntheticToml.document appendixInstructions (bodyBlocks searches kept omitted)

            if SyntheticToml.byteCount appendix <= MaxWarmStartBytes then
                basePrompt.TrimEnd() + "\n\n" + appendix
            else
                match List.rev kept with
                | [] -> basePrompt
                | _ :: restRev -> fit (List.rev restRev) (omitted + 1)

        fit orderedHints initialOmitted
