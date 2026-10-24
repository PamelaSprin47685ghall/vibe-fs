namespace Wanxiangshu.Context.Scope

open Fable.Core
open Fable.Core.JsInterop

/// Host-neutral scope owner binding; all state and decisions belong to wanxiangli.
module Surface =

    [<Literal>]
    let ResetScopeTool = "resetScope"

    [<Literal>]
    let SuffixSeparator = "\u0000\uFEFF"

    [<Import("PairHint", "wanxiangli/pair-hint")>]
    let private construct () : obj = jsNative

    let private owner = construct ()

    let toObjArray (messages: obj) : obj array =
        if isNull messages then [||]
        else emitJsExpr messages "Array.from($0)"

    let isUnchanged (first: obj array) (second: obj array) : bool = owner?isUnchanged(first, second)
    let budgetSuffix (budget: int) : string = owner?budgetSuffix(budget)
    let turnBudget (sessionId: string) : int = owner?turnBudget(sessionId)
    let isScopeInitialized (sessionId: string) : bool = owner?isInitialized(sessionId)
    let onTurnStart (sessionId: string) : unit = owner?startTurn(sessionId)
    let resetScopeState (sessionId: string) (budget: int) : unit = owner?resetSession(sessionId, budget)
    let clearSessionState (sessionId: string) : unit = owner?clearSession(sessionId)
    let planRequest (sessionId: string) (messages: obj) : obj = owner?planRequest(sessionId, messages)
    let bindDelegation (recipient: string) (delegator: string) : unit = owner?bindDelegation(recipient, delegator)
    let askNegotiation (recipient: string) (questions: string) : obj = owner?askNegotiation(recipient, questions)
    let prepareNegotiationReply (receipt: string) (answer: string) : obj = owner?prepareNegotiationReply(receipt, answer)
    let completeNegotiationReply (receipt: string) : bool = owner?completeNegotiationReply(receipt)
    let confirmNegotiationSteer (receipt: string) (recipient: string) : bool =
        owner?confirmNegotiationSteer(receipt, recipient)
    let negotiationRequired (recipient: string) : bool = owner?negotiationRequired(recipient)
    let planFilter (messages: obj) : obj array = owner?planFilter(messages)
    let appendTailOnce (content: obj) (suffix: string) : obj = owner?appendTailOnce(content, suffix)
    let writeUserHint (message: obj) (texts: string array) : obj = owner?writeUserHint(message, texts)
    let writeToolResultHint (message: obj) (texts: string array) : obj = owner?writeToolResultHint(message, texts)
    let stripKnownHints (message: obj) (texts: string array) : obj = owner?stripKnownHints(message, texts)
    let userCanCarryHint (message: obj) : bool = owner?userCanCarryHint(message)
    let toolCanCarryHint (message: obj) : bool = owner?toolCanCarryHint(message)
    let rememberSystem (sessionId: string) (system: string array) : unit =
        owner?rememberSystem(sessionId, system)

    let rememberMessages (sessionId: string) (messages: obj array) : unit =
        owner?rememberMessages(sessionId, messages)

    let rememberModel (sessionId: string) (model: obj) : unit =
        owner?rememberModel(sessionId, model)

    let modelFor (sessionId: string) : obj =
        owner?modelFor(sessionId)

    let startTemporary (tempId: string) (delegatorId: string) : obj =
        owner?startTemporary(tempId, delegatorId)

    let isTemporary (sessionId: string) : bool =
        owner?isTemporary(sessionId)

    let finishTemporary (tempId: string) : unit =
        owner?finishTemporary(tempId)

    let recordToolViolation (tempId: string) : unit =
        owner?recordToolViolation(tempId)

    let toolViolationCount (tempId: string) : int =
        owner?toolViolationCount(tempId)

    let forgetSession (sessionId: string) : unit =
        owner?forgetSession(sessionId)

    let projectSystem (tempId: string) (current: string array) : string array option =
        let r = owner?projectSystem(tempId, current)
        if isNull r then None else Some(unbox<string array> r)

    let projectMessages (tempId: string) (question: obj) : obj array option =
        let r = owner?projectMessages(tempId, question)
        if isNull r then None else Some(unbox<obj array> r)
