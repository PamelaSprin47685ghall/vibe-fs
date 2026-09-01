namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.Identity

module LoopEventCodec =
    type TextDelta =
        { SessionId: SessionId
          MessageId: string option
          PartId: string option
          Field: string option
          Delta: string }

    val isLoopTextDelta: rawInput: obj -> bool
    val tryDecodeTextDelta: rawInput: obj -> TextDelta option
