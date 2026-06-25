module VibeFs.Omp.Codec

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Omp.Schema
module Dyn = VibeFs.Shell.Dyn

type ToolTextResult = { ``type``: string; text: string }

type ToolResult =
    { content: ToolTextResult array
      isError: bool option
      display: bool option }

let textResult (text: string) : ToolResult =
    { content = [| { ``type`` = "text"; text = text } |]
      isError = None
      display = None }

let errorResult (text: string) : ToolResult =
    { content = [| { ``type`` = "text"; text = text } |]
      isError = Some true
      display = None }

let asErrorResult (error: obj) : ToolResult =
    let msg =
        if Dyn.typeIs error "Error" then
            string (Dyn.get error "message")
        else
            string error
    errorResult msg

let getSessionIdFromContext (ctx: obj) : string option =
    let sm = Dyn.get ctx "sessionManager"
    if Dyn.isNullish sm then None
    else
        let fromFn =
            let getId = Dyn.get sm "getSessionId"
            if Dyn.typeIs getId "function" then
                let id = Dyn.call0 getId
                if Dyn.isNullish id then None else Some (string id)
            else None
        match fromFn with
        | Some id -> Some id
        | None ->
            let sid = Dyn.get sm "sessionId"
            if Dyn.isNullish sid then None else Some (string sid)

let stringArraySchema (pi: obj) (description: string) : obj =
    let tb = Dyn.get pi "typebox"
    strArray description tb

let createAbortError () : obj =
    createObj [ "name", box "AbortError"; "message", box "Aborted" ]

let hasErrorName (error: obj) (name: string) : bool =
    not (Dyn.isNullish error) && string (Dyn.get error "name") = name

let optInt (o: obj) (key: string) : int option =
    let v = Dyn.get o key
    if Dyn.isNullish v then None else Some(unbox<int> v)