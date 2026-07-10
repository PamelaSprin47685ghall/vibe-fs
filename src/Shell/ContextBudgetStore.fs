module Wanxiangshu.Shell.ContextBudgetStore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.SerialStateHolder

type ContextBudgetEntry =
    { State: ContextState option
      LastUsage: {| tokenCount: int; textBytes: int |} option
      LastBacklog: Wanxiangshu.Kernel.BacklogProjectionCore.BacklogEntry list
      NudgeInjected: bool }

let private keyFor (sessionID: string) = "contextbudget_" + sessionID

let private defaultEntry : ContextBudgetEntry =
    { State = None
      LastUsage = None
      LastBacklog = []
      NudgeInjected = false }

let private getHolder (scope: RuntimeScope) (sessionID: string) : StateHolder<ContextBudgetEntry> =
    let key = keyFor sessionID
    match scope.TryFindKey(key) with
    | Some obj -> unbox<StateHolder<ContextBudgetEntry>> obj
    | None ->
        let holder = StateHolder(defaultEntry)
        scope.Add(key, box holder)
        holder

let get (scope: RuntimeScope) (sessionID: string) : ContextBudgetEntry =
    let holder = getHolder scope sessionID
    holder.Mutate(fun state -> state, state)

let put (scope: RuntimeScope) (sessionID: string) (entry: ContextBudgetEntry) : unit =
    let holder = getHolder scope sessionID
    holder.Mutate(fun _ -> entry, ())

let update (scope: RuntimeScope) (sessionID: string) (f: ContextBudgetEntry -> ContextBudgetEntry) : unit =
    let holder = getHolder scope sessionID
    holder.Mutate(fun state -> f state, ())
