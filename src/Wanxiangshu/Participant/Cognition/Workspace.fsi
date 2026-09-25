namespace Wanxiangshu.Participant.Cognition

type AssumeSnapshot =
    { CanvasEncodingVersion: string
      CanvasJson: string
      Todos: TodoRow list }

and TodoRow =
    { Content: string
      Status: TodoStatus
      Priority: TodoPriority }

and [<RequireQualifiedAccess>] TodoStatus =
    | Pending
    | InProgress
    | Completed
    | Cancelled

and [<RequireQualifiedAccess>] TodoPriority =
    | High
    | Medium
    | Low

[<RequireQualifiedAccess>]
module TodoStatus =
    val wire: status: TodoStatus -> string
    val tryParse: text: string -> TodoStatus option

[<RequireQualifiedAccess>]
module TodoPriority =
    val wire: priority: TodoPriority -> string
    val tryParse: text: string -> TodoPriority option

[<RequireQualifiedAccess>]
module AssumeSnapshot =
    val empty: AssumeSnapshot
    val ofJson: canvasJson: string -> rows: (string * TodoStatus * TodoPriority) list -> AssumeSnapshot
    val normalizeTodos: rows: (string * TodoStatus * TodoPriority) list -> TodoRow list
    val json: snapshot: AssumeSnapshot -> string
    val tryParseJson: text: string -> Result<AssumeSnapshot, string>
