namespace Wanxiangshu.Foundation

/// Wire field names carried on the Host prompt boundary.
///
/// PROMPT-011: the PromptKey must reach Host metadata, because it is the only
/// anchor through which an unresolved claim can be reconciled after a crash.
///
/// These are the single source of truth for the dispatch-correlation field
/// names: every write side and every read side refers to them here, so one
/// wire field never carries two literals.
module PromptMetadataField =
    [<Literal>]
    val PromptKeyField: string = "wanxiangshu_prompt_key"

    [<Literal>]
    val OriginField: string = "wanxiangshu_origin"

    [<Literal>]
    val LogicalRunField: string = "wanxiangshu_logical_run"
