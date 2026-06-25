module VibeFs.Tests.ArchitectureTestsSubagent

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

let muxSubagentToolsUsesToolCopy () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let shell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    check "arch: Mux SubagentTools must not open ToolCopy (Shell owns copy)"
        (not (mux.Contains "ToolCopy"))
    check "arch: MuxSubagentToolExecute opens ToolCopy"
        (shell.Contains "ToolCopy")
    check "arch: MuxSubagentToolExecute uses muxToolRequiresWorkspaceId"
        (shell.Contains "muxToolRequiresWorkspaceId")
    check "arch: MuxSubagentToolExecute must not inline requires workspaceId template"
        (not (shell.Contains "requires workspaceId"))

let muxSubagentToolsUsesFromMuxConfig () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let shell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    check "arch: Mux SubagentTools must not open ToolRuntimeContext (Shell owns config)"
        (not (mux.Contains "ToolRuntimeContext"))
    check "arch: MuxSubagentToolExecute opens ToolRuntimeContext"
        (shell.Contains "ToolRuntimeContext")
    check "arch: MuxSubagentToolExecute uses fromMuxConfig"
        (shell.Contains "fromMuxConfig")
    check "arch: MuxSubagentToolExecute must not strField config workspaceId"
        (not (shell.Contains "strField config \"workspaceId\""))

let muxSubagentToolsUsesSubagentToolPolicy () =
    let code = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: Mux SubagentTools calls SubagentToolPolicy.disabledToolNamesForRole"
        (code.Contains "SubagentToolPolicy.disabledToolNamesForRole")
    check "arch: Mux SubagentTools disabledToolsForRole delegates to policy with muxSpawnToolUniverse"
        (code.Contains "disabledToolNamesForRole mux toolNames role muxSpawnToolUniverse")
    check "arch: Mux SubagentTools must not filter with canUseForHost locally"
        (not (code.Contains "canUseForHost"))
    check "arch: Mux SubagentTools must not call deniedToolsForHost locally"
        (not (code.Contains "deniedToolsForHost"))

let subagentToolsUseKernelPromptHelpers () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let muxShell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    let shellExec = requireFile "src/Shell/SubagentToolExecute.fs" |> nonCommentCode
    for (label, code) in [| "SubagentToolExecute", shellExec; "MuxSubagentToolExecute", muxShell |] do
        check ("arch: " + label + " uses promptsFromCoderIntents")
            (code.Contains "promptsFromCoderIntents")
        check ("arch: " + label + " uses meditatorPromptFromFiles")
            (code.Contains "meditatorPromptFromFiles")
        check ("arch: " + label + " uses browserPromptText")
            (code.Contains "browserPromptText")
    check "arch: Opencode SubagentTools must not call promptsForParallelIntents locally"
        (not (opencode.Contains "promptsForParallelIntents"))
    check "arch: Opencode SubagentTools must not call meditatorPromptText locally"
        (not (opencode.Contains "meditatorPromptText"))
    check "arch: Opencode SubagentTools must not call buildMeditatorSections locally"
        (not (opencode.Contains "buildMeditatorSections"))
    check "arch: Mux SubagentTools must not call promptsForParallelIntents locally"
        (not (mux.Contains "promptsForParallelIntents"))
    check "arch: Mux SubagentTools must not call meditatorPromptText locally"
        (not (mux.Contains "meditatorPromptText"))
    check "arch: Mux SubagentTools must not call buildMeditatorSections locally"
        (not (mux.Contains "buildMeditatorSections"))
    check "arch: Mux SubagentTools must not call formatPrompt opencode (Coder"
        (not (mux.Contains "formatPrompt opencode (Coder"))
    check "arch: Mux SubagentTools must not call formatPrompt Host.Mimocode (Coder"
        (not (mux.Contains "formatPrompt Host.Mimocode (Coder"))
    check "arch: Mux SubagentTools must not call formatPrompt opencode (Investigator"
        (not (mux.Contains "formatPrompt opencode (Investigator"))
    check "arch: Mux SubagentTools must not call formatPrompt Host.Mimocode (Investigator"
        (not (mux.Contains "formatPrompt Host.Mimocode (Investigator"))
    check "arch: Mux SubagentTools must not call formatPrompt opencode (Meditator"
        (not (mux.Contains "formatPrompt opencode (Meditator"))
    check "arch: Mux SubagentTools must not call formatPrompt Host.Mimocode (Meditator"
        (not (mux.Contains "formatPrompt Host.Mimocode (Meditator"))
    check "arch: Mux SubagentTools must not call formatPrompt opencode (Browser"
        (not (mux.Contains "formatPrompt opencode (Browser"))
    check "arch: Mux SubagentTools must not call formatPrompt Host.Mimocode (Browser"
        (not (mux.Contains "formatPrompt Host.Mimocode (Browser"))

let subagentToolsUseDecodeIntentsField () =
    let codec = requireFile "src/Shell/SubagentSimpleArgsCodec.fs" |> nonCommentCode
    let decode = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    check "arch: SubagentSimpleArgsCodec defines decodeIntentsField"
        (codec.Contains "let decodeIntentsField")
    check "arch: ToolArgsDecode must not use decodeIntentsField"
        (not (decode.Contains "decodeIntentsField"))
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: Mux SubagentTools must not use decodeIntentsField"
        (not (mux.Contains "decodeIntentsField"))
    check "arch: Mux SubagentTools must not Dyn.get args intents"
        (not (mux.Contains "Dyn.get args \"intents\""))

let subagentToolsUseToolCatalogRequiredKeys () =
    let catalog = requireFile "src/Kernel/ToolCatalog.fs" |> nonCommentCode
    check "arch: ToolCatalog defines subagentRequiredKeys"
        (catalog.Contains "let subagentRequiredKeys")
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: Mux SubagentTools uses subagentRequiredKeys for coder"
        (mux.Contains "subagentRequiredKeys \"coder\"")
    check "arch: Mux SubagentTools uses subagentRequiredKeys for investigator"
        (mux.Contains "subagentRequiredKeys \"investigator\"")
    check "arch: Mux SubagentTools uses subagentRequiredKeys for meditator"
        (mux.Contains "subagentRequiredKeys \"meditator\"")
    check "arch: Mux SubagentTools uses subagentRequiredKeys for browser"
        (mux.Contains "subagentRequiredKeys \"browser\"")
    check "arch: Mux SubagentTools must not hardcode [| intents; tdd |]"
        (not (mux.Contains "[| \"intents\"; \"tdd\" |]"))
    check "arch: Mux SubagentTools must not hardcode [| intents |] required array"
        (not (mux.Contains "[| \"intents\" |]"))
    check "arch: Mux SubagentTools must not hardcode [| intent; files |]"
        (not (mux.Contains "[| \"intent\"; \"files\" |]"))
    check "arch: Mux SubagentTools must not hardcode [| intent |] required array"
        (not (mux.Contains "[| \"intent\" |]"))
    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools uses subagentRequiredKeys for coder"
        (opencode.Contains "subagentRequiredKeys \"coder\"")
    check "arch: Opencode SubagentTools uses subagentRequiredKeys for investigator"
        (opencode.Contains "subagentRequiredKeys \"investigator\"")
    check "arch: Opencode SubagentTools uses subagentRequiredKeys for meditator"
        (opencode.Contains "subagentRequiredKeys \"meditator\"")
    check "arch: Opencode SubagentTools uses subagentRequiredKeys for browser"
        (opencode.Contains "subagentRequiredKeys \"browser\"")
    check "arch: Opencode SubagentTools uses subagentZodShape"
        (opencode.Contains "subagentZodShape")
    check "arch: Opencode SubagentTools must not hardcode [| intents; tdd |]"
        (not (opencode.Contains "[| \"intents\"; \"tdd\" |]"))
    check "arch: Opencode SubagentTools must not hardcode [| intents |] required array"
        (not (opencode.Contains "[| \"intents\" |]"))
    check "arch: Opencode SubagentTools must not hardcode [| intent; files |]"
        (not (opencode.Contains "[| \"intent\"; \"files\" |]"))
    check "arch: Opencode SubagentTools must not hardcode [| intent |] required array"
        (not (opencode.Contains "[| \"intent\" |]"))
    let toolSchema = requireFile "src/Opencode/ToolSchema.fs" |> nonCommentCode
    check "arch: Opencode ToolSchema defines subagentZodShape"
        (toolSchema.Contains "let subagentZodShape")

let kernelToolArgsExists () =
    let code = requireFile "src/Kernel/ToolArgs.fs" |> nonCommentCode
    check "arch: Kernel ToolArgs defines ToolArgs DU"
        (code.Contains "type ToolArgs =")
    check "arch: Kernel ToolArgs must not define CoderIntents"
        (not (code.Contains "CoderIntents"))
    check "arch: Kernel ToolArgs must not define InvestigatorIntents"
        (not (code.Contains "InvestigatorIntents"))

let toolArgsDecodeExists () =
    let code = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    check "arch: ToolArgsDecode defines decodeToolArgs"
        (code.Contains "let decodeToolArgs")
    check "arch: ToolArgsDecode defines decodeToolInvocation"
        (code.Contains "let decodeToolInvocation")
    check "arch: ToolArgsDecode defines DecodedToolInvocation"
        (code.Contains "type DecodedToolInvocation =")
    check "arch: DecodedToolInvocation defines CoderBatch"
        (code.Contains "CoderBatch")
    check "arch: DecodedToolInvocation defines InvestigatorBatch"
        (code.Contains "InvestigatorBatch")

let toolArgsDecodeCoversMajorTools () =
    let code = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    check "arch: ToolArgsDecode mentions websearch"
        (code.Contains "websearch")
    check "arch: ToolArgsDecode mentions webfetch"
        (code.Contains "webfetch")
    check "arch: ToolArgsDecode mentions executor"
        (code.Contains "executor")
    check "arch: ToolArgsDecode uses decodeWebsearchArgs"
        (code.Contains "decodeWebsearchArgs")
    check "arch: ToolArgsDecode uses decodeWebfetchArgs"
        (code.Contains "decodeWebfetchArgs")
    check "arch: ToolArgsDecode uses decodeExecutorArgs"
        (code.Contains "decodeExecutorArgs")
    check "arch: ToolArgsDecode mentions todowrite"
        (code.Contains "todowrite")
    check "arch: ToolArgsDecode mentions knowledge_graph_fetch"
        (code.Contains "knowledge_graph_fetch")
    check "arch: ToolArgsDecode mentions return_bookkeeper"
        (code.Contains "return_bookkeeper")
    check "arch: ToolArgsDecode mentions apply_patch"
        (code.Contains "apply_patch")
    check "arch: ToolArgsDecode mentions submit_review"
        (code.Contains "submit_review")
    check "arch: ToolArgsDecode uses decodeTodoWriteArgs"
        (code.Contains "decodeTodoWriteArgs")
    check "arch: ToolArgsDecode uses decodeFetchEntity"
        (code.Contains "decodeFetchEntity")
    check "arch: ToolArgsDecode uses decodeReturnBookkeeperArgs"
        (code.Contains "decodeReturnBookkeeperArgs")
    check "arch: ToolArgsDecode uses decodeApplyPatchFields"
        (code.Contains "decodeApplyPatchFields")
    check "arch: ToolArgsDecode uses decodeSubmitReviewArgs"
        (code.Contains "decodeSubmitReviewArgs")

let decodedToolInvocationNoObj () =
    let code = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    check "arch: DecodedToolInvocation must not carry intents obj"
        (not (code.Contains "intents: obj"))
    check "arch: DecodedToolInvocation must not define SubagentIntents case"
        (not (code.Contains "SubagentIntents of"))

let sessionIoRunSubagentReturnsResult () =
    let sessionIo = requireFile "src/Opencode/SessionIo.fs" |> nonCommentCode
    check "arch: SessionIo runSubagent returns Promise Result"
        (sessionIo.Contains "let runSubagent" && sessionIo.Contains "JS.Promise<Result<string, DomainError>>")
    check "arch: SessionIo runSubagentWithCleanup returns Promise Result"
        (sessionIo.Contains "let runSubagentWithCleanup")
    check "arch: SessionIo must not define private runSubagentCore string wrapper"
        (not (sessionIo.Contains "let private runSubagentCore"))
    check "arch: runSubagentCoreResult outer catch returns Error not reject"
        (sessionIo.Contains "return Error (translateJsError err)")
    check "arch: runSubagentCoreResult inner catch returns Error domain"
        (sessionIo.Contains "return Error other")

let commandHooksUsesToolCopyReviewMessages () =
    let code = requireFile "src/Opencode/CommandHooks.fs" |> nonCommentCode
    let copy = requireFile "src/Kernel/ToolCopy.fs" |> nonCommentCode
    check "arch: CommandHooks opens ToolCopy"
        (code.Contains "ToolCopy")
    check "arch: CommandHooks uses reviewAlreadyActiveMessage"
        (code.Contains "reviewAlreadyActiveMessage")
    check "arch: ToolCopy defines preReviewCouldNotComplete"
        (copy.Contains "let preReviewCouldNotComplete")
    check "arch: CommandHooks must not inline With-Review Mode is already active"
        (not (code.Contains "With-Review Mode is already active. Submit your work via submit_review."))

let opencodeSubagentToolsUsesSimpleArgsCodec () =
    let code = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    let decode = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/SubagentSimpleArgsCodec.fs" |> nonCommentCode
    check "arch: SubagentSimpleArgsCodec defines decodeMeditatorArgs"
        (codec.Contains "let decodeMeditatorArgs")
    check "arch: SubagentSimpleArgsCodec defines decodeBrowserArgs"
        (codec.Contains "let decodeBrowserArgs")
    check "arch: ToolArgsDecode uses decodeMeditatorArgs"
        (decode.Contains "decodeMeditatorArgs")
    check "arch: ToolArgsDecode uses decodeBrowserArgs"
        (decode.Contains "decodeBrowserArgs")
    check "arch: Opencode SubagentTools must not open SubagentSimpleArgsCodec"
        (not (code.Contains "SubagentSimpleArgsCodec"))
    check "arch: Opencode SubagentTools must not decodeMeditatorArgs"
        (not (code.Contains "decodeMeditatorArgs"))
    check "arch: Opencode SubagentTools meditator must not Dyn.str args intent"
        (not (code.Contains "Dyn.str args \"intent\""))

let opencodeSubagentToolsUsesFromOpencode () =
    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    let shellExec = requireFile "src/Shell/SubagentToolExecute.fs" |> nonCommentCode
    check "arch: SubagentToolExecute opens ToolRuntimeContext"
        (shellExec.Contains "ToolRuntimeContext")
    check "arch: SubagentToolExecute uses fromOpencode"
        (shellExec.Contains "fromOpencode")
    check "arch: SubagentToolExecute uses runtime.Execution for directory and session"
        ((shellExec.Contains "runtime.Execution.Directory")
         && (shellExec.Contains "runtime.Execution.SessionId"))
    check "arch: Opencode SubagentTools must not decodeOpencodeToolContext"
        (not (opencode.Contains "decodeOpencodeToolContext"))
    check "arch: Opencode SubagentTools must not define private ToolExecutionContext"
        (not (opencode.Contains "type private ToolExecutionContext"))
    check "arch: SubagentToolExecute uses pluginDirectoryFromCtx"
        (shellExec.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode SubagentTools must not Dyn.str ctx directory"
        (not (opencode.Contains "Dyn.str ctx \"directory\""))