module Wanxiangshu.Omp.NudgeRuntime

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.PromptFragments

let clearNudgeSession (_sessionId: string) : unit = ()

let todoReminderContent () = todoNudgePrompt
let loopReminderContent () = loopNudgePrompt
let runnerReminderContent () = runnerNudgePromptFor omp
