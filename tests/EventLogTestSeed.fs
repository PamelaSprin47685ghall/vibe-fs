module Wanxiangshu.Tests.EventLogTestSeed

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.EventLogRuntime

let seedLoopActivated (workspaceRoot: string) (sessionID: string) (task: string) : JS.Promise<unit> =
    appendLoopActivated workspaceRoot sessionID task |> Promise.map ignore
