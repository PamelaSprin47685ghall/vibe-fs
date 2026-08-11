namespace Wanxiangshu.Domain

open Thoth.Json
open Wanxiangshu.Domain.MagicTodo

/// Canonical durable body for SettledCurrent / BaseTodo / ProposedTodo blobs.
///
/// Journal facts carry only blob locators and their write receipts; this codec is
/// the sole interpreter of the referenced list body. Unknown status values fail
/// closed during recovery rather than becoming a stringly typed todo state.
module MagicTodoListCodec =

    let private decodeStatus (raw: string) =
        match TodoStatus.parse raw with
        | Some status -> status
        | None -> failwithf "unknown Magic Todo status: %s" raw

    let private itemDecoder: Decoder<MagicTodoItem> =
        Decode.object (fun get ->
            { Id = TodoItemId.create (get.Required.Field "id" Decode.string)
              Content = get.Required.Field "content" Decode.string
              Status = get.Required.Field "status" Decode.string |> decodeStatus
              Priority = get.Required.Field "priority" Decode.string })

    let encode (items: MagicTodoList) : string = MagicTodo.canonicalListWire items

    let tryDecode (json: string) : Result<MagicTodoList, string> =
        try
            Decode.fromString (Decode.list itemDecoder) json
        with error ->
            Error error.Message
