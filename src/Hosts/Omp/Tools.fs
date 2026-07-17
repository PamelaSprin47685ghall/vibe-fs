module Wanxiangshu.Hosts.Omp.Tools

open Wanxiangshu.Hosts.Omp.FuzzyTools
open Wanxiangshu.Hosts.Omp.MessageTransform
open Wanxiangshu.Hosts.Omp.ReviewToolsRegister
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Hosts.Omp.SubagentTools
open Wanxiangshu.Hosts.Omp.TodoTool
open Wanxiangshu.Hosts.Omp.WebTools
open Wanxiangshu.Hosts.Omp.OmpTools
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Kernel.FallbackKernel.Types

let registerAllTools
    (pi: obj)
    (reviewStore: ReviewStore)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    : unit =
    let finderCache = FinderCache()
    let iteratorStore = ompScope.IteratorStore
    registerFuzzyTools pi finderCache iteratorStore
    registerWebTools pi fallbackRuntime fallbackConfigOpt
    registerExecutorTools pi
    registerSubagentTools pi fallbackRuntime fallbackConfigOpt
    registerTodoTool pi
    registerMeditatorTools pi fallbackRuntime fallbackConfigOpt
    registerLoopFeatures pi reviewStore
    registerContextTransform pi reviewStore
