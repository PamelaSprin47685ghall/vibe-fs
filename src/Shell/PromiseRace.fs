module VibeFs.Shell.PromiseRace

open Fable.Core
open Fable.Core.JsInterop

/// Native `Promise.race` lifted into F#. Lives once in Shell so the Mux and
/// Opencode host layers share a single definition instead of each re-importing
/// the Promise constructor (REFACTOR.md §7).
[<Global("Promise")>]
let private promiseCtor : obj = jsNative

let promiseRace<'T> (promises: JS.Promise<'T> array) : JS.Promise<'T> =
    unbox<JS.Promise<'T>> (promiseCtor?race(promises))
