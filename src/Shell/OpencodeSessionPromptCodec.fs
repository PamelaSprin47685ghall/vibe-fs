module Wanxiangshu.Shell.OpencodeSessionPromptCodec

open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn

let tryDecodePromptModelFromModelString (modelString: string) : obj option =
    if modelString = "" then
        None
    else
        let colon = modelString.IndexOf(':')

        let basePart =
            if colon = -1 then
                modelString
            else
                modelString.[0 .. colon - 1].Trim()

        let variant =
            if colon = -1 then
                None
            else
                let v = modelString.[colon + 1 ..].Trim()
                if v = "" then None else Some v

        if basePart = "" then
            None
        else
            let slash = basePart.IndexOf('/')

            if slash <= 0 || slash >= basePart.Length - 1 then
                None
            else
                let p = basePart.[0 .. slash - 1]
                let m = basePart.[slash + 1 ..]

                Some(
                    box
                        {| providerID = p
                           modelID = m
                           variant =
                            match variant with
                            | Some v -> box v
                            | None -> null |}
                )

let tryDecodePromptModelFromPayload (payload: obj) : obj option =
    let promptModel = Dyn.get payload "model"

    if not (Dyn.isNullish promptModel) then
        Some promptModel
    else
        tryDecodePromptModelFromModelString (Dyn.str payload "modelString")
