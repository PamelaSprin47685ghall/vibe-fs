module Wanxiangshu.Runtime.LivelockGuard

open Fable.Core
open Wanxiangshu.Runtime.RuntimeScope

[<Emit("($0 === undefined || $0 === null) ? '{}' : JSON.stringify($0, (key, value) => { if (key === 'ui_') return undefined; return value; })")>]
let cleanArgsJson (args: obj) : string = jsNative

type LivelockState =
    { tool: string
      argsJson: string
      outputJson: string
      count: int }

let defaultMaxRepeats = 3

let private tryGetState (scope: RuntimeScope) (sid: string) =
    match scope.TryFindKey "livelock_state" with
    | Some m -> Map.tryFind sid (unbox<Map<string, LivelockState>> m)
    | None -> None

let private putState (scope: RuntimeScope) (sid: string) (s: LivelockState option) =
    let m =
        match scope.TryFindKey "livelock_state" with
        | Some v -> unbox<Map<string, LivelockState>> v
        | None -> Map.empty

    let newM =
        match s with
        | Some x -> Map.add sid x m
        | None -> Map.remove sid m

    scope.Add("livelock_state", box newM)

let check (scope: RuntimeScope) (sessionId: string) (tool: string) (argsJson: string) (outputJson: string) : bool =
    let same (s: LivelockState) =
        s.tool = tool && s.argsJson = argsJson && s.outputJson = outputJson

    match tryGetState scope sessionId with
    | Some prev when same prev ->
        let next = { prev with count = prev.count + 1 }
        putState scope sessionId (Some next)
        next.count >= defaultMaxRepeats
    | _ ->
        putState
            scope
            sessionId
            (Some
                { tool = tool
                  argsJson = argsJson
                  outputJson = outputJson
                  count = 1 })

        false

let cleanup (scope: RuntimeScope) (sessionId: string) : unit = putState scope sessionId None
