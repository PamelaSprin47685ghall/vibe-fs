module Wanxiangshu.Hosts.Omp.NudgeRuntime

open Wanxiangshu.Runtime.Fallback.RuntimeStore

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Runtime.Fallback.CompactionTransitions
open Wanxiangshu.Runtime.Fallback.ModelInjection

let mutable private fallbackRuntimeInstance: FallbackRuntimeStore option = None

let setFallbackRuntime (rt: FallbackRuntimeStore) : unit = fallbackRuntimeInstance <- Some rt

let private getFallbackRuntime () : FallbackRuntimeStore option = fallbackRuntimeInstance

let markSessionForceStopped (sessionId: string) : unit =
    match getFallbackRuntime () with
    | Some rt -> rt.MarkForceStopped sessionId
    | None -> ()

let clearNudgeSession (sessionId: string) : unit =
    match getFallbackRuntime () with
    | Some rt -> rt.RemoveForceStopped sessionId
    | None -> ()

let isSessionForceStopped (sessionId: string) : bool =
    match getFallbackRuntime () with
    | Some rt -> rt.IsForceStopped sessionId
    | None -> false

let todoReminderContent (todos: string list) = todoNudgePromptFor todos
let loopReminderContent (todos: string list) = loopNudgePromptFor todos
let runnerReminderContent () = runnerNudgePromptFor omp
