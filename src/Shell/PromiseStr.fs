module Wanxiangshu.Shell.PromiseStr

open Fable.Core

let resolveStr (s: string) : JS.Promise<string> = Promise.lift s
