namespace Wanxiangshu.Strength.OpenCode

open Fable.Core
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Replica

/// STRENGTH-*: decision-local replica ownership/capability registry plus the
/// process-lifetime fuse for one plugin instance. Durable causality stays in
/// EventStore; this is only live physical-session state (STRENGTH-014).
[<AttachMembers>]
type PluginStrengthScope =
    new: unit -> PluginStrengthScope

    member StrengthRuntime: StrengthRuntime
    member AttachStrengthReplicaRuntime: runtime: StrengthReplicaRuntime -> unit
    member StrengthReplicaRuntime: StrengthReplicaRuntime option

    member TripStrengthFuse: reason: string -> unit
    member StrengthFuseReason: string option
    member StrengthFuse: Result<unit, string>

    member ClearSession: sessionId: string -> unit
    member Dispose: unit -> unit
