module Wanxiangshu.Runtime.ToolOutputInfo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.Tooling.ToolOutputToml

let hintExecutorMisuse =
    "No executor for reading, searching, writing files. Use read/inspector/coder!"

let hintTodosUpdated = "Todos updated."

let hintMethodologyFollowup (methodologyId: string) =
    $"Great! Please apply {methodologyId} to the subsequent steps. If all work has been fully completed and this is purely a summary/handover, you do NOT need to call the methodology tool. Otherwise, for any remaining tasks, the more difficult/complex the problem is, the more critical it is to think over and call the methodology tool with {methodologyId} selected to guide your next work step."

let hintForMethodologies (methodologies: string list) : string =
    match methodologies with
    | [] -> hintTodosUpdated
    | names -> names |> List.map hintMethodologyFollowup |> String.concat " "

let empty = { info = []; body = "" }

let private normalizedBody (s: string) = if isNull s then "" else s

let withBody body =
    { empty with
        body = normalizedBody body }

let appendInfo item (msg: ToolOutputMessage) : ToolOutputMessage = { msg with info = item :: msg.info }

let render (msg: ToolOutputMessage) : string = renderToolOutput msg

let noChangeEnvelope () =
    render
        { info = [ InfoItem.Status noChangeStatus ]
          body = "" }

let addSyntax (raw: string) (syntax: string) : string =
    if System.String.IsNullOrWhiteSpace syntax then
        raw
    elif System.String.IsNullOrWhiteSpace raw then
        render
            { empty with
                info = [ InfoItem.Syntax syntax ]
                body = "" }
    else
        render
            { empty with
                info = [ InfoItem.Syntax syntax ]
                body = raw }

let withIterator (body: string) (iterator: string) : string =
    if iterator = "" then
        body
    else
        render
            { empty with
                info = [ InfoItem.Iterator iterator ]
                body = body }

let todoWriteOutput (methodologies: string list) : string =
    let hints = [ InfoItem.Hint(hintForMethodologies methodologies) ]
    render { empty with info = hints; body = "" }
