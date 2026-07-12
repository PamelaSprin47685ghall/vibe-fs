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
      NudgeTrack: BudgetNudgeTrack
      ResolvedLimit: {| limit: int; source: string |} option
      EpisodeID: string
      NudgeCount: int
      SignalTodoOrdinal: int option
      SignalTokens: int64 option
      StableSyntheticNudgeID: string option }

let private keyFor (sessionID: string) = "contextbudget_" + sessionID

let private defaultEntry: ContextBudgetEntry =
    { State = None
      LastUsage = None
      LastBacklog = []
      NudgeTrack = Idle
      ResolvedLimit = None
      EpisodeID = ""
      NudgeCount = 0
      SignalTodoOrdinal = None
      SignalTokens = None
      StableSyntheticNudgeID = None }

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
