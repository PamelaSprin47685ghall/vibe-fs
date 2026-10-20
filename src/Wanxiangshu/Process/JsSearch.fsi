namespace Wanxiangshu.Process

open System.Threading.Tasks

module JsSearch =
    val execute:
        scope: JsCallLoop.ExecutionScope -> root: string -> arguments: string array -> pathsOnly: bool -> Task<obj>
