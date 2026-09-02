namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module HostForkPtySurface =
    val scenario: action: string -> input: string -> failure: string -> Task<obj>
