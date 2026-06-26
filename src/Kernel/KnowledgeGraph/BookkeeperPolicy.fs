module Wanxiangshu.Kernel.KnowledgeGraph.BookkeeperPolicy

open Wanxiangshu.Kernel.ToolCatalog

/// Subagent and IO tools that always feed the knowledge graph bookkeeper when
/// they succeed. File mutations also qualify via `isFileEditTool` (edit, write,
/// apply_patch, patch alias, ast_* …). Review, lookup, and graph tools themselves
/// are intentionally absent — including `submit_review`.
let bookkeepingSubagentTools =
    Set [ "coder"; "investigator"; "meditator"; "browser"; "executor"; "websearch"; "webfetch"; "write"; "apply_patch"; "patch" ]

let recordsToBookkeeper (toolName: string) : bool =
    isFileEditTool toolName || Set.contains toolName bookkeepingSubagentTools