namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks

/// Session-scoped resource owner implemented by the tool runtime without
/// exposing its concrete dictionaries to the plugin composition root.
type ISessionRuntimeOwner =
    inherit IDisposable
    abstract CancelSessionChildren: string -> Task
    abstract DisposeSession: string -> Task
    abstract DisposeExecutorRuntime: string -> Task
    /// MANAGED-SESSION-018: plugin shutdown drains process-local observers without
    /// manufacturing logical parent cancellation. Durable Active handles survive
    /// for restart recovery before the shared Journal/EventStore is released.
    abstract DisposeAsync: unit -> Task
    /// EXEC-016: live PTY still tracked for this parent session (DevOps).
    abstract HasLivePty: string -> bool
