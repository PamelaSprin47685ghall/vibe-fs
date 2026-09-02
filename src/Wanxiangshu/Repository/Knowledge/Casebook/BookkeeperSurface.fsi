namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks

/// JS-native owner boundary for the Bookkeeper runtime and its staged provider
/// transaction. Session ports remain opaque capabilities; staging snapshots,
/// result envelopes, and tool metadata are plain JavaScript values.
module CasebookBookkeeperSurface =

    /// Configure the Host session capability with explicit immutable authority owners.
    val setRuntime: port: obj -> ownerDescriptors: obj -> obj

    val resetRuntime: unit -> unit

    val bindSession: sessionId: string -> txId: string -> ownerSessionId: string -> unit

    val txIdFor: sessionId: string -> string

    val beginTransaction: txId: string -> question: string -> answer: string -> unit

    val abort: txId: string -> unit

    val snapshot: txId: string -> obj

    val take: txId: string -> obj

    /// Execute one provider program against the currently bound transaction.
    /// Host argument/context decoding and ToolResultBound remain owner-private.
    val runProgram: sessionId: string -> program: string -> Task<string>

    /// Provider-visible metadata without exposing ToolSpec or HostSchema.
    val contract: toolModule: obj -> obj

    val sessionId: value: string -> obj

    val sessionValue: value: obj -> string

    val acceptedSession: value: string -> obj

    val acceptedPrompt: unit -> obj

    val failedPrompt: reason: string -> obj

    val aborted: unit -> obj

    val completed: value: string -> obj
