module VibeFs.Omp.NudgeRuntime

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.PromptFragments
open VibeFs.Omp.MessagingCodec

let mutable private coordinator = freshCoordinator

let clearNudgeSession (sessionId: string) : unit =
    coordinator <- { coordinator with sessions = Map.remove sessionId coordinator.sessions }

let private tryNudge (sessionId: string) (context: NudgeContext) : NudgeAction option =
    let next, action = update coordinator sessionId context
    coordinator <- next
    if action = NudgeNone then None else Some action

let tryRunnerNudge (sessionId: string) : bool =
    let ctx =
        { todos = []
          lastAssistantMessage = ""
          hasActiveRunner = true
          isLoopActive = false }
    tryNudge sessionId ctx |> Option.map (fun a -> a = NudgeRunner) |> Option.defaultValue false

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

let runnerReminderContent () = runnerNudgePromptFor omp

let loopReminderContent () = loopNudgePrompt

let todoReminderContent () = todoNudgePrompt