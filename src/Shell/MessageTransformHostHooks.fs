module Wanxiangshu.Shell.MessageTransformHostHooks

open Fable.Core
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Shell.CapsFileCache
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.RuntimeScope

type CapsLoadPolicy =
    | RequireDirectory
    | AllowEmptyDirectory

let loadCapsForScope
    (scope: RuntimeScope)
    (policy: CapsLoadPolicy)
    (plan: MessageTransformPlan)
    : JS.Promise<CapsFile list> =
    if plan.Excluded then Promise.lift []
    else
        match policy with
        | RequireDirectory when plan.Directory = "" -> Promise.lift []
        | RequireDirectory | AllowEmptyDirectory ->
            getOrLoadCapsFilesForScope scope plan.SessionID plan.Directory

let loadKgPreludeForAgent
    (requireDirectory: bool)
    (agent: string)
    (plan: MessageTransformPlan)
    (buildFn: string -> string -> JS.Promise<string option>)
    : JS.Promise<string option> =
    if plan.Excluded then Promise.lift None
    elif requireDirectory && plan.Directory = "" then Promise.lift None
    elif not (canUse agent "knowledge_graph_fetch") then Promise.lift None
    else buildFn plan.SessionID plan.Directory