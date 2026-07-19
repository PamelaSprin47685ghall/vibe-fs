module Wanxiangshu.Tests.PluginObjectContractTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert

let private objectKeys (o: obj) : string array =
    Fable.Core.JS.Constructors.Object.keys (o) |> unbox

let private startsWithDoubleUnderscore (key: string) : bool = key.StartsWith("__")

let private containsKey (keys: string array) (target: string) : bool =
    keys |> Array.exists (fun k -> k = target)

let run () : unit =
    let deps = createObj []
    let registration = Wanxiangshu.Hosts.Mux.PluginRegistration.createRegistration deps
    let keys = objectKeys registration

    let forbiddenKeys = keys |> Array.filter startsWithDoubleUnderscore

    check
        (sprintf
            "Mux registration must not expose __-prefixed test backdoor keys, found: %A"
            (Array.toList forbiddenKeys))
        (forbiddenKeys.Length = 0)

    check
        "Mux registration must expose public runtime hook 'tool.execute.before'"
        (containsKey keys "tool.execute.before")

    check "Mux registration must expose public runtime hook 'systemTransform'" (containsKey keys "systemTransform")
