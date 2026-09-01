namespace Wanxiangshu.Persistence.Journal

open Wanxiangshu.Composition.Durable.Fact

module FactCodec =
    val pre050MigrationMessage: string
    val tipV2CleanBreakMessage: string
    val isIgnoredLegacyDecodeError: error: string -> bool
    val containsLegacyFallbackFields: json: string -> bool
    val containsLegacyScoreVectorEntry: json: string -> bool
    val containsHandleCompletedMissingCompletionFields: json: string -> bool
    val containsLegacyUnanchoredGuideline: json: string -> bool
    val legacyGuidelineCleanBreakMessage: string
    val serializeFact: fact: Fact -> string
    val validateFact: fact: Fact -> Result<Fact, string>
    val deserializeFact: json: string -> Result<Fact, string>
