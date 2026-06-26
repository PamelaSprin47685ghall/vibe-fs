// msg.parts text / read-dedup projection; full Part decode → MessagingPartCodec.
module Wanxiangshu.Shell.HostMessagePartCodec

open Wanxiangshu.Shell.Dyn

let private tryReadContentFromOutput (output: obj) : string option =
    if Dyn.isNullish output then None
    elif Dyn.typeIs output "string" then
        let s = string output
        if s = "" then None else Some s
    else
        let s = Dyn.str output "content"
        if s = "" then None else Some s

let getMessageParts (msg: obj) : obj array =
    if Dyn.isNullish msg then [||]
    else
        let parts = Dyn.get msg "parts"
        if Dyn.isNullish parts || not (Dyn.isArray parts) then [||]
        else parts :?> obj array

let decodeDynamicToolReadOutput (part: obj) : string option =
    if Dyn.str part "type" <> "dynamic-tool" then None
    else tryReadContentFromOutput (Dyn.get part "output")

let extractTextLinesFromParts (parts: obj array) : string seq =
    parts
    |> Seq.choose (fun part ->
        if Dyn.str part "type" = "text" then
            let text = Dyn.str part "text"
            if text <> "" then Some text else None
        else
            decodeDynamicToolReadOutput part)