namespace Wanxiangshu.Infrastructure

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.OpenCode

/// Dedicated Bookkeeper tool: unique replacement in staged Q.md / A.md.
/// No filesystem path; missing or ambiguous old_text is a tool failure.
module EditQaTool =

    [<Fable.Core.Emit("$0 === undefined || $0 === null")>]
    let private isUndefined (value: obj) : bool = jsNative

    let private tString = ToolHostCodec.TString

    let private argText (args: HostToolArguments) (name: string) : string =
        try
            let typed = args.Text name

            if not (String.IsNullOrWhiteSpace typed) then
                typed
            else
                let raw = args?(name)
                if isUndefined raw then "" else string raw
        with _ ->
            try
                let raw = args?(name)
                if isUndefined raw then "" else string raw
            with _ ->
                ""

    let execute (args: HostToolArguments) (context: HostToolContext) : Task<string> =
        task {
            let document = argText args "document"
            let oldText = argText args "old_text"
            let newText = argText args "new_text"

            let txId =
                match BookkeeperRuntime.tryTxId context.SessionId with
                | Some id -> id
                | None -> BookkeeperRuntime.txIdFor context.SessionId

            if String.IsNullOrWhiteSpace txId then
                return
                    ToolHostCodec.tomlObject [ "error", tString "edit-qa: no Bookkeeper transaction for this session" ]
            else
                match BookkeeperStaging.replace txId document oldText newText with
                | Error err -> return ToolHostCodec.tomlObject [ "error", tString err ]
                | Ok() -> return ToolHostCodec.tomlObject [ "status", tString "replaced"; "document", tString document ]
        }

    let spec (factory: HostToolFactory) : ToolSpec =
        { Name = "edit-qa"
          Description =
            "Replace unique old_text with new_text in the current Bookkeeper staged document. "
            + "document is Q.md or A.md. Missing or ambiguous old_text fails. No filesystem path."
          Arguments =
            [ "document", ToolHostCodec.enumSchema [ "Q.md"; "A.md" ] factory
              "old_text", ToolHostCodec.stringSchema factory
              "new_text", ToolHostCodec.stringSchema factory ]
          Execute = execute }
