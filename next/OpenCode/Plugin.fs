namespace Wanxiangshu.Next.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

type PluginConfig = { Directory: string }

module Plugin =

    let initPlugin (input: obj) : Task<obj> = SpikePlugin.initSpikePlugin input

    [<ExportDefault>]
    let defaultExport =
        createObj
            [ "id", box "wanxiangshu-next"
              "server", box (fun (input: obj) -> initPlugin input) ]
