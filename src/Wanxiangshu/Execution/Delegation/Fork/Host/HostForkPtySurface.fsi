namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Process

[<RequireQualifiedAccess>]
module HostForkPtySurface =
    val ownershipProbe: commonDir: string option -> sessionId: string -> onCommand: obj -> obj
    val realOwnershipProbe: commonDir: string option -> sessionId: string -> obj
    val sharedOwnershipProbe: commonDir: string option -> sessionId: string -> onCommand: obj -> obj
    val realSharedOwnershipProbe: commonDir: string option -> sessionId: string -> obj
    val scenario: action: string -> input: string -> failure: string -> journal: JournalHandle option -> Task<obj>
