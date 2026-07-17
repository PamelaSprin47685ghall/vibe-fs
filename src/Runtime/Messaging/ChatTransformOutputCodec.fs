module Wanxiangshu.Runtime.ChatTransformOutputCodec

open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

let tryGetMessagesArrayFromOutput (output: obj) : obj array option =
    let messages = Dyn.get output "messages"

    if Dyn.isNullish messages || not (Dyn.isArray messages) then
        None
    else
        let arr = messages :?> obj array
        if arr.Length = 0 then None else Some arr

let clearSystemOutputLength (output: obj) : unit =
    let systemObj = Dyn.get output "system"

    if not (Dyn.isNullish systemObj) then
        systemObj?length <- 0

let setSystemOutputToDirectory (directory: string) (output: obj) : unit =
    if directory <> "" then
        let sys = output?system

        if not (Dyn.isNullish sys) && Dyn.isArray sys then
            sys?``length`` <- 0
            sys?push (box directory) |> ignore
        else
            output?system <- [| box directory |]
