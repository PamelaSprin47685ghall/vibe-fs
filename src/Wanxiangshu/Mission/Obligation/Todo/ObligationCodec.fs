namespace Wanxiangshu.Mission.Obligation.Todo

open Thoth.Json
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo

/// Sole durable/provider JSON codec for the GrandRewrite obligation account.
/// Blob bodies are `[{name,horizon,work}]`; provider calls are
/// `{planComplete:bool,workingOn:string,obligations:[{name,horizon,work}]}`. No legacy
/// id/status/progress vocabulary is accepted here.
module MagicTodoObligationCodec =

    let private horizonDecoder: Decoder<ObligationHorizon> =
        Decode.string
        |> Decode.andThen (fun value ->
            match ObligationHorizon.tryParse value with
            | Some horizon -> Decode.succeed horizon
            | None -> Decode.fail ("unknown obligation horizon: " + value))

    /// v4 durable bytes had no horizon. They represented one uniform flat list,
    /// so migration conservatively maps every historical item to Near rather than
    /// inventing distance that was never recorded.
    let private obligationDecoder: Decoder<Obligation> =
        Decode.object (fun get ->
            { Name = get.Required.Field "name" Decode.string
              Horizon =
                get.Optional.Field "horizon" horizonDecoder
                |> Option.defaultValue ObligationHorizon.Near
              Work = get.Required.Field "work" Decode.string })

    let encode (items: ObligationList) : string =
        MagicTodo.canonicalObligationListWire items

    let tryDecode (json: string) : Result<ObligationList, string> =
        try
            Decode.fromString (Decode.list obligationDecoder) json
        with error ->
            Error error.Message

    let private normalizeInput (input: TodoWriteInput) =
        match MagicTodo.validateTodoWriteInput input with
        | Ok normalized -> Ok normalized
        | Error _ -> Error "todowrite input failed semantic validation"

    /// Decode the provider-facing call object explicitly; snapshot locality binds
    /// the raw commitment declaration, focus pointer, and obligation account together.
    let tryDecodeInput (json: string) : Result<TodoWriteInput, string> =
        try
            Decode.fromString
                (Decode.object (fun get ->
                    { PlanComplete = get.Required.Field "planComplete" Decode.bool
                      WorkingOn = get.Required.Field "workingOn" Decode.string
                      Obligations = get.Required.Field "obligations" (Decode.list obligationDecoder) }))
                json
            |> Result.bind normalizeInput
        with error ->
            Error error.Message
