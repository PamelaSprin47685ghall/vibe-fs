module Wanxiangshu.Hosts.Omp.NudgeRuntime

open Wanxiangshu.Runtime.Fallback.RuntimeStore

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeCompactionPure

let markSessionForceStopped (fallbackRuntime: FallbackRuntimeStore) (sessionId: string) : unit =
    fallbackRuntime.UpdateSession(sessionId, markForceStopped)

let clearNudgeSession (fallbackRuntime: FallbackRuntimeStore) (sessionId: string) : unit =
    fallbackRuntime.UpdateSession(sessionId, removeForceStopped)

let isSessionForceStopped (fallbackRuntime: FallbackRuntimeStore) (sessionId: string) : bool =
    (fallbackRuntime.GetSession sessionId).CompactionForceStopped

let todoReminderContent (todos: string list) = todoNudgePromptFor todos
let loopReminderContent (todos: string list) = loopNudgePromptFor todos
let runnerReminderContent () = runnerNudgePromptFor omp
