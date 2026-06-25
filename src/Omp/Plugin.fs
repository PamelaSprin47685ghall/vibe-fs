module VibeFs.Omp.Plugin

open Fable.Core.JsInterop
open Fable.Core
open VibeFs.Kernel.Executor
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Omp.MessageTransform
open VibeFs.Omp.MessagingCodec
open VibeFs.Omp.PruneGuard
open VibeFs.Omp.ReviewTools
open VibeFs.Omp.SessionLifecycle
open VibeFs.Omp.OmpTestHooks
open VibeFs.Omp.PiResolve
open VibeFs.Omp.Tools

open VibeFs.Shell.OllamaClient
open VibeFs.Shell.OmpCaps
open VibeFs.Shell.ReviewRuntime
open VibeFs.Shell.RunnerBackground
open VibeFs.Shell.SessionExecutor
open VibeFs.Shell.TreeSitterShell

let private registered: obj = emitJsExpr () "new WeakSet()"

let reviewStore = createReviewStore ()

[<ExportDefault>]
let kunweiExtension (pi: obj) : JS.Promise<unit> =
    if registered?has(pi) then
        emitJsExpr () "Promise.resolve()" |> unbox<JS.Promise<unit>>
    else
        promise {
            registered?add(pi) |> ignore
            registerAllTools pi reviewStore
            registerInputHandler pi reviewStore
            registerSessionLifecycle pi reviewStore
            do! patchDisablePrune ()
        }

let private supportsSyntaxDiagnosticsTool (toolName: string) : JS.Promise<bool> =
    promise { return isFileEditTool toolName }

let resetOmpPluginTestState () : unit =
    clearCodingAgentModuleForTest ()
    resetReviewStates reviewStore
    resetRunnerJobsForTesting ()
    resetFuzzyState ()
    resetSessionExecutorForTesting ()

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
    ]