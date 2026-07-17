module Wanxiangshu.Runtime.E2eSandbox

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools

[<Emit("$0 === undefined || $0 === null")>]
let private isNullish (v: obj) : bool = jsNative

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

/// Read process.env once and inject into pure Kernel HostTools.
let applyFromProcessEnv () : unit =
    let e = nodeProcess?("env")

    let value =
        if isNullish e then
            ""
        else
            let v = e?("WANXIANG_E2E_SANDBOX")
            if isNullish v then "" else string v

    setE2eSandbox (value = "1")
