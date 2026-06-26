module Wanxiangshu.Shell.JsArrayMutate

open Fable.Core.JsInterop

let replaceArrayInPlace (target: obj array) (source: obj array) : unit =
    if System.Object.ReferenceEquals(target, source) then ()
    else
        let targetObj = box target
        targetObj?length <- 0
        for item in source do
            targetObj?push(item) |> ignore