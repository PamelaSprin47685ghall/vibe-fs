module Wanxiangshu.Omp.Tools

open Wanxiangshu.Omp.FuzzyTools
open Wanxiangshu.Omp.MessageTransform
open Wanxiangshu.Omp.ReviewTools
open Wanxiangshu.Omp.ExecutorTools
open Wanxiangshu.Omp.SubagentTools
open Wanxiangshu.Omp.TodoTool
open Wanxiangshu.Omp.WebTools
open Wanxiangshu.Methodology.OmpTools
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Kernel.FallbackKernel.Types

let registerAllTools (pi: obj) (reviewStore: ReviewStore) (fallbackRuntime: FallbackRuntimeState) (fallbackConfigOpt: FallbackConfig option) : unit =
    let finderCache = FinderCache()
    registerFuzzyTools pi finderCache
    registerWebTools pi fallbackRuntime fallbackConfigOpt
    registerExecutorTools pi
    registerSubagentTools pi fallbackRuntime fallbackConfigOpt
    registerTodoTool pi
    registerMethodologyTools pi fallbackRuntime fallbackConfigOpt
    registerLoopFeatures pi reviewStore
    registerContextTransform pi reviewStore