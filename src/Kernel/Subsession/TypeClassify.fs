module Wanxiangshu.Kernel.Subsession.TypeClassify

/// Cross-host, normalized set of message-part `type` string variants known
/// to represent a tool call. This is the union of every string literal that
/// Opencode/Omp/Mux have historically matched against in their own
/// `ExtractTurnObservation` implementations:
///   - "tool"          (Opencode)
///   - "dynamic-tool"   (Opencode)
///   - "toolCall"       (Opencode, Mux — camelCase)
///   - "tool_call"      (Opencode, Omp — snake_case)
/// Each host still extracts its own `partType` string from its own raw event
/// shape (that extraction cannot be shared — the JSON shapes differ), but the
/// final string-set membership test is centralized here so the three hosts
/// can never silently drift out of sync on which variants they recognize.
let toolCallPartTypes: Set<string> =
    Set.ofList [ "tool"; "dynamic-tool"; "toolCall"; "tool_call" ]

/// Cross-host, normalized set of message-part `type` string variants known
/// to represent a tool result. Union of every string literal previously
/// matched by Opencode/Omp's `ExtractTurnObservation` implementations:
///   - "tool-result"  (Opencode)
///   - "toolResult"   (Opencode)
///   - "tool_result"  (Opencode)
///   - "tool"         (Opencode)
let toolResultPartTypes: Set<string> =
    Set.ofList [ "tool-result"; "toolResult"; "tool_result"; "tool" ]

/// True when `partType` is one of the known cross-host tool-call part-type
/// string variants (see `toolCallPartTypes`).
let isToolCallPartType (partType: string) : bool = Set.contains partType toolCallPartTypes

/// True when `partType` is one of the known cross-host tool-result part-type
/// string variants (see `toolResultPartTypes`).
let isToolResultPartType (partType: string) : bool =
    Set.contains partType toolResultPartTypes
