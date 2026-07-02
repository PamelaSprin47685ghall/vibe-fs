module Wanxiangshu.Omp.NudgeRuntime

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.PromptFragments

let mutable private forceStoppedSessions: Set<string> = Set.empty

let markSessionForceStopped (sessionId: string) : unit =
    forceStoppedSessions <- Set.add sessionId forceStoppedSessions

let clearNudgeSession (sessionId: string) : unit =
    forceStoppedSessions <- Set.remove sessionId forceStoppedSessions

let isSessionForceStopped (sessionId: string) : bool =
    Set.contains sessionId forceStoppedSessions

let todoReminderContent () = todoNudgePrompt
let loopReminderContent () = loopNudgePrompt
let runnerReminderContent () = runnerNudgePromptFor omp
