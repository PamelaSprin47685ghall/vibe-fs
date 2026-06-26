module Wanxiangshu.Omp.Plugin

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.PruneGuard
open Wanxiangshu.Omp.ReviewTools
open Wanxiangshu.Omp.PiResolve
open Wanxiangshu.Omp.KnowledgeGraph.Runtime
open Wanxiangshu.Omp.MessageTransform
open Wanxiangshu.Omp.OmpTestHooks
open Wanxiangshu.Omp.PluginCore
open Wanxiangshu.Omp.Tools

open Wanxiangshu.Shell.WebSearchApi
open Wanxiangshu.Shell.OmpCaps
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.SessionExecutor
open Wanxiangshu.Shell.TreeSitterShell

let private registered: obj = emitJsExpr () "new WeakSet()"

let private supportsSyntaxDiagnosticsTool (toolName: string) : JS.Promise<bool> =
    promise { return isFileEditTool toolName }

let resetOmpPluginTestState () : unit =
    clearCodingAgentModuleForTest ()
    resetReviewStates reviewStore
    resetRunnerJobsForTesting ()
    resetFuzzyState ()
    resetSessionExecutorForTesting ()
    resetOmpToolsTestState ()

/// Public test-visible `reviewStore` handle. Backed by the same singleton
/// `PluginCore.reviewStore` cell that the registered tools use, so tests
/// that pre-activate a review see it through the tool path.
let reviewStore : ReviewStore = reviewStore

[<ExportDefault>]
let wanxiangshuExtension (pi: obj) : JS.Promise<unit> =
    promise {
        if registered?has(pi) then
            ()
        else
            registered?add(pi) |> ignore
            do! pluginFor pi
    }

let _test =
    createObj [
        "appendCapsContext", box appendCapsContext
        "buildCapsContext", box buildCapsContextAsync
        "stripHostAgentsPrompt", box stripHostAgentsPrompt
        "checkSyntax", box checkSyntax
        "fuzzy", box (createFuzzyTestExports ())
        "getOllamaKey", box getOllamaApiKey
        "readAssistantText", box readAssistantText
        "resetRunner", box resetRunnerJobsForTesting
        "setRunnerJobStateForTest", box setRunnerJobStateForTest
        "setPendingReviewStateForTest",
            box(fun sessionId parentId pending ->
                setPendingReviewStateForTest reviewStore sessionId parentId pending)
        "stripHeadTailPipes", box strip
        "supportsSyntaxDiagnosticsTool", box supportsSyntaxDiagnosticsTool
        "reset", box resetOmpPluginTestState
        "transformEntries",
            box(fun (entries: obj array) (cwd: string) (sessionId: string) ->
                let kgRuntime = OmpKnowledgeGraphRuntime(createObj [])
                transformEntriesAsync reviewStore kgRuntime cwd sessionId (box entries))
    ]
