namespace Wanxiangshu.Participant.Cognition

open Wanxiangshu.Foundation

/// The one projection from a committed declaration to the Host's TodoTable.
///
/// HOST-019 (revised): the pinned OpenCode build exposes NO public todo write API —
/// no SDK method, no HTTP route (verified `GET /session/{id}/todo` only, and every
/// other method falls through to the SPA). The single physical sink is the built-in
/// `todowrite` executor, reached through the public `tool.execute.before` args
/// contract. This module produces those args and nothing else: it never writes a
/// database, reflects a private module, or patches the Host.
[<RequireQualifiedAccess>]
module TodoSink =

    /// The row shape the Host executor consumes. `id` is deliberately absent: the
    /// Host assigns positions on a full-table replace, so inventing one would be a
    /// private field the contract does not have.
    type Row =
        { Content: string
          Status: string
          Priority: string }

    /// Project a committed declaration.
    ///
    /// Order and content are preserved exactly. The sink contract is a full-list
    /// replacement, so any rename, dedupe or reordering here would silently change
    /// what the user sees without the model having asked for it.
    let rows (todos: TodoRow list) : Row list =
        todos
        |> List.map (fun row ->
            { Content = row.Content
              Status = TodoStatus.wire row.Status
              Priority = TodoPriority.wire row.Priority })

    /// The `todos` array the Host executor reads from `output.args`.
    ///
    /// Kept non-enumerable by the caller (the Host's Effect Schema decodes it while
    /// JSON persistence keeps the original `update` bytes), which is why this returns
    /// a plain list the adapter can attach rather than a merged argument object.
    let compatibilityArgs (todos: TodoRow list) : Row list = rows todos
