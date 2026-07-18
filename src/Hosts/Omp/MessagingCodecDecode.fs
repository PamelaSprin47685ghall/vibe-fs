module Wanxiangshu.Hosts.Omp.MessagingCodecDecode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.MessagingDecode
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Hosts.Omp.Codec

module Dyn = Wanxiangshu.Runtime.Dyn

let private toObjArray (value: obj) : obj array =
    if Dyn.isNullish value || not (Dyn.isArray value) then
        [||]
    else
        unbox<obj array> value

let private textFromParts (parts: obj) : string =
    if not (Dyn.isArray parts) then
        ""
    else
        toObjArray parts
        |> Array.choose (fun part ->
            if Dyn.str part "type" = "text" then
                let t = Dyn.get part "text"
                if Dyn.isNullish t then None else Some(string t)
            else
                None)
        |> String.concat "\n\n"

let decodeToolState (state: obj) : ToolState<obj> option =
    if Dyn.isNullish state then
        None
    else
        let input = Dyn.get state "input"

        Some
            { status = fromString (Dyn.str state "status")
              output = Dyn.str state "output"
              error = Dyn.str state "error"
              input = input
              operationAction = "" }

let private roleOfEntry (entry: obj) : string =
    let m = Dyn.get entry "message"

    if not (Dyn.isNullish m) then
        Dyn.str m "role"
    else
        Dyn.str (Dyn.get entry "info") "role"

let private idOfEntry (entry: obj) : string =
    let m = Dyn.get entry "message"

    if not (Dyn.isNullish m) then
        let id = Dyn.str m "id"
        if id <> "" then id else Dyn.str entry "id"
    else
        let info = Dyn.get entry "info"

        if not (Dyn.isNullish info) then
            Dyn.str info "id"
        else
            Dyn.str entry "id"

let ompAdapters =
    { GetParts =
        fun entry ->
            let m = Dyn.get entry "message"

            let p =
                if not (Dyn.isNullish m) then
                    Dyn.get m "content"
                else
                    Dyn.get entry "parts"

            if Dyn.isArray p then unbox p else [||]
      PartType = fun p -> Dyn.str p "type"
      PartToolName = fun p -> Dyn.str p "tool"
      PartCallID = fun p -> Dyn.str p "callID"
      PartState = fun p -> let s = Dyn.get p "state" in if Dyn.isNullish s then None else Some s
      MessageID = fun m -> idOfEntry m
      MessageRole = fun m -> roleOfEntry m
      MessageAgent = fun _ -> ""
      MessageToolName = fun _ -> ""
      MessageIsError = fun _ -> false
      MessageDetails = fun _ -> null
      MessageTime = fun _ -> null
      MessageSessionID = fun _ -> ""
      DecodeToolState = decodeToolState
      DecodeTextPart = fun p -> Dyn.str p "text"
      RequireRole = true }

let decodeEntry sessionID msg =
    Wanxiangshu.Runtime.MessagingDecode.decodeMessage ompAdapters sessionID msg

let decodeEntries sessionID msgs =
    Wanxiangshu.Runtime.MessagingDecode.decodeMessages ompAdapters sessionID msgs
