namespace Wanxiangshu.Next.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity

/// Dispatcher correlation metadata carried on the Host prompt boundary.
///
/// PROMPT-011: the PromptKey must reach Host metadata, because it is the only
/// anchor through which an unresolved claim can be reconciled after a crash.
/// The other fields are diagnostic.
///
/// Metadata is deliberately absent from `Projection.decodePart`, so none of this
/// enters either provider projection (COMPANION-012).
module PromptMetadataCodec =

    [<Literal>]
    let PromptKeyField = "wanxiangshu_prompt_key"

    [<Literal>]
    let OriginField = "wanxiangshu_origin"

    [<Literal>]
    let LogicalRunField = "wanxiangshu_logical_run"

    let create (key: PromptKey) (origin: string) (logicalRunId: LogicalRunId option) : obj =
        createObj
            [ PromptKeyField, box (PromptKey.value key)
              OriginField, box origin
              // An Authority Root claim has no run yet: the run id derives from a
              // physical message the Host has not created. Emitting "" would make
              // "no run" and "a run named empty" the same value on the wire.
              LogicalRunField,
              box (
                  match logicalRunId with
                  | Some runId -> LogicalRunId.value runId
                  | None -> null
              ) ]
