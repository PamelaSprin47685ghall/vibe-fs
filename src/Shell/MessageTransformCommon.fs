module VibeFs.Shell.MessageTransformCommon

open VibeFs.Shell.Dyn

let extractTextsFromEncodedMessages (messages: obj array) : string seq =
    messages
    |> Seq.collect (fun msg ->
        let parts = Dyn.get msg "parts"
        if Dyn.isNullish parts || not (Dyn.isArray parts) then Seq.empty
        else
            (parts :?> obj array)
            |> Seq.choose (fun part ->
                if Dyn.str part "type" = "text" then
                    let text = Dyn.str part "text"
                    if text <> "" then Some text else None
                elif Dyn.str part "type" = "dynamic-tool" then
                    let output = Dyn.get part "output"
                    if not (Dyn.isNullish output) then
                        let text =
                            if Dyn.typeIs output "string" then string output
                            else Dyn.str output "content"
                        if text <> "" then Some text else None
                    else None
                else None))