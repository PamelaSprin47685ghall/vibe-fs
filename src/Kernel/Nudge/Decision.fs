module Wanxiangshu.Kernel.Nudge.Decision

open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.Transitions

let private hasStoppedSession state sessionID = Set.contains sessionID state.stoppedSessions
let private getAgent state sessionID = Map.tryFind sessionID state.sessionAgents

let private selectNudgePrompt = function
    | NudgeTodo -> Some todoNudgePrompt
    | NudgeLoop -> Some loopNudgePrompt
    | _ -> None

let decideNudge isReviewActive lookupChildAgent state sessionID snapshot =
    if hasStoppedSession state sessionID then
        state, StandDown
    elif snapshot.alreadyNudged || snapshot.anchorPromptIssued then
        state, StandDown
    else
        let state = rememberAgent state sessionID snapshot.agentFromMessage
        let isWorkerLoopActive =
            match lookupChildAgent sessionID with
            | Some "reviewer" -> false
            | _ -> isReviewActive sessionID
        let context =
            { todos = snapshot.todos
              lastAssistantMessage = snapshot.lastAssistantMessage
              hasActiveRunner = false
              isLoopActive = isWorkerLoopActive }
        match decide context with
        | NudgeNone
        | NudgeRunner -> state, StandDown
        | action ->
            match selectNudgePrompt action with
            | None -> state, StandDown
            | Some promptText ->
                let agentOpt = getAgent state sessionID |> Option.orElse (lookupChildAgent sessionID)
                { state with lastNudgedSession = Some sessionID }, Send(promptText, agentOpt)
