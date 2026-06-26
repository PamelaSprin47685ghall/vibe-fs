module Wanxiangshu.Omp.Tools

open Wanxiangshu.Omp.FuzzyTools
open Wanxiangshu.Omp.KnowledgeGraph.Runtime
open Wanxiangshu.Omp.KnowledgeGraphTools
open Wanxiangshu.Omp.MessageTransform
open Wanxiangshu.Omp.ReviewTools
open Wanxiangshu.Omp.ExecutorTools
open Wanxiangshu.Omp.SubagentTools
open Wanxiangshu.Omp.TodoTool
open Wanxiangshu.Omp.WebTools
open Wanxiangshu.Methodology.OmpTools
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.KnowledgeGraphFiles
open Wanxiangshu.Shell.ReviewRuntime

let registerAllTools (pi: obj) (reviewStore: ReviewStore) (kgRuntime: OmpKnowledgeGraphRuntime) : unit =
    let finderCache = FinderCache()
    registerFuzzyTools pi finderCache
    registerWebTools pi
    registerExecutorTools pi
    registerSubagentTools pi
    registerTodoTool pi
    registerMethodologyTools pi
    registerLoopFeatures pi reviewStore
    registerContextTransform pi reviewStore kgRuntime
    ensureKnowledgeGraphTools pi kgRuntime (Wanxiangshu.Shell.Dyn.str pi "cwd")

let resetOmpToolsTestState () = resetOmpKgToolsTestState ()