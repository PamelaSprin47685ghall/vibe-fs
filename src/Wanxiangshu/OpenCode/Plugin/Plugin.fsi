namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.HostSession

open System.Threading.Tasks
open Fable.Core

type PluginConfig = { Directory: string }

module Plugin =

    val initPlugin: input: obj -> Task<obj>

    [<ExportDefault>]
    val defaultExport: obj
