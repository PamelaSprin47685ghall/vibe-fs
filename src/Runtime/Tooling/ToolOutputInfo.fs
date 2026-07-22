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

let empty : ToolOutputMessage =
    { content = Empty
      hint = None
      syntax = None
      iterator = None
      status = None }

let plainText (text: string) : ToolOutputMessage =
    if System.String.IsNullOrWhiteSpace text then
        empty
    else
        { empty with content = Plain text }

let render (msg: ToolOutputMessage) : string = renderToolOutput msg

let noChangeEnvelope () =
    render { empty with status = Some noChangeStatus }

let addSyntax (msg: ToolOutputMessage) (syntax: string) : ToolOutputMessage =
    if System.String.IsNullOrWhiteSpace syntax then
        msg
    else
        { msg with syntax = Some syntax }

let withIterator (msg: ToolOutputMessage) (iterator: string) : ToolOutputMessage =
    if System.String.IsNullOrWhiteSpace iterator then
        msg
    else
        { msg with iterator = Some iterator }

let todoWriteOutput (methodologies: string list) : string =
    let hintStr = hintForMethodologies methodologies
    render { empty with hint = Some hintStr }
