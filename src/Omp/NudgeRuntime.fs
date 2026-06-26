module Wanxiangshu.Omp.NudgeRuntime

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Coordinator
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Omp.MessagingCodec

let mutable private coordinator = freshCoordinator

let clearNudgeSession (sessionId: string) : unit =
    coordinator <- { coordinator with sessions = Map.remove sessionId coordinator.sessions }

let private tryNudge (sessionId: string) (context: NudgeContext) : NudgeAction option =
    let next, action = update coordinator sessionId context
    coordinator <- next
    if action = NudgeNone then None else Some action

let tryLoopNudge (sessionId: string) (lastAssistantMessage: string) : NudgeAction option =
    let ctx =
        { todos = []
          lastAssistantMessage = lastAssistantMessage
          hasActiveRunner = hasRunningRunnerJob sessionId
          isLoopActive = true }
    tryNudge sessionId ctx

let tryTodoNudge (sessionId: string) (sessionManager: obj) (lastAssistantMessage: string) : NudgeAction option =
    let openTodos = openTodoStatuses sessionManager
    let ctx =
        { todos = openTodos
          lastAssistantMessage = lastAssistantMessage
          hasActiveRunner = hasRunningRunnerJob sessionId
          isLoopActive = false }
    tryNudge sessionId ctx

let loopReminderContent () = loopNudgePrompt
let todoReminderContent () = todoNudgePrompt
let runnerReminderContent () = runnerNudgePromptFor omp