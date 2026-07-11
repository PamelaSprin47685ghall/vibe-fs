module Wanxiangshu.Kernel.FallbackRuntimeLifecycle

open Wanxiangshu.Kernel.FallbackKernel.Types

[<RequireQualifiedAccess>]
type FallbackContinueMode =
    | Idle
    | Retrying

[<RequireQualifiedAccess>]
type FallbackTaskCompletion =
    | InProgress
    | Complete

let phaseForContinue (mode: FallbackContinueMode) : FallbackPhase =
    match mode with
    | FallbackContinueMode.Idle -> FallbackPhase.Idle
    | FallbackContinueMode.Retrying -> FallbackPhase.Retrying 1

let lifecycleForTask (completion: FallbackTaskCompletion) : FallbackLifecycle =
    match completion with
    | FallbackTaskCompletion.InProgress -> FallbackLifecycle.Active
    | FallbackTaskCompletion.Complete -> FallbackLifecycle.TaskComplete

let continueModeFromBool (value: bool) : FallbackContinueMode =
    if value then
        FallbackContinueMode.Retrying
    else
        FallbackContinueMode.Idle

let taskCompletionFromBool (value: bool) : FallbackTaskCompletion =
    if value then
        FallbackTaskCompletion.Complete
    else
        FallbackTaskCompletion.InProgress
