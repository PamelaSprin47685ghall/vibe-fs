namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks

/// JS-native owner boundary for the Bookkeeper runtime and its staged provider
/// transaction.
module CasebookBookkeeperSurface =

    val createRefreshPrompt: input: obj -> string

    val setRuntime: port: obj -> ownerDescriptors: obj -> obj

    val resetRuntime: unit -> unit

    val bindSession: sessionId: string -> txId: string -> ownerSessionId: string -> unit
    val unbindSession: sessionId: string -> unit
    val txIdFor: sessionId: string -> string

    val beginTransaction: txId: string -> question: string -> answer: string -> unit

    val abort: txId: string -> unit

    val snapshot: txId: string -> obj

    val take: txId: string -> obj

    val runProgram: sessionId: string -> program: string -> Task<string>

    val contract: toolModule: obj -> obj

    val sessionId: value: string -> obj

    val sessionValue: value: obj -> string

    val acceptedSession: value: string -> obj

    val acceptedPrompt: unit -> obj

    val failedPrompt: reason: string -> obj

    val aborted: unit -> obj

    val completed: value: string -> obj
