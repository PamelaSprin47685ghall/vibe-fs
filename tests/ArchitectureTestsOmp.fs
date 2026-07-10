module Wanxiangshu.Tests.ArchitectureTestsOmp

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let ompMessageTransformUsesProjectionPolicy () =
    let code = requireFile "src/Omp/MessageTransform.fs" |> nonCommentCode

    check
        "arch: Omp MessageTransform uses shouldExcludeAgentFromProjection"
        (code.Contains "shouldExcludeAgentFromProjection")

    check "arch: Omp MessageTransform uses MessageTransformPipeline" (code.Contains "MessageTransformPipeline")

let ompMessageTransformUsesShellCaps () =
    let code = requireFile "src/Omp/MessageTransform.fs" |> nonCommentCode
    check "arch: Omp MessageTransform opens OmpCaps" (code.Contains "OmpCaps")
    check "arch: Omp MessageTransform uses MessageTransformCore" (code.Contains "MessageTransformCore")
    check "arch: Omp MessageTransform uses MessageTransformHostEntry" (code.Contains "MessageTransformHostEntry")

let ompMessageTransformUsesMessagingCodec () =
    let code = requireFile "src/Omp/MessageTransform.fs" |> nonCommentCode
    check "arch: Omp MessageTransform opens MessagingCodec" (code.Contains "MessagingCodec")
    check "arch: Omp MessageTransform no local buildCapsMessages" (not (code.Contains "let private buildCapsMessages"))

let ompMessagingCodecUsesShellPartCodec () =
    let code = requireFile "src/Omp/MessagingCodec.fs" |> nonCommentCode
    check "arch: Omp MessagingCodec defines textFromParts" (code.Contains "let private textFromParts")
    check "arch: Omp MessagingCodec defines entries" (code.Contains "let entries")

let ompCodecUsesDynModule () =
    let code = requireFile "src/Omp/Codec.fs" |> nonCommentCode
    check "arch: Omp Codec aliases Shell Dyn" (code.Contains "module Dyn = Wanxiangshu.Shell.Dyn")
    check "arch: Omp Codec defines getSessionIdFromContext" (code.Contains "let getSessionIdFromContext")

let ompHookExecuteUsesSubagentIntentsCodec () =
    let code = requireFile "src/Omp/HookExecute.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/SubagentIntentsCodec.fs" |> nonCommentCode
    check "arch: Omp HookExecute opens SubagentIntentsCodec" (code.Contains "SubagentIntentsCodec")
    check "arch: Omp HookExecute uses joinCoderUiLabel" (code.Contains "joinCoderUiLabel")
    check "arch: SubagentIntentsCodec defines joinCoderUiLabel" (codec.Contains "let joinCoderUiLabel")

let ompToolsRegisterAll () =
    let code = requireFile "src/Omp/Tools.fs" |> nonCommentCode
    check "arch: Omp Tools defines registerAllTools" (code.Contains "let registerAllTools")
    check "arch: Omp Tools registers fuzzy tools" (code.Contains "registerFuzzyTools")
    check "arch: Omp Tools registers methodology tools" (code.Contains "registerMeditatorTools")
    check "arch: Omp Tools registers executor tools" (code.Contains "registerExecutorTools")
    check "arch: Omp Tools registers subagent tools" (code.Contains "registerSubagentTools")

let ompPluginUsesPluginCore () =
    let plugin = requireFile "src/Omp/Plugin.fs" |> nonCommentCode
    let core = requireFile "src/Omp/PluginCore.fs" |> nonCommentCode
    check "arch: Omp Plugin opens PluginCore" (plugin.Contains "PluginCore")
    check "arch: Omp PluginCore defines CoreServices" (core.Contains "type CoreServices")
    check "arch: Omp PluginCore creates review store" (core.Contains "createReviewStore")

let ompPluginNoOpencodeMuxRefs () =
    for f in fsFiles "src/Omp" do
        let content = requireFile ("src/Omp/" + f)
        check ("arch: Omp/" + f + " no Wanxiangshu.Opencode") (not (content.Contains "Wanxiangshu.Opencode"))
        check ("arch: Omp/" + f + " no Wanxiangshu.Mux") (not (content.Contains "Wanxiangshu.Mux"))

let ompUsesOmpToolSchema () =
    let schema = requireFile "src/Omp/OmpToolSchema.fs" |> nonCommentCode
    check "arch: OmpToolSchema exists" (schema.Length > 0)
    let subagent = requireFile "src/Omp/SubagentTools.fs" |> nonCommentCode
    check "arch: Omp SubagentTools references OmpToolSchema" (subagent.Contains "OmpToolSchema")

let ompReviewUsesReviewRuntime () =
    let loop = requireFile "src/Omp/ReviewLoop.fs" |> nonCommentCode
    let tools = requireFile "src/Omp/ReviewToolsRegister.fs" |> nonCommentCode
    check "arch: Omp ReviewLoop uses ReviewSession" (loop.Contains "ReviewSession")
    check "arch: Omp ReviewTools uses ReviewRuntime" (tools.Contains "ReviewRuntime")

let ompCapsCodecExists () =
    let code = requireFile "src/Omp/CapsCodec.fs" |> nonCommentCode
    check "arch: Omp CapsCodec non-empty" (code.Contains "module")

let ompChildSessionExists () =
    let code = requireFile "src/Omp/ChildSession.fs" |> nonCommentCode
    check "arch: Omp ChildSession non-empty" (code.Contains "module")

let ompTestFilesUnder300 () =
    for f in fsFiles "tests" do
        if f.StartsWith "Omp" && f.EndsWith ".fs" then
            let path = "tests/" + f
            let content = requireFile path
            let lineCount = content.Length - content.Replace("\n", "").Length
            check ("arch: " + path + " <=300 lines") (lineCount <= 300)

let ompSourceFilesUnder300 () =
    for path in fsFilesRecursive "src/Omp" do
        let content = requireFile path
        let lineCount = content.Length - content.Replace("\n", "").Length
        let msg = "arch: " + path + " <=300 lines"
        check msg (lineCount <= 300)

let ompFuzzyToolsUsesShellFinder () =
    let code = requireFile "src/Omp/FuzzyTools.fs" |> nonCommentCode
    check "arch: Omp FuzzyTools uses FuzzyFinderShell" (code.Contains "FuzzyFinderShell")

let ompExecutorUsesShellExecute () =
    let code = requireFile "src/Omp/ExecutorTools.fs" |> nonCommentCode

    check
        "arch: Omp ExecutorTools uses ToolExecute or Shell executor path"
        (code.Contains "ToolExecute" || code.Contains "SessionExecutor")

let ompNudgeRuntimeModule () =
    let code = requireFile "src/Omp/NudgeRuntime.fs" |> nonCommentCode
    check "arch: Omp NudgeRuntime non-empty" (code.Contains "module")

let ompNudgeHooksDoNotReadReviewStoreForLoopState () =
    let code = requireFile "src/Omp/NudgeHooks.fs" |> nonCommentCode
    check "arch: Omp NudgeHooks must not read live review-state query" (not (code.Contains "isReviewActive"))

    check
        "arch: Omp NudgeHooks loop state from event log"
        (code.Contains "isLoopActiveFromEventLog"
         || code.Contains "getNudgeSnapshotFromEventLog")

let ompSessionLifecycleHooks () =
    let code = requireFile "src/Omp/SessionLifecycleHooks.fs" |> nonCommentCode
    check "arch: Omp SessionLifecycleHooks non-empty" (code.Contains "module")

let ompPiResolveNoEngine () =
    let code = requireFile "src/Omp/PiResolve.fs" |> nonCommentCode
    check "arch: Omp PiResolve no engine path" (not (code.Contains "engine/"))
