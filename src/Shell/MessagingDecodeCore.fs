module Wanxiangshu.Shell.MessagingDecodeCore

open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn

/// Host-specific field extraction bundled into a record. Each adapter function
/// performs exactly one Dyn access so the generic decode paths never touch host
/// object internals directly. This keeps the FFI boundary disciplines in one
/// place and makes adding a new host a matter of writing eleven pure functions.
type DecodeAdapters =
    { GetParts: obj -> obj array
      PartType: obj -> string
      PartToolName: obj -> string
      PartCallID: obj -> string
      PartState: obj -> obj option
      MessageID: obj -> string
      MessageRole: obj -> string
      MessageAgent: obj -> string
      MessageToolName: obj -> string
      MessageIsError: obj -> bool
      MessageDetails: obj -> obj
      MessageTime: obj -> obj
      MessageSessionID: obj -> string
      DecodeToolState: obj -> ToolState<obj> option
      DecodeTextPart: obj -> string
      RequireRole: bool }

let decodeSinglePart (adapters: DecodeAdapters) (raw: obj) : Part<obj> option =
    match adapters.PartType raw with
    | "text"
    | "reasoning" -> Some(TextPart(adapters.DecodeTextPart raw))
    | t when t = "tool" || t = "dynamic-tool" ->
        let tool = adapters.PartToolName raw
        let callID = adapters.PartCallID raw
        let state = adapters.PartState raw |> Option.bind adapters.DecodeToolState
        Some(ToolPart(tool, callID, state, raw))
    | _ -> Some(RawPart raw)

let decodeParts (adapters: DecodeAdapters) (msg: obj) : Part<obj> list =
    adapters.GetParts msg
    |> Array.choose (decodeSinglePart adapters)
    |> List.ofArray

let decodeMessage (adapters: DecodeAdapters) (sessionID: string) (raw: obj) : Message<obj> option =
    if Dyn.isNullish raw then
        None
    else
        let role = adapters.MessageRole raw

        if adapters.RequireRole && role = "" then
            None
        else
            let id = adapters.MessageID raw

            let msgSessionID =
                let s = adapters.MessageSessionID raw
                if not (System.String.IsNullOrEmpty s) then s else sessionID

            let parts = decodeParts adapters raw

            Some
                { info =
                    { id = id
                      sessionID = msgSessionID
                      role = decodeRole role
                      agent = adapters.MessageAgent raw
                      isError = adapters.MessageIsError raw
                      toolName = adapters.MessageToolName raw
                      details = adapters.MessageDetails raw
                      time = adapters.MessageTime raw }
                  parts = parts
                  source = classifySource id (Some parts) (Some raw)
                  raw = raw }

let decodeMessages (adapters: DecodeAdapters) (sessionID: string) (messages: obj array) : Message<obj> list =
    if Dyn.isNullish messages then
        []
    else
        messages |> Array.choose (decodeMessage adapters sessionID) |> List.ofArray
