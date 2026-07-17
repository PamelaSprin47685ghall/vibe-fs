module Wanxiangshu.Runtime.SubagentSpawn

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Runtime.Dyn

[<Global>]
type AbortController() =
    member _.signal: obj = jsNative
    member _.abort() : unit = jsNative

let private abortableConfig (config: obj) (signal: obj) = Dyn.withKey config "abortSignal" signal

let runParallelSpawns (prompts: string list) (spawnOne: string -> JS.Promise<string>) : JS.Promise<string> =
    promise {
        let! reports = prompts |> List.map spawnOne |> List.toArray |> Promise.all
        return joinReports reports
    }

let runParallelSpawnsWithAbort
    (prompts: string array)
    (spawn: string -> obj -> JS.Promise<string>)
    (config: obj)
    : JS.Promise<string> =
    promise {
        let controller = AbortController()

        let! reports =
            prompts
            |> Array.map (fun prompt ->
                promise {
                    try
                        let! r = spawn prompt (abortableConfig config controller.signal)
                        return Some r
                    with _ ->
                        controller.abort ()
                        return None
                })
            |> Promise.all

        return joinReports (reports |> Array.choose id |> Array.toList)
    }
