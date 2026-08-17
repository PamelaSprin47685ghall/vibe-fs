namespace Wanxiangshu.OpenCode

open Fable.Core.JsInterop

/// JS-native observation surface for the chat.params binding barrier.
/// The hook mutates only the approved temperature field; provider identity is
/// validated against the session execution binding and never inferred.
module ChatParamsSurface =

    let private modelId (value: obj) : obj =
        if isNull value then
            null
        else
            let model = value?model
            if isNull model then null else model?modelID

    let apply (input: obj) (output: obj) : obj =
        let hook = ChatParamsHook.create () |> unbox<obj -> obj -> unit>

        try
            hook input output

            box
                {| ok = true
                   error = ""
                   modelID = modelId output
                   temperature = if isNull output then null else output?temperature |}
        with ex ->
            box
                {| ok = false
                   error = ex.Message
                   modelID = modelId output
                   temperature = if isNull output then null else output?temperature |}
