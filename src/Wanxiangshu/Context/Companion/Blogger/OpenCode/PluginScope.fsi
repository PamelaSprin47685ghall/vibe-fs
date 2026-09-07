namespace Wanxiangshu.Context.Companion.Blogger.OpenCode

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Foundation.Identity

/// Blogger continuation material mailbox and physical flight ownership for one
/// plugin instance. Parked transforms are per-session
/// serial (the dictionary entry is the guard); flights live in SharedState
/// because they must be visible across worktree/root instances.
type PluginBloggerScope =
    new: unit -> PluginBloggerScope

    interface IBloggerRuntimeHost

    /// ENFORCER-162: plugin dispose emits Cancelled to every material waiter.
    member Dispose: unit -> unit

    /// Begin shutting down the scope, cancelling parked waiters and pending offers.
    member BeginShutdown: unit -> unit

    /// Cancels and removes any active repair episodes associated with the session.
    member CancelEpisodesForSession: sessionId: string -> unit

    /// Awaits stored repair episode completions.
    member DrainRepairEpisodes: unit -> Task
