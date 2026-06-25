module VibeFs.Shell.OpencodeSessionPromptCodec

open Fable.Core.JsInterop
open VibeFs.Shell.Dyn

let tryDecodePromptModelFromModelString (modelString: string) : obj option =
    if modelString = "" then None
    else
        let slash = modelString.IndexOf('/')
        if slash <= 0 || slash >= modelString.Length - 1 then None
        else Some (box {| providerID = modelString.[0..slash - 1]; modelID = modelString.[slash + 1..] |})

let tryDecodePromptModelFromPayload (payload: obj) : obj option =
    let promptModel = Dyn.get payload "model"
    if not (Dyn.isNullish promptModel) then Some promptModel
    else tryDecodePromptModelFromModelString (Dyn.str payload "modelString")