namespace Wanxiangshu.Next.Tests.OpenCodeTests

open System
open Xunit
open Wanxiangshu.Next.OpenCode

module PluginRuntimeScopeTests =

    type private RecordingOwner() =
        let mutable disposed = 0
        let mutable disposedSessions: string list = []
        let mutable disposedExecutors: string list = []

        member _.Disposed = disposed
        member _.DisposedSessions = disposedSessions
        member _.DisposedExecutors = disposedExecutors

        interface ISessionRuntimeOwner with
            member _.DisposeSession sessionId =
                disposedSessions <- sessionId :: disposedSessions

            member _.DisposeExecutorRuntime sessionId =
                disposedExecutors <- sessionId :: disposedExecutors

        interface IDisposable with
            member _.Dispose() = disposed <- disposed + 1

    type private RecordingSubscription() =
        let mutable disposed = 0
        member _.Disposed = disposed

        interface IDisposable with
            member _.Dispose() = disposed <- disposed + 1

    [<Fact>]
    let ``plugin scopes isolate companion budgets`` () =
        use first = new PluginRuntimeScope(None)
        use second = new PluginRuntimeScope(None)

        first.CompanionBudgets.Remember("session-1", 4096)

        Assert.Equal(Some 4096, first.CompanionBudgets.TryFind "session-1")
        Assert.Equal(None, second.CompanionBudgets.TryFind "session-1")

    [<Fact>]
    let ``plugin scope owns session resources and disposes once`` () =
        let scope = new PluginRuntimeScope(None)
        let owner = new RecordingOwner()
        let subscription = new RecordingSubscription()

        scope.AttachToolRuntime(owner :> ISessionRuntimeOwner)
        scope.TrackSubscription(Some(subscription :> IDisposable))
        scope.SessionRoles.["session-1"] <- "fast-manager"
        scope.DisposeExecutorRuntime "session-1"
        scope.DisposeSession "session-1"
        scope.Dispose()
        scope.Dispose()

        Assert.Equal<string list>([ "session-1" ], owner.DisposedExecutors)
        Assert.Equal<string list>([ "session-1" ], owner.DisposedSessions)
        Assert.Equal(1, owner.Disposed)
        Assert.Equal(1, subscription.Disposed)
        Assert.False(scope.SessionRoles.ContainsKey "session-1")
