namespace Wanxiangshu.Context.Companion.Blogger.OpenCode

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Foundation.Identity

/// ENFORCER-*: Blogger continuation parking, physical flight ownership and
/// drain windows for one plugin instance. Parked transforms are per-session
/// serial (the dictionary entry is the guard); flights live in SharedState
/// because they must be visible across worktree/root instances.
type PluginBloggerScope =
    new: unit -> PluginBloggerScope

    interface IBloggerRuntimeHost

    /// Session deletion drops the drain slot (unlike CancelParked, which
    /// preserves it). Mirrors DisposeSession's per-session cleanup.
    member DropDrainWindow: sessionId: string -> unit

    /// ENFORCER-162: plugin dispose emits Cancelled to every material waiter.
    member Dispose: unit -> unit

    /// Begin shutting down the scope, cancelling parked waiters and pending offers.
    member BeginShutdown: unit -> unit
