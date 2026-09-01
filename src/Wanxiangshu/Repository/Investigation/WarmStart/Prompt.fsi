namespace Wanxiangshu.Repository.Investigation.WarmStart

open Wanxiangshu.Foundation

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

[<RequireQualifiedAccess>]
module RepositoryWarmStartPrompt =
    val ChargeEnvelope: string
    val Appendix: string
    val MaxKeywords: int
    val TopKPerKeyword: int
    val MaxHintsTotal: int
    val MaxWarmStartBytes: int
    val isDirectConsumer: role: Role -> bool
    val normalizeKeywords: raw: string -> string list
    val stableDedupeHints: hints: RepositoryWarmStartHint list -> RepositoryWarmStartHint list

    val buildDocument:
        instructions: string list ->
        fallbackInstruction: string ->
        searches: RepositoryWarmStartSearch list ->
            LlmFacing.Document

    val render: instructions: string list -> charge: string -> searches: RepositoryWarmStartSearch list -> string

    val appendToDocument:
        appendixInstructions: string list ->
        baseDocument: LlmFacing.Document ->
        searches: RepositoryWarmStartSearch list ->
            LlmFacing.Document
