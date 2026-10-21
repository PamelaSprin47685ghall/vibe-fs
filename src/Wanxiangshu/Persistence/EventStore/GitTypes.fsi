namespace Wanxiangshu.Persistence.EventStore

/// The one Git ref name the unified store reserves (durable-events-016). No Git object, tree,
/// snapshot or remote-sync type remains: local append never touches Git (durable-events-017) and
/// no remote-sync membrane exists (CUT-19-c).
[<RequireQualifiedAccess>]
module StoreRef =
    val canonical: string
