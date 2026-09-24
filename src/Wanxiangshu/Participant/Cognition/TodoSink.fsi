namespace Wanxiangshu.Participant.Cognition

[<RequireQualifiedAccess>]
module TodoSink =
    type Row =
        { Content: string
          Status: string
          Priority: string }

    val rows: todos: TodoRow list -> Row list
    val compatibilityArgs: todos: TodoRow list -> Row list
