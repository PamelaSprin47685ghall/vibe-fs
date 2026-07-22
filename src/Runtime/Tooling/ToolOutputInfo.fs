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

let empty =
    { body = None
      hint = None
      syntax = None
      iterator = None
      status = None
      exitCode = None }

let private normalizedBody (s: string) = if System.String.IsNullOrWhiteSpace s then None else Some s

let withBody body =
    { empty with
        body = normalizedBody body }

let render (msg: ToolOutputMessage) : string = renderToolOutput msg

let noChangeEnvelope () =
    render { empty with status = Some noChangeStatus }

let addSyntax (raw: string) (syntax: string) : string =
    if System.String.IsNullOrWhiteSpace syntax then
        raw
    elif System.String.IsNullOrWhiteSpace raw then
        render { empty with syntax = Some syntax }
    else
        render
            { empty with
                body = Some raw
                syntax = Some syntax }

let withIterator (body: string) (iterator: string) : string =
    let iterOpt = if System.String.IsNullOrWhiteSpace iterator then None else Some iterator
    let bodyOpt = if System.String.IsNullOrWhiteSpace body then None else Some body

    if Option.isNone iterOpt then
        body
    else
        render
            { empty with
                body = bodyOpt
                iterator = iterOpt }

let todoWriteOutput (methodologies: string list) : string =
    let hintStr = hintForMethodologies methodologies
    render { empty with hint = Some hintStr }
