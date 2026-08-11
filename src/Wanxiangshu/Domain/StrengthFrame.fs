namespace Wanxiangshu.Domain

open System
open Wanxiangshu.Kernel.Identity

/// STRENGTH-005: a real, completed readonly tool exchange. Absence of either
/// half is unrepresentable once this value exists; Host collection builds it only
/// after observing both call and result.
type StrengthToolExchange =
    { ToolName: string
      CanonicalArguments: string
      CanonicalResult: string }

/// One Replica provider request. RequestOrdinal, not tool count, spends K.
type StrengthRequestBatch =
    { RequestOrdinal: int
      Exchanges: StrengthToolExchange list }

/// Cross-session semantic material. It deliberately contains no Replica call id,
/// timestamp, model id, provenance marker or other wire-local identity.
type StrengthFrameBundle =
    { Batches: StrengthRequestBatch list
      Digest: string
      ByteLength: int }

[<RequireQualifiedAccess>]
type StrengthFrameError =
    | EmptyBundle
    | EmptyBatch of requestOrdinal: int
    | InvalidRequestOrdinal of expected: int * actual: int
    | UnsupportedTool of toolName: string
    | ByteLimitExceeded of actualBytes: int * maxBytes: int

module StrengthFrame =

    let private allowedTools = set [ "read"; "glob"; "grep" ]

    let isAllowedTool (toolName: string) =
        not (String.IsNullOrWhiteSpace toolName)
        && Set.contains (toolName.Trim().ToLowerInvariant()) allowedTools

    /// Fable-compatible UTF-8 length. .NET Encoding.GetByteCount is not
    /// available in Fable, while UTF-16 String.Length would undercharge non-ASCII
    /// payloads and make the hard byte fuse platform-dependent.
    let utf8ByteCount (value: string) =
        let text = if isNull value then "" else value

        let rec loop index total =
            if index >= text.Length then
                total
            else
                let code = int text.[index]

                if code <= 0x7F then
                    loop (index + 1) (total + 1)
                elif code <= 0x7FF then
                    loop (index + 1) (total + 2)
                elif code >= 0xD800 && code <= 0xDBFF && index + 1 < text.Length then
                    let next = int text.[index + 1]

                    if next >= 0xDC00 && next <= 0xDFFF then
                        loop (index + 2) (total + 4)
                    else
                        loop (index + 1) (total + 3)
                else
                    loop (index + 1) (total + 3)

        loop 0 0

    let private canonicalField (value: string) =
        let text = if isNull value then "" else value
        sprintf "%d:%s" (utf8ByteCount text) text

    let private canonicalExchange (exchangeOrdinal: int) (exchange: StrengthToolExchange) =
        String.concat
            "\u001f"
            [ string exchangeOrdinal
              canonicalField (exchange.ToolName.Trim().ToLowerInvariant())
              canonicalField exchange.CanonicalArguments
              canonicalField exchange.CanonicalResult ]

    let private canonicalBatch (batch: StrengthRequestBatch) =
        let exchanges =
            batch.Exchanges
            |> List.mapi (fun index exchange -> canonicalExchange (index + 1) exchange)
            |> String.concat "\u001e"

        String.concat "\u001d" [ string batch.RequestOrdinal; exchanges ]

    let canonicalText (batches: StrengthRequestBatch list) =
        batches |> List.map canonicalBatch |> String.concat "\u001c"

    let tryBuild
        (sha256: string -> string)
        (maxBytes: int)
        (batches: StrengthRequestBatch list)
        : Result<StrengthFrameBundle, StrengthFrameError> =
        let rec validateBatches expected remaining =
            match remaining with
            | [] -> Ok()
            | batch :: tail when batch.RequestOrdinal <> expected ->
                Error(StrengthFrameError.InvalidRequestOrdinal(expected, batch.RequestOrdinal))
            | batch :: _ when List.isEmpty batch.Exchanges -> Error(StrengthFrameError.EmptyBatch batch.RequestOrdinal)
            | batch :: tail ->
                match
                    batch.Exchanges
                    |> List.tryFind (fun exchange -> not (isAllowedTool exchange.ToolName))
                with
                | Some invalid -> Error(StrengthFrameError.UnsupportedTool invalid.ToolName)
                | None -> validateBatches (expected + 1) tail

        match batches with
        | [] -> Error StrengthFrameError.EmptyBundle
        | _ ->
            match validateBatches 1 batches with
            | Error error -> Error error
            | Ok() ->
                let canonical = canonicalText batches
                let bytes = utf8ByteCount canonical

                if maxBytes < 0 || bytes > maxBytes then
                    Error(StrengthFrameError.ByteLimitExceeded(bytes, maxBytes))
                else
                    Ok
                        { Batches = batches
                          Digest = sha256 canonical
                          ByteLength = bytes }

    /// Host-only synthetic message identity for one rendered half of a provider
    /// request batch. This identity is stable across replay/restart and is used by
    /// XTrace provenance to recover the exact Traced cursor range. It never enters
    /// ProviderSemanticProjection or provider-visible content.
    let hostMessageId
        (sha256: string -> string)
        (ownerSessionId: SessionId)
        (decisionId: StrengthDecisionId)
        (requestOrdinal: int)
        (half: string)
        (semanticDigest: string)
        : string =
        String.concat
            "\u001f"
            [ SessionId.value ownerSessionId
              StrengthDecisionId.value decisionId
              string requestOrdinal
              half
              semanticDigest ]
        |> sha256

    /// Cross-session wire identity is derived only from frozen owner/decision and
    /// semantic coordinates. No mechanism/provenance label enters provider bytes.
    let wireToolCallId
        (sha256: string -> string)
        (ownerSessionId: SessionId)
        (decisionId: StrengthDecisionId)
        (requestOrdinal: int)
        (exchangeOrdinal: int)
        (semanticDigest: string)
        : string =
        String.concat
            "\u001f"
            [ SessionId.value ownerSessionId
              StrengthDecisionId.value decisionId
              string requestOrdinal
              string exchangeOrdinal
              semanticDigest ]
        |> sha256
