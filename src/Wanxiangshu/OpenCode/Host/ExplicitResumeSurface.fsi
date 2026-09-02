namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

/// JS-native boundary for explicit restart disclosure.
/// Journal and snapshot capabilities remain private to the Host owner.
module ExplicitResumeSurface =

    val registerCommand: config: obj -> unit

    val run: command: string -> sessionId: string -> arguments: string -> Task<obj>
