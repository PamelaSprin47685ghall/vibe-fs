module VibeFs.Omp.Tools

open VibeFs.Omp.FuzzyTools
open VibeFs.Omp.KnowledgeGraphRuntime
open VibeFs.Omp.KnowledgeGraphTools
open VibeFs.Omp.MessageTransform
open VibeFs.Omp.ReviewTools
open VibeFs.Omp.ExecutorTools
open VibeFs.Omp.SubagentTools
open VibeFs.Omp.TodoTool
open VibeFs.Omp.WebTools
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.ReviewRuntime

let registerAllTools (pi: obj) (reviewStore: ReviewStore) (kgRuntime: OmpKnowledgeGraphRuntime) : unit =
    let finderCache = FinderCache()
    registerFuzzyTools pi finderCache
    registerWebTools pi
    registerExecutorTools pi
    registerSubagentTools pi
    registerTodoTool pi
    registerLoopFeatures pi reviewStore
    registerContextTransform pi reviewStore kgRuntime
    ensureKnowledgeGraphTools pi kgRuntime (VibeFs.Shell.Dyn.str pi "cwd")

let resetOmpToolsTestState () = resetOmpKgToolsTestState ()