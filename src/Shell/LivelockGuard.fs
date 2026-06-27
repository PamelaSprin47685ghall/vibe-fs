module Wanxiangshu.Shell.LivelockGuard

type LivelockState = { tool: string; argsJson: string; outputJson: string; count: int }
let defaultMaxRepeats = 3

let mutable private stateBySession = Map.empty<string, LivelockState>

let check (sessionId: string) (tool: string) (argsJson: string) (outputJson: string) : bool =
    let same (s: LivelockState) =
        s.tool = tool && s.argsJson = argsJson && s.outputJson = outputJson
    match Map.tryFind sessionId stateBySession with
    | Some prev when same prev ->
        let next = { prev with count = prev.count + 1 }
        stateBySession <- Map.add sessionId next stateBySession
        next.count >= defaultMaxRepeats
    | _ ->
        stateBySession <- Map.add sessionId { tool = tool; argsJson = argsJson; outputJson = outputJson; count = 1 } stateBySession
        false
