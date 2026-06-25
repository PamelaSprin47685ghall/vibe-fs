module VibeFs.Omp.NudgeRuntime

open VibeFs.Kernel.Nudge

let mutable private coordinator = freshCoordinator

let clearNudgeSession (sessionId: string) : unit =
    coordinator <- { coordinator with sessions = Map.remove sessionId coordinator.sessions }

let tryRunnerNudge (sessionId: string) : bool =
    let ctx = { todos = []; lastAssistantMessage = ""; hasActiveRunner = true; isLoopActive = false }
    let next, action = update coordinator sessionId ctx
    coordinator <- next
    action = NudgeRunner

let tryLoopNudge (sessionId: string) (lastAssistantMessage: string) : bool =
    let ctx = { todos = []; lastAssistantMessage = lastAssistantMessage; hasActiveRunner = false; isLoopActive = true }
    let next, action = update coordinator sessionId ctx
    coordinator <- next
    action = NudgeLoop