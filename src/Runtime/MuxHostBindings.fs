module Wanxiangshu.Runtime.MuxHostBindings

open Fable.Core
open Wanxiangshu.Runtime.Dyn

/// Centralized Mux host tool.execute access (replaces inline `tool?execute` in Wrappers).
let getToolExecute (tool: obj) : obj = Dyn.get tool "execute"

let invokeToolExecute (tool: obj) (args: obj) (opts: obj) : obj =
    Dyn.call2 (getToolExecute tool) args opts
