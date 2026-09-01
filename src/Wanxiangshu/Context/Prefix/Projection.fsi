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

    type RawPrefixMessageFacts =
        { ContainsTodoWrite: bool
          ToolCallIds: Set<ToolCallId> }

    val retainTodoWriteRounds: messages: RawPrefixMessageFacts list -> bool list

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
