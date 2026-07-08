module Wanxiangshu.Shell.CapsSynthCommon

open Wanxiangshu.Kernel.CapsPrelude
open Wanxiangshu.Kernel.CapsSynthPolicy

let hasExistingCapsMessages (messageId: obj -> string) (messages: obj array) : bool =
    if messages.Length = 0 then
        false
    else
        let id = messageId messages.[0]
        id <> "" && id.StartsWith capsUserPrefix

let stripLeadingCapsSynth (messageId: obj -> string) (messages: obj array) : obj array =
    if not (hasExistingCapsMessages messageId messages) then
        messages
    else
        messages |> Array.skipWhile (fun msg -> isCapsSynthId (messageId msg))

let findFirstNonSynthMessage (messageId: obj -> string) (messages: obj array) : obj option =
    messages
    |> Array.tryFind (fun msg ->
        let id = messageId msg

        id <> ""
        && not (id.StartsWith capsUserPrefix)
        && not (id.StartsWith capsAssistantPrefix)
        && not (id.StartsWith capsAcknowledgePrefix))

let userCapsText (preludeText: string option) : string =
    match preludeText with
    | Some prelude when prelude.Trim() <> "" -> prelude.Trim() + "\n\n" + thinkWrapped
    | _ -> thinkWrapped
