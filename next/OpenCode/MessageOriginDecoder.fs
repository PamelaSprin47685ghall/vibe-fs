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

    let tryExtractPromptKey (partObj: obj) : PromptKey option =
        if isNull partObj then
            None
        else
            let meta = partObj?metadata

            if isNull meta then
                None
            else
                let keyStr = meta?wanxiangshu_prompt_key

                if not (isNull keyStr) then
                    PromptKey.parse (unbox<string> keyStr)
                else
                    let keyStr2 = meta?promptKey

                    if not (isNull keyStr2) then
                        PromptKey.parse (unbox<string> keyStr2)
                    else
                        None

    let decodeUserMessageOrigin (userMsg: OpencodeUserMessage) : MessageOrigin =
        let parts = userMsg.parts
        let hasCompaction = parts |> List.exists isCompactionPart
        let pluginKeyOpt = parts |> List.tryPick tryExtractPromptKey

        match pluginKeyOpt with
        | Some key -> PluginGenerated(PromptKeyRef.create (PromptKey.asString key))
        | None when hasCompaction -> HostInternal
        | None ->
            let allSynthetic = parts.Length > 0 && (parts |> List.forall isSyntheticPart)

            if allSynthetic then
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
