module Wanxiangshu.Tests.AsyncFlush

open Fable.Core

/// Yield one microtask turn (deterministic ordering), not wall-clock sleep.
[<Emit("new Promise(function(resolve){ queueMicrotask(resolve); })")>]
let yieldMicrotask () : JS.Promise<unit> = jsNative
