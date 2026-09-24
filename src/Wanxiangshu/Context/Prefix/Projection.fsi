namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Foundation.Identity

type PrefixActivation =
    { SyntheticMessageId: string
      Memory: string
      CutoffExclusive: int }

[<RequireQualifiedAccess>]
type PrefixProjectionIntent =
    | Keep
    | Activate of PrefixActivation

[<RequireQualifiedAccess>]
type PrefixRendered =
    | Physical
    | Synthetic of PrefixActivation

[<RequireQualifiedAccess>]
module XPrefixProjection =
    val render: intent: PrefixProjectionIntent -> PrefixRendered

    /// context-compression-020 (revised): the caller owns retention — the real Opening
    /// and the last K phases. This record carries that decision; no tool name grants
    /// an unbounded exemption any more.
    type RawPrefixMessageFacts = { Retained: bool }

    val forSnapshot:
        snapshot: PrefixSnapshot option ->
        memoryPreamble: string ->
        frozenRecordPrefixBody: string ->
            PrefixProjectionIntent

    val forChoice:
        choice: XProjectionChoice ->
        committed: PrefixSnapshot option ->
        memoryPreamble: string ->
        frozenRecordPrefixBody: string ->
            PrefixProjectionIntent

    val requiredBlob: choice: XProjectionChoice -> committed: PrefixSnapshot option -> BlobRef option
