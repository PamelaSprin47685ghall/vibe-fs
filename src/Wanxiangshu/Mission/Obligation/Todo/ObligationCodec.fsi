namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Mission.Obligation.Todo.MagicTodo

module MagicTodoObligationCodec =
    val encode: items: ObligationList -> string
    val tryDecode: json: string -> Result<ObligationList, string>
    val tryDecodeInput: json: string -> Result<TodoWriteInput, string>
