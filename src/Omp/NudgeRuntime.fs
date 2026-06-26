module Wanxiangshu.Omp.NudgeRuntime

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Coordinator
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Omp.MessagingCodec

let mutable private coordinator = freshCoordinator

let clearNudgeSession (sessionId: string) : unit =
    coordinator <- { coordinator with sessions = Map.remove sessionId coordinator.sessions }

let private tryNudge (sessionId: string) (context: NudgeContext) : NudgeAction option =
    let next, action = update coordinator sessionId context
    coordinator <- next
    if action = NudgeNone then None else Some action

let tryLoopNudge (sessionId: string) (lastAssistantMessage: string) : bool =
    let ctx =
        { todos = []
          lastAssistantMessage = lastAssistantMessage
          hasActiveRunner = false
          isLoopActive = true }
    tryNudge sessionId ctx |> Option.map (fun a -> a = NudgeLoop) |> Option.defaultValue false

let tryTodoNudge (sessionId: string) (sessionManager: obj) (lastAssistantMessage: string) : bool =
    let openTodos = openTodoStatuses sessionManager
    if List.isEmpty openTodos then false
    else
        let ctx =
            { todos = openTodos
              lastAssistantMessage = lastAssistantMessage
              hasActiveRunner = false
              isLoopActive = false }
        tryNudge sessionId ctx |> Option.map (fun a -> a = NudgeTodo) |> Option.defaultValue false

let loopReminderContent () = loopNudgePrompt

let todoReminderContent () = todoNudgePrompt