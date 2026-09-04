namespace Wanxiangshu.Change.Host

open System.Threading.Tasks

/// JS-native owner for the OrchestratorHost semantic harness.
///
/// Host runtime state, Relay port, journal projection and typed ports remain
/// opaque. The harness supplies plain JavaScript port observations; this owner
/// translates them once into the real Host contracts.
[<RequireQualifiedAccess>]
module OrchestratorHostSurface =

    /// Build a real OrchestratorHost from plain JavaScript port contracts.
    /// `sessions`, `gitPort`, and `journal` are capabilities owned by the caller;
    /// this function never projects their internal representation.
    val create: options: obj -> obj

    val detachAndDrain: handle: obj -> Task

    val hasChild: handle: obj -> agentId: string -> bool

    /// Exercise the production candidate-finalization sequence through a plain
    /// JavaScript command port without exposing Command or Result internals.
    val finalizeWorktree: runner: obj -> managerId: string -> worktree: string -> Task<obj>
