namespace Wanxiangshu.Context.Companion.Blogger

/// Context-compression delta owner. Semantic message parts enter as plain
/// objects; the BloggerDelta cursor/chunker and Fable unions stay private.
[<RequireQualifiedAccess>]
module BloggerDeltaSurface =

    /// Plain constructors used by semantic tests and other JS callers.
    val textPart: value: string -> obj
    val reasoningPart: value: string -> obj
    val toolCallPart: tool: string -> args: string -> obj
    val toolResultPart: value: string -> obj
    val mediaPart: mediaType: obj -> digest: string -> obj

    val text: value: string -> obj
    val reasoning: value: string -> obj
    val toolCall: tool: string -> args: string -> obj
    val toolResult: value: string -> obj
    val media: mediaType: obj -> digest: string -> obj
    val byteCount: value: string -> int
    val messages: values: obj array -> obj

    val cursor: turn: int -> part: int -> obj

    val limitBytes: int

    /// Render the next deterministic chunk. `None` remains an absent option at
    /// the JS boundary; a present chunk is a plain semantic summary.
    val nextChunk: request: obj -> obj option
