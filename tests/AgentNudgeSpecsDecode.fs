module Wanxiangshu.Tests.AgentNudgeSpecsDecode

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Hosts.OpenCode.OpencodeSessionEventCodec

let decodeTodosOpenItems () =
    let todos =
        decodeTodos (
            box
                [| box
                       {| content = "finish feature"
                          status = "in_progress" |}
                   box
                       {| content = "done item"
                          status = "completed" |}
                   box {| content = ""; status = "pending" |} |]
        )

    equal "decodeTodos uses content not status" [ "finish feature" ] todos
