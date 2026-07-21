module Wanxiangshu.Runtime.Yaml

open Fable.Core
open Fable.Core.JsInterop

[<ImportAll("yaml")>]
let private yamlLib: obj = jsNative

let private stringifyOptions = createObj [ "lineWidth", box 0 ]

let stringify (value: obj) : string =
    yamlLib?stringify (value, stringifyOptions)
