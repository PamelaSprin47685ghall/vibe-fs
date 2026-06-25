module VibeFs.Omp.Tools

open VibeFs.Omp.FuzzyTools
open VibeFs.Omp.ReviewTools
open VibeFs.Omp.ExecutorTools
open VibeFs.Omp.SubagentTools
open VibeFs.Omp.TodoTool
open VibeFs.Omp.WebTools
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.ReviewRuntime

let registerAllTools (pi: obj) (reviewStore: ReviewStore) : unit =
    let finderCache = FinderCache()
    registerFuzzyTools pi finderCache
    registerWebTools pi
    registerExecutorTools pi
    registerSubagentTools pi
    registerTodoTool pi
    registerLoopFeatures pi reviewStore