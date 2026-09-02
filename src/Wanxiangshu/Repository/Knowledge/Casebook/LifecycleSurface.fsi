namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks

/// JS-native lifecycle boundary for the Casebook draft and observation flow.
/// Draft storage, collector state, and Bookkeeper/Journal capabilities remain
/// private to the lifecycle owner.
module CasebookLifecycleSurface =

    val enable: workspaceRoot: string -> unit

    val disable: unit -> unit

    val isEnabled: unit -> bool

    val notePrompt: sessionId: string -> question: string -> unit

    val noteAnswer: sessionId: string -> answer: string -> unit

    val collect: sessionId: string -> toolName: string -> args: obj -> output: string -> unit

    val observationCount: sessionId: string -> int

    val cleanup: sessionId: string -> unit

    val tryFinalize: workspaceRoot: string -> sessionId: string -> Task<obj>

    val touchAccess: workspaceRoot: string -> sessionId: string -> Task<unit>
