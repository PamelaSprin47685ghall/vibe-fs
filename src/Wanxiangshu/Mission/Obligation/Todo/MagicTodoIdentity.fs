namespace Wanxiangshu.Mission.Obligation.Todo

/// Canonical checkpoint identity (durable-convergence-owned).
///
/// Digest(IncumbencyId + ToolCallId). Same ToolCallId replay → same id.
/// Lives beside the fact algebra that keys on it, in the persistence-owned
/// compile shard, so the journal fold no longer reaches into the work shard's
/// `MagicTodo` decision module. That module keeps a same-type alias for its
/// admission/validation callers. Namespace stays frozen so existing journal
/// consumers resolve without edits.
module MagicTodoIdentity =

    type TodoWriteId = private TodoWriteId of string

    module TodoWriteId =
        let create (value: string) = TodoWriteId value
        let value (TodoWriteId value) = value
