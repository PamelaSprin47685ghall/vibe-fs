module Wanxiangshu.Omp.NudgeRuntime

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.PromptFragments

let mutable private fallbackRuntimeInstance: Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState option =
    None

let setFallbackRuntime (rt: Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState) : unit =
    fallbackRuntimeInstance <- Some rt

let private getFallbackRuntime () : Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState option =
    fallbackRuntimeInstance

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
