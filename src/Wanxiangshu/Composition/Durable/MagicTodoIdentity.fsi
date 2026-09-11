namespace Wanxiangshu.Mission.Obligation.Todo

/// Canonical checkpoint identity (durable-convergence-owned). Same type the
/// work shard's `MagicTodo.TodoWriteId` aliases; namespace frozen for consumers.
module MagicTodoIdentity =
    type TodoWriteId = private TodoWriteId of string

    module TodoWriteId =
        val create: value: string -> TodoWriteId
        val value: TodoWriteId -> string
