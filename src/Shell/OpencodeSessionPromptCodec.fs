module Wanxiangshu.Shell.OpencodeSessionPromptCodec

open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn

let tryDecodePromptModelFromModelString (modelString: string) : obj option =
    if modelString = "" then None
    else
        let basePart =
            match modelString.IndexOf(':') with
            | -1 -> modelString
            | colon -> modelString.[0..colon - 1].Trim()
        if basePart = "" then None
        else
            let slash = basePart.IndexOf('/')
            if slash <= 0 || slash >= basePart.Length - 1 then None
            else Some (box {| providerID = basePart.[0..slash - 1]; modelID = basePart.[slash + 1..] |})

let tryDecodePromptModelFromPayload (payload: obj) : obj option =
    let promptModel = Dyn.get payload "model"
    if not (Dyn.isNullish promptModel) then Some promptModel
    else tryDecodePromptModelFromModelString (Dyn.str payload "modelString")