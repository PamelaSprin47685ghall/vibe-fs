module VibeFs.Shell.ToolRuntimeContext

open VibeFs.Kernel.Domain
open VibeFs.Kernel.ToolContext
open VibeFs.Shell.Dyn
open VibeFs.Shell.ToolContextCodec

type IToolRuntimeContext = {
    Execution: ToolExecutionContext
    AbortSignal: obj option
}

let fromMuxConfig (config: obj) : Result<IToolRuntimeContext, DomainError> =
    decodeMuxConfig config
    |> Result.map (fun execution ->
        let signal = Dyn.get config "abortSignal"
        {
            Execution = execution
            AbortSignal = if Dyn.isNullish signal then None else Some signal
        })

let fromOpencode (context: obj) (fallbackDir: string) : IToolRuntimeContext =
    let execution = decodeOpencodeToolContext context fallbackDir
    let signal =
        if Dyn.isNullish context then None
        else
            let a = Dyn.get context "abort"
            if Dyn.isNullish a then None else Some a
    { Execution = execution; AbortSignal = signal }

let pluginDirectoryFromCtx (ctx: obj) : string =
    (fromOpencode ctx "").Execution.Directory