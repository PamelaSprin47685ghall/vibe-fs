module Wanxiangshu.Runtime.Fallback.ContinuationSessionReenter

open Fable.Core
open Wanxiangshu.Runtime.PromiseQueue

/// Serialize state mutations on the session actor queue. Physical transport stays outside.
type SessionReenter = (unit -> JS.Promise<unit>) -> JS.Promise<unit>

let inlineReenter (work: unit -> JS.Promise<unit>) : JS.Promise<unit> = work ()

let queueReenter (queue: SerialQueue) (work: unit -> JS.Promise<unit>) : JS.Promise<unit> =
    queue.Enqueue(work)
