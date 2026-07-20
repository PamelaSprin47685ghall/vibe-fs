module Wanxiangshu.Runtime.ToolHookIdentity

open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.DynField

let tryExtractToolCallId (input: obj) : string option =
    if Dyn.isNullish input then
        None
    else
        let getField k obj =
            match DynField.strField obj k with
            | Some id when id <> "" -> Some id
            | _ -> None

        match getField "toolCallId" input with
        | Some id -> Some id
        | None ->
            match getField "callId" input with
            | Some id -> Some id
            | None ->
                match getField "callID" input with
                | Some id -> Some id
                | None ->
                    let tool = Dyn.get input "tool"

                    if not (Dyn.isNullish tool) && Dyn.typeIs tool "object" then
                        match getField "callID" tool with
                        | Some id -> Some id
                        | None ->
                            match getField "callId" tool with
                            | Some id -> Some id
                            | None -> getField "toolCallId" tool
                    else
                        None

let tryExtractSessionId (input: obj) : string option =
    if Dyn.isNullish input then
        None
    else
        let getField k obj =
            match DynField.strField obj k with
            | Some id when id <> "" -> Some id
            | _ -> None

        match getField "sessionID" input with
        | Some id -> Some id
        | None ->
            match getField "sessionId" input with
            | Some id -> Some id
            | None ->
                match getField "session_id" input with
                | Some id -> Some id
                | None ->
                    match getField "workspaceId" input with
                    | Some id -> Some id
                    | None ->
                        let s = Dyn.get input "session"

                        if not (Dyn.isNullish s) && Dyn.typeIs s "object" then
                            getField "id" s
                        else
                            None
