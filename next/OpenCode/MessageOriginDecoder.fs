namespace Wanxiangshu.Next.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

module MessageOriginDecoder =

    let isSyntheticPart (partObj: obj) : bool =
        if isNull partObj then
            false
        else
            let synthetic = partObj?synthetic
            not (isNull synthetic) && unbox<bool> synthetic

    let isCompactionPart (partObj: obj) : bool =
        if isNull partObj then
            false
        else
            let pType = partObj?``type``
            not (isNull pType) && unbox<string> pType = "compaction"

    let decodeUserMessageOrigin (userMsg: OpencodeUserMessage) : MessageOrigin =
        let parts = userMsg.parts
        let allSynthetic = parts.Length > 0 && (parts |> List.forall isSyntheticPart)
        let hasCompaction = parts |> List.exists isCompactionPart

        if hasCompaction || allSynthetic then
            HostInternal
        else
            Human(TurnId.create userMsg.id)

    let isCompactionAssistant (msg: OpencodeAssistantMessage) : bool =
        match msg.summary with
        | Some true -> true
        | _ ->
            match msg.agent with
            | Some "compaction" -> true
            | _ -> false

    let isAbortedError (errorObj: obj option) : bool =
        match errorObj with
        | None -> false
        | Some err ->
            if isNull err then
                false
            else
                let name = err?name

                not (isNull name)
                && (unbox<string> name = "MessageAbortedError" || unbox<string> name = "AbortError")
