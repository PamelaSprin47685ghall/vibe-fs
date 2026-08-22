namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts

/// Pure typed-fact codec owner. It deliberately has no projection resource
/// handle; callers that need replay state use MagicTodoProjectionSurface.
[<RequireQualifiedAccess>]
module MagicTodoProjectionCodecSurface =

    let encode (factJson: string) : obj =
        match MagicTodoFactCodec.tryDecode factJson with
        | Error error -> box {| ok = false; error = error |}
        | Ok fact ->
            box
                {| ok = true
                   value = MagicTodoFactCodec.encode fact |}

    let decode (factJson: string) : obj =
        match MagicTodoFactCodec.tryDecode factJson with
        | Error error -> box {| ok = false; error = error |}
        | Ok(MagicTodoFact.TodoWritePrepared prepared) ->
            box
                {| ok = true
                   case = "TodoWritePrepared"
                   planCompleteDeclared = prepared.PlanCompleteDeclared
                   normalized = MagicTodoFactCodec.encode (MagicTodoFact.TodoWritePrepared prepared) |}
        | Ok(MagicTodoFact.TodoWriteAccepted accepted) ->
            box
                {| ok = true
                   case = "TodoWriteAccepted"
                   planCompleteDeclared = null
                   normalized = MagicTodoFactCodec.encode (MagicTodoFact.TodoWriteAccepted accepted) |}
        | Ok(MagicTodoFact.LegacyTodoSeedAdopted fact) ->
            box
                {| ok = true
                   case = "LegacyTodoSeedAdopted"
                   planCompleteDeclared = null
                   normalized = MagicTodoFactCodec.encode (MagicTodoFact.LegacyTodoSeedAdopted fact) |}
        | Ok(MagicTodoFact.PrefixRebaseCommittedV2 fact) ->
            box
                {| ok = true
                   case = "PrefixRebaseCommittedV2"
                   planCompleteDeclared = null
                   normalized = MagicTodoFactCodec.encode (MagicTodoFact.PrefixRebaseCommittedV2 fact) |}
