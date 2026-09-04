namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Context.Companion
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Foundation.Identity

/// Per-instance session registry state for one plugin instance (HOST-012):
/// owned sessions, user-message bindings, companions, verdicts, nudges,
/// quiescence permits and join interrupts. Shared cross-worktree state stays
/// in SharedState; everything here is per-instance and dies with the scope.
type PluginSessionScope =
    new: unit -> PluginSessionScope

    /// Cross-instance session directory map alias.
    member SessionDirectories: Dictionary<string, string>

    /// Per-instance owned session set.
    member OwnedSessions: HashSet<string>

    /// Per-plugin-instance routing demands.
    member ModelRoutingSessions: HashSet<string>

    /// Per-instance user message binding map.
    member UserMessageBindings: Dictionary<string, PhysicalUserMessageId>

    /// Cross-instance session parent map alias.
    member SessionParents: Dictionary<string, string>

    /// Per-instance companion registry.
    member Companions: Dictionary<string, CompanionHost>

    /// Lock gate object for companion operations.
    member CompanionGate: obj

    /// Cross-instance verdict session set alias.
    /// Per-instance nudge sent set.
    member NudgeSent: HashSet<string>

    /// Per-instance join guard nudge set.
    member JoinGuardNudges: HashSet<string>

    /// Per-instance quiescence gate instance.
    member Quiescence: SessionQuiescenceGate

    /// Per-instance join interrupt registry.
    member JoinInterrupts: IJoinAttemptRegistry

    /// Returns the keys to cancel (including sessionId itself when it is not
    /// a Main with a linked Blogger).
    member LinkedBloggerKeys: sessionId: string -> string list

    member DropSessionIdentity: sessionId: string -> unit

    /// Session deletion drops every per-instance registry entry for this session.
    member ClearSession: sessionId: string * preserveIdentity: bool -> unit

    /// Plugin dispose releases every companion host and every routing demand/lease.
    member Dispose: unit -> unit
