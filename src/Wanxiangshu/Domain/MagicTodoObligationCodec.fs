namespace Wanxiangshu.Domain

open Thoth.Json
open Wanxiangshu.Domain.MagicTodo

/// Sole durable/provider JSON codec for the GrandRewrite obligation account.
/// Blob bodies are `[{name,work}]`; provider calls are
/// `{planComplete:bool,obligations:[{name,work}]}`. No legacy id/status/progress vocabulary is
/// accepted here.
module MagicTodoObligationCodec =

    let private obligationDecoder: Decoder<Obligation> =
        Decode.object (fun get ->
            { Name = get.Required.Field "name" Decode.string
              Work = get.Required.Field "work" Decode.string })

    let encode (items: ObligationList) : string =
        MagicTodo.canonicalObligationListWire items

    let tryDecode (json: string) : Result<ObligationList, string> =
        try
            Decode.fromString (Decode.list obligationDecoder) json
        with error ->
            Error error.Message

    /// Decode the provider-facing call object explicitly; snapshot locality binds
    /// the raw commitment declaration and the obligation account together.
    let tryDecodeInput (json: string) : Result<TodoWriteInput, string> =
        try
            Decode.fromString
                (Decode.object (fun get ->
                    { PlanComplete = get.Required.Field "planComplete" Decode.bool
                      Obligations = get.Required.Field "obligations" (Decode.list obligationDecoder) }))
                json
        with error ->
            Error error.Message
