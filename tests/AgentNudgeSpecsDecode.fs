module VibeFs.Tests.AgentNudgeSpecsDecode

open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Shell.OpencodeSessionEventCodec

let decodeTodosOpenItems () =
    let todos =
        decodeTodos
            (box [|
                box {| content = "finish feature"; status = "in_progress" |}
                box {| content = "done item"; status = "completed" |}
                box {| content = ""; status = "pending" |}
            |])
    equal "decodeTodos uses content not status" [ "finish feature" ] todos
