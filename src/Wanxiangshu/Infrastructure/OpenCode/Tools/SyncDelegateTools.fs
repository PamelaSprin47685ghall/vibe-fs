namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Session

/// Unified `return` tool for SyncDelegate Inspector/Coder.
module SyncDelegateTools =

    let private render =
        function
        | Ok value -> ToolResultBound.bound value
        | Error error -> ToolHostCodec.tomlObject [ "error", ToolHostCodec.TString error ]

    let returnSpec (factory: HostToolFactory) (syncDelegate: SyncDelegateRuntime option) : ToolSpec =
        { Name = "return"
          Description = "Return a SyncDelegate (Inspector/Coder) answer."
          Arguments = [ "message", ToolHostCodec.stringSchema factory ]
          Execute =
            fun args ctx ->
                task {
                    let message = args.Text "message"

                    match syncDelegate with
                    | Some sd ->
                        let! result = sd.Return(ctx.SessionId, ctx.ProviderRunId, message)
                        return render result
                    | None -> return ToolHostCodec.tomlObject [ "error", ToolHostCodec.TString "return unavailable" ]
                } }
