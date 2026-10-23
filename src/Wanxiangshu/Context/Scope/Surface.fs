namespace Wanxiangshu.Context.Scope

open System.Collections.Generic
open Wanxiangshu.Foundation.Identity

module Surface =

    let toObjArray (messages: obj) : obj array = Protocol.toObjArray messages
    let filterScopeMessages (messages: obj) : obj array = Protocol.filterScopeMessages messages
    let appendUserReminder (content: obj) : obj = Protocol.appendUserReminder content
    let appendBudgetReminder (content: obj) (budget: int) : obj = Protocol.appendBudgetReminder content budget
    let budgetSuffix (budget: int) : string = Protocol.budgetSuffix budget
    let addBudgetExhaustedSteer (messages: obj) : obj array = Protocol.addBudgetExhaustedSteer messages
    let stripContradictoryBudgetSuffixes (messages: obj) : obj array = Protocol.stripContradictoryBudgetSuffixes messages
    let isScopePrompt (message: obj) : bool = Protocol.isScopePrompt message
    let hasResetSinceLastPrompt (messages: obj) : bool = Protocol.hasResetSinceLastPrompt messages
    let lastPromptIndex (messages: obj) : int = Protocol.lastPromptIndex messages
    let promptKey (message: obj) : string = Protocol.promptKey message
    let endsWithSideRequest (messages: obj) : bool = Protocol.endsWithSideRequest messages
    let mapArrayIfChanged (arr: obj array) (mapper: obj -> obj) : obj array = Protocol.mapArrayIfChanged arr mapper
    let isUnchanged (arr: obj array) (other: obj array) : bool = Protocol.isUnchanged arr other
    let addUserReminders (messages: obj) : obj array = Protocol.addUserReminders messages

    let private sessionStore = Dictionary<string, ScopeSessionState>()

    let private defaultState =
        { TurnBudget = 0
          ScopeInitialized = false
          CurrentPromptKey = ""
          CurrentPromptTimestamp = None }

    let turnBudget (sessionId: string) : int =
        match sessionStore.TryGetValue(sessionId) with
        | true, state -> state.TurnBudget
        | false, _ -> 0

    let isScopeInitialized (sessionId: string) : bool =
        match sessionStore.TryGetValue(sessionId) with
        | true, state -> state.ScopeInitialized
        | false, _ -> false

    let onTurnStart (sessionId: string) : unit =
        match sessionStore.TryGetValue(sessionId) with
        | true, state ->
            sessionStore.[sessionId] <- { state with TurnBudget = max 0 (state.TurnBudget - 1) }
        | false, _ ->
            sessionStore.[sessionId] <- defaultState

    let onNewPrompt (sessionId: string) (promptKey: string) (timestamp: float option) (hasReset: bool) : unit =
        match sessionStore.TryGetValue(sessionId) with
        | true, state ->
            if promptKey = state.CurrentPromptKey then
                sessionStore.[sessionId] <- { state with ScopeInitialized = state.ScopeInitialized || hasReset }
            elif state.CurrentPromptTimestamp.IsNone || timestamp.IsNone || timestamp.Value >= state.CurrentPromptTimestamp.Value then
                sessionStore.[sessionId] <-
                    { TurnBudget = 0
                      ScopeInitialized = hasReset
                      CurrentPromptKey = promptKey
                      CurrentPromptTimestamp = timestamp }
        | false, _ ->
            sessionStore.[sessionId] <-
                { TurnBudget = 0
                  ScopeInitialized = hasReset
                  CurrentPromptKey = promptKey
                  CurrentPromptTimestamp = timestamp }

    let markScopeInitialized (sessionId: string) (initialized: bool) : unit =
        match sessionStore.TryGetValue(sessionId) with
        | true, state ->
            sessionStore.[sessionId] <- { state with ScopeInitialized = initialized }
        | false, _ ->
            sessionStore.[sessionId] <- { defaultState with ScopeInitialized = initialized }

    let resetScopeState (sessionId: string) (budget: int) : unit =
        match sessionStore.TryGetValue(sessionId) with
        | true, state ->
            sessionStore.[sessionId] <-
                { state with
                    TurnBudget = max 0 budget
                    ScopeInitialized = true }
        | false, _ ->
            sessionStore.[sessionId] <-
                { defaultState with
                    TurnBudget = max 0 budget
                    ScopeInitialized = true }

    let clearSessionState (sessionId: string) : unit =
        sessionStore.Remove(sessionId) |> ignore
