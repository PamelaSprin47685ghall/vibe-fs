namespace Wanxiangshu.Infrastructure

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.OpenCode

/// Bookkeeper provider verb `js-bookkeeper(program)`.
/// Minimal bridge: program may still carry document/old_text/new_text fields for
/// staged Q/A replacement until the full program SDK lands.
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
            let program = argText args "program"

            let txId =
                match BookkeeperRuntime.tryTxId context.SessionId with
                | Some id -> id
                | None -> BookkeeperRuntime.txIdFor context.SessionId

            if String.IsNullOrWhiteSpace txId then
                return
                    ToolHostCodec.tomlObject
                        [ "error", tString "js-bookkeeper: no Bookkeeper transaction for this session" ]
            elif not (String.IsNullOrWhiteSpace program) && String.IsNullOrWhiteSpace document then
                // Full program SDK is Phase later; empty mutation is legal.
                return ToolHostCodec.tomlObjectWithInstructions [ "# Staged case accepted." ] []
            else
                match BookkeeperStaging.replace txId document oldText newText with
                | Error err -> return ToolHostCodec.tomlObject [ "error", tString err ]
                | Ok() -> return ToolHostCodec.tomlObjectWithInstructions [ "# Staged case rewritten." ] []
        }

    let spec (factory: HostToolFactory) : ToolSpec =
        { Name = "js-bookkeeper"
          Description =
            "Program the next form of a staged case. Prefer program=; document/old_text/new_text remain for unique staged replacement."
          Arguments =
            [ "program", ToolHostCodec.optionalStringSchema factory
              "document", ToolHostCodec.optionalEnumSchema [ "Q.md"; "A.md" ] factory
              "old_text", ToolHostCodec.optionalStringSchema factory
              "new_text", ToolHostCodec.optionalStringSchema factory ]
          Execute = execute }
