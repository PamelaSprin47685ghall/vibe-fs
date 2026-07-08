module Wanxiangshu.Tests.ArchitectureTestsSubagentCatalog

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let subagentToolsUseToolCatalogRequiredKeys () =
    let catalog = requireFile "src/Kernel/ToolCatalog/Registry.fs" |> nonCommentCode
    check "arch: ToolCatalog defines subagentRequiredKeys" (catalog.Contains "let subagentRequiredKeys")
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: Mux SubagentTools uses subagentRequiredKeys for coder" (mux.Contains "subagentRequiredKeys \"coder\"")

    check
        "arch: Mux SubagentTools uses subagentRequiredKeys for investigator"
        (mux.Contains "subagentRequiredKeys \"investigator\"")

    check
        "arch: Mux SubagentTools uses subagentRequiredKeys for meditator"
        (mux.Contains "subagentRequiredKeys \"meditator\"")

    check
        "arch: Mux SubagentTools uses subagentRequiredKeys for browser"
        (mux.Contains "subagentRequiredKeys \"browser\"")

    check
        "arch: Mux SubagentTools must not hardcode [| intents; tdd |]"
        (not (mux.Contains "[| \"intents\"; \"tdd\" |]"))

    check
        "arch: Mux SubagentTools must not hardcode [| intents |] required array"
        (not (mux.Contains "[| \"intents\" |]"))

    check
        "arch: Mux SubagentTools must not hardcode [| intent; files |]"
        (not (mux.Contains "[| \"intent\"; \"files\" |]"))

    check
        "arch: Mux SubagentTools must not hardcode [| intent |] required array"
        (not (mux.Contains "[| \"intent\" |]"))

    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode

    check
        "arch: Opencode SubagentTools uses subagentRequiredKeys for coder"
        (opencode.Contains "subagentRequiredKeys \"coder\"")

    check
        "arch: Opencode SubagentTools uses subagentRequiredKeys for investigator"
        (opencode.Contains "subagentRequiredKeys \"investigator\"")

    check
        "arch: Opencode SubagentTools uses subagentRequiredKeys for meditator"
        (opencode.Contains "subagentRequiredKeys \"meditator\"")

    check
        "arch: Opencode SubagentTools uses subagentRequiredKeys for browser"
        (opencode.Contains "subagentRequiredKeys \"browser\"")

    check "arch: Opencode SubagentTools uses subagentZodShape" (opencode.Contains "subagentZodShape")

    check
        "arch: Opencode SubagentTools must not hardcode [| intents; tdd |]"
        (not (opencode.Contains "[| \"intents\"; \"tdd\" |]"))

    check
        "arch: Opencode SubagentTools must not hardcode [| intents |] required array"
        (not (opencode.Contains "[| \"intents\" |]"))

    check
        "arch: Opencode SubagentTools must not hardcode [| intent; files |]"
        (not (opencode.Contains "[| \"intent\"; \"files\" |]"))

    check
        "arch: Opencode SubagentTools must not hardcode [| intent |] required array"
        (not (opencode.Contains "[| \"intent\" |]"))

    let toolSchema = requireFile "src/Opencode/ToolSchema.fs" |> nonCommentCode
    check "arch: Opencode ToolSchema defines subagentZodShape" (toolSchema.Contains "let subagentZodShape")

let kernelToolArgsExists () =
    let code = requireFile "src/Kernel/ToolArgs.fs" |> nonCommentCode
    check "arch: Kernel ToolArgs defines ToolArgs DU" (code.Contains "type ToolArgs =")
    check "arch: Kernel ToolArgs must not define CoderIntents" (not (code.Contains "CoderIntents"))
    check "arch: Kernel ToolArgs must not define InvestigatorIntents" (not (code.Contains "InvestigatorIntents"))

let toolArgsDecodeExists () =
    let code = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    check "arch: ToolArgsDecode defines decodeToolArgs" (code.Contains "let decodeToolArgs")
    check "arch: ToolArgsDecode defines decodeToolInvocation" (code.Contains "let decodeToolInvocation")
    check "arch: ToolArgsDecode defines DecodedToolInvocation" (code.Contains "type DecodedToolInvocation =")
    check "arch: DecodedToolInvocation defines CoderBatch" (code.Contains "CoderBatch")
    check "arch: DecodedToolInvocation defines InvestigatorBatch" (code.Contains "InvestigatorBatch")

let toolArgsDecodeCoversMajorTools () =
    let code = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    check "arch: ToolArgsDecode mentions websearch" (code.Contains "websearch")
    check "arch: ToolArgsDecode mentions webfetch" (code.Contains "webfetch")
    check "arch: ToolArgsDecode mentions executor" (code.Contains "executor")
    check "arch: ToolArgsDecode uses decodeWebsearchArgs" (code.Contains "decodeWebsearchArgs")
    check "arch: ToolArgsDecode uses decodeWebfetchArgs" (code.Contains "decodeWebfetchArgs")
    check "arch: ToolArgsDecode uses decodeExecutorArgs" (code.Contains "decodeExecutorArgs")
    check "arch: ToolArgsDecode mentions todowrite" (code.Contains "todowrite")
    check "arch: ToolArgsDecode mentions apply_patch" (code.Contains "apply_patch")
    check "arch: ToolArgsDecode mentions submit_review" (code.Contains "submit_review")
    check "arch: ToolArgsDecode uses decodeTodoWriteArgs" (code.Contains "decodeTodoWriteArgs")
    check "arch: ToolArgsDecode uses decodeApplyPatchFields" (code.Contains "decodeApplyPatchFields")
    check "arch: ToolArgsDecode uses decodeSubmitReviewArgs" (code.Contains "decodeSubmitReviewArgs")

let decodedToolInvocationNoObj () =
    let code = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    check "arch: DecodedToolInvocation must not carry intents obj" (not (code.Contains "intents: obj"))
    check "arch: DecodedToolInvocation must not define SubagentIntents case" (not (code.Contains "SubagentIntents of"))
