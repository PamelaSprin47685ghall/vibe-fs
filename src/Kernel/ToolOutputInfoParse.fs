module VibeFs.Kernel.ToolOutputInfoParse

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.ToolOutputInfoTypes

[<Emit("Object.keys($0)")>]
let private objectKeys (o: obj) : string array = jsNative

[<Emit("Array.isArray($0)")>]
let private isArray (o: obj) : bool = jsNative

let private parseInfoItem (item: obj) : InfoItem option =
    if isNull item then None
    else
        let keys = objectKeys item
        if keys.Length = 0 then None
        else
            let key = keys.[0]
            let value = item?(key)
            let strValue = if isNull value then "" else string value
            match key with
            | "hint" -> Some (InfoItem.Hint strValue)
            | "syntax" -> Some (InfoItem.Syntax strValue)
            | "iterator" -> Some (InfoItem.Iterator strValue)
            | "status" -> Some (InfoItem.Status strValue)
            | "exit_code" ->
                match System.Int32.TryParse strValue with
                | true, n -> Some (InfoItem.ExitCode n)
                | false, _ -> None
            | "signal" -> Some (InfoItem.Signal strValue)
            | "timeout_ms" ->
                match System.Int32.TryParse strValue with
                | true, n -> Some (InfoItem.TimeoutMs n)
                | false, _ -> None
            | "tool_output" ->
                let ref' =
                    if strValue = seeBelow then ToolOutputBodyRef.SeeBelow
                    elif strValue = seeBelowTruncated then ToolOutputBodyRef.SeeBelowTruncated
                    elif strValue = noChangeSincePreviousReadWrite then ToolOutputBodyRef.NoChangeSincePreviousReadWrite
                    else ToolOutputBodyRef.SeeBelow
                Some (InfoItem.BodyRef ref')
            | _ -> None

let parseInfoItems (parsed: obj) : InfoItem list =
    if isNull parsed then []
    else
        let info = parsed?("info")
        if isNull info || not (isArray info) then []
        else (unbox<obj array> info) |> Array.choose parseInfoItem |> Array.toList
