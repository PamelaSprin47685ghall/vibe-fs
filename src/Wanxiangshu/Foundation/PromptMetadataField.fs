namespace Wanxiangshu.Foundation

/// Wire field names carried on the Host prompt boundary.
///
/// PROMPT-011: the PromptKey must reach Host metadata, because it is the only
/// anchor through which an unresolved claim can be reconciled after a crash.
module PromptMetadataField =

    [<Literal>]
    let PromptKeyField = "wanxiangshu_prompt_key"

    [<Literal>]
    let OriginField = "wanxiangshu_origin"

    [<Literal>]
    let LogicalRunField = "wanxiangshu_logical_run"
