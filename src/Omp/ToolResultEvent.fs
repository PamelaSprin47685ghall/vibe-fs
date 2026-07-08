module Wanxiangshu.Omp.ToolResultEvent

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

/// Coalesce `input` then `args` from the event object.
let getToolInput (event: obj) : obj =
    let input = Dyn.get event "input"

    if not (Dyn.isNullish input) then
        input
    else
        Dyn.get event "args"

/// Resolve the call identifier: `toolCallId` > `callId` > `callID`.
let getToolCallId (event: obj) : string =
    let v = Dyn.get event "toolCallId"

    if not (Dyn.isNullish v) then
        string v
    else
        let v2 = Dyn.get event "callId"

        if not (Dyn.isNullish v2) then
            string v2
        else
            let v3 = Dyn.get event "callID"
            if not (Dyn.isNullish v3) then string v3 else ""

/// Extract text from the result:
/// - `content` array → join text parts
/// - otherwise → `string content`
let getToolResultText (event: obj) : string =
    let content = Dyn.get event "content"

    if Dyn.isArray content then
        let arr = unbox<obj array> content

        arr
        |> Array.choose (function
            | :? string as s -> Some s
            | o -> Dyn.get o "text" |> fun v -> if Dyn.isNullish v then None else Some(string v))
        |> String.concat ""
    else
        string content

/// Overwrite the result content field with canonical TextContent[].
/// `content` = `[| { type: "text", text: … } |]`
let setToolResultText (event: obj) (text: string) : unit =
    let entry = createObj [ "type", box "text"; "text", box text ]
    event?content <- [| entry |]
