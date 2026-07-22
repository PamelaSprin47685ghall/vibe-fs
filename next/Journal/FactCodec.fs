namespace Wanxiangshu.Next.Journal

open Thoth.Json
open Wanxiangshu.Next.Kernel.Fact

module FactCodec =

    let private extra = Extra.empty |> Extra.withInt64

    let serializeFact (fact: Fact) : string =
        Encode.Auto.toString (0, fact, extra = extra)

    let deserializeFact (json: string) : Result<Fact, string> =
        match Decode.Auto.fromString<Fact> (json, extra = extra) with
        | Ok f -> Ok f
        | Error err -> Error err
