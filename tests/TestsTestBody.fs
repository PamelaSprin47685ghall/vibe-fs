module Wanxiangshu.Tests.TestsTestBody

open Fable.Core

type TestBody =
    | Sync of (unit -> unit)
    | Async of (unit -> JS.Promise<unit>)

let sync (f: unit -> 'a) : unit -> unit = fun () -> ignore (f ())
