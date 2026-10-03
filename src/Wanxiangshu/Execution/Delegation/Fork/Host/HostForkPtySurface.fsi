namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module HostForkPtySurface =
    val scenario: action: string -> input: string -> failure: string -> journal: JournalHandle option -> Task<obj>
