module Wanxiangshu.Kernel.ToolOutputInfoParse

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.ToolOutputInfoTypes

[<Global("Object")>]
let private JSObject: obj = jsNative

let private objectKeys (o: obj) : string array = JSObject?keys(o) |> unbox

let private isArray (o: obj) : bool = not (isNull o) && (o :? obj array)

let private parseInfoItem (key: string) (value: obj) : InfoItem option =
    if isNull value then
        None
    else
        let strValue = string value

        match key with
        | "hint" -> Some(InfoItem.Hint strValue)
        | "syntax" -> Some(InfoItem.Syntax strValue)
        | "iterator" -> Some(InfoItem.Iterator strValue)
        | "status" -> Some(InfoItem.Status strValue)
        | "exit_code" ->
            match System.Int32.TryParse strValue with
            | true, n -> Some(InfoItem.ExitCode n)
            | false, _ -> None
        | _ -> None

let parseInfoItems (parsed: obj) : InfoItem list =
    if isNull parsed then
        []
    else
        objectKeys parsed
        |> Array.collect (fun key ->
            let value = parsed?(key)

            if isArray value then
                (unbox<obj array> value) |> Array.map (fun element -> key, element)
            else
                [| key, value |])
        |> Array.choose (fun (key, value) -> parseInfoItem key value)
        |> Array.toList
