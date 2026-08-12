namespace Wanxiangshu.Domain

open Thoth.Json
open Wanxiangshu.Domain.MagicTodo

/// Canonical durable body for SettledCurrent / BaseTodo / ProposedTodo blobs.
///
/// Journal facts carry only blob locators and their write receipts; this codec is
/// the sole interpreter of the referenced list body. Unknown status values fail
/// closed during recovery rather than becoming a stringly typed todo state.
module MagicTodoObligationCodec =

    let private obligationDecoder: Decoder<Obligation> =
        Decode.object (fun get ->
            { Name = get.Required.Field "name" Decode.string
              Work = get.Required.Field "work" Decode.string })

    let encode (items: ObligationList) : string = MagicTodo.canonicalObligationListWire items

    let tryDecode (json: string) : Result<ObligationList, string> =
        try
            Decode.fromString (Decode.list obligationDecoder) json
        with error ->
            Error error.Message

    /// Decode the provider-facing todowrite argument object rather than a
    /// canonical blob body. Keeping these two wire layers explicit prevents a
    /// before-hook snapshot from being compared through the wrong decoder.
    let tryDecodeAccount (json: string) : Result<ObligationList, string> =
        try
            Decode.fromString (Decode.field "obligations" (Decode.list obligationDecoder)) json
        with error ->
            Error error.Message

/// Historical tagged TodoItem blob codec. New provider checkpoints use
/// MagicTodoObligationCodec; this remains for journal upgrade/recovery only.
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
