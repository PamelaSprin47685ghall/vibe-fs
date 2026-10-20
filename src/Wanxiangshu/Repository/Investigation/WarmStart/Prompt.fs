namespace Wanxiangshu.Repository.Investigation.WarmStart

open System
open Fable.Core
open Fable.Core.JsInterop
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

    /// The `wanxiangli/warm-start` owner (CUT-29 ①) of the warm-start law:
    /// keyword normalization, per-keyword ranking, ordinal merge, stable
    /// de-duplication, the total cap and whole-entry byte budgeting. This host
    /// keeps only the TOML rendering and its byte measure.
    type private WarmStartLawLibrary =
        abstract maxKeywords: int
        abstract topKPerKeyword: int
        abstract maxHintsTotal: int
        abstract maxWarmStartBytes: int
        abstract isDirectConsumer: role: string -> bool
        abstract normalizeKeywords: raw: string -> string array
        abstract rankHits: ordinal: int * hits: obj array -> obj array
        abstract select: searches: obj array -> obj
        abstract fit: count: int * initialOmitted: int * measure: Func<int, int, int> * budget: int -> obj

    [<Import("WarmStartLaw", "wanxiangli/warm-start")>]
    let private warmStartLawClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : WarmStartLawLibrary = jsNative

    [<Emit("$0[$1]")>]
    let private field (value: obj) (name: string) : obj = jsNative

    let private law = construct warmStartLawClass

    let ChargeEnvelope = "lifecycle/warm-start/charge-envelope"
    let Appendix = "lifecycle/warm-start/appendix"

    let MaxKeywords = law.maxKeywords
    let TopKPerKeyword = law.topKPerKeyword
    let MaxHintsTotal = law.maxHintsTotal
    let MaxWarmStartBytes = law.maxWarmStartBytes

    let isDirectConsumer (role: Role) = law.isDirectConsumer (Roles.roleLabel role)

    let normalizeKeywords (raw: string) : string list =
        law.normalizeKeywords raw |> List.ofArray

    /// The law ranks hits by reference; the host stamps its neutral DTO.
    let rankHits (ordinal: int) (hits: 'hit list) (toHint: int -> int -> 'hit -> RepositoryWarmStartHint) =
        law.rankHits (ordinal, hits |> List.map box |> Array.ofList)
        |> Array.map (fun ranked ->
            toHint
                (unbox<int> (field ranked "keywordOrdinal"))
                (unbox<int> (field ranked "localRank"))
                (unbox<'hit> (field ranked "hit")))
        |> List.ofArray

    let private identityView (hint: RepositoryWarmStartHint) : obj =
        createObj
            [ "filePath" ==> hint.FilePath
              "startLine" ==> hint.StartLine
              "endLine" ==> hint.EndLine
              "content" ==> hint.Content
              "hint" ==> hint ]

    let private searchView (search: RepositoryWarmStartSearch) : obj =
        createObj
            [ "ordinal" ==> search.Ordinal
              "hints" ==> (search.Hints |> List.map identityView |> Array.ofList)
              "search" ==> search ]

    let private selected (searches: RepositoryWarmStartSearch list) =
        let selection = law.select (searches |> List.map searchView |> Array.ofList)

        let ordered =
            unbox<obj array> (field selection "searches")
            |> Array.map (fun view -> unbox<RepositoryWarmStartSearch> (field view "search"))
            |> List.ofArray

        let hints =
            unbox<obj array> (field selection "hints")
            |> Array.map (fun view -> unbox<RepositoryWarmStartHint> (field view "hint"))
            |> List.ofArray

        ordered, hints, unbox<int> (field selection "omitted")

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

    /// Whole entries only: the law picks how many hints fit the byte budget as
    /// measured on this host's rendering; when even none fit, `fallback`.
    let private fitted build fallback (hints: RepositoryWarmStartHint list) initialOmitted =
        let measure =
            Func<int, int, int>(fun kept omitted ->
                build (List.truncate kept hints) omitted |> LlmFacing.render |> LlmFacing.byteCount)

        let verdict =
            law.fit (List.length hints, initialOmitted, measure, MaxWarmStartBytes)

        if isNull verdict then
            fallback
        else
            build (List.truncate (unbox<int> (field verdict "kept")) hints) (unbox<int> (field verdict "omitted"))

    let buildDocument
        (instructions: string list)
        (fallbackInstruction: string)
        (searches: RepositoryWarmStartSearch list)
        : LlmFacing.Document =
        let ordered, hints, omitted = selected searches

        fitted
            (fun kept omitted -> document instructions ordered kept omitted)
            (LlmFacing.instruction fallbackInstruction)
            hints
            omitted

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
        let ordered, hints, omitted = selected searches

        let appendix =
            fitted
                (fun kept omitted -> document appendixInstructions ordered kept omitted)
                LlmFacing.empty
                hints
                omitted

        LlmFacing.combine [ baseDocument; appendix ]
