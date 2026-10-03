namespace Wanxiangshu.OpenCode

open Wanxiangshu.Persistence.EventStore

module WorkspaceEventStoreSurface =
    let acquire (commonDir: string) : obj =
        WorkspaceEventStore.acquire commonDir |> box

    let activate (store: obj) : unit =
        (unbox<IEventStore> store).TryCurrent "Journal" |> ignore

    let release (commonDir: string) : unit = WorkspaceEventStore.release commonDir

    /// durable-events-019 oracle: an isolated writer whose history program is
    /// the production hostProgram minus exactly one named registration. The
    /// caller owns the lifecycle and keeps the store in a throwaway directory;
    /// removing Structural or Journal throws the integrator's fail-closed
    /// base-rule refusal instead of producing a usable store.
    let createIsolatedWithoutRegistration (commonDir: string) (writerId: string) (ruleName: string) : obj =
        let integrator =
            CanonicalIntegrator.createWithRules
                (WorkspaceEventStore.programWithoutRegistration ruleName)
                AuthoritativeEventTypes.isKnown

        EventStoreHandle.Create(EventStore.createLocal commonDir writerId integrator)
        |> box
