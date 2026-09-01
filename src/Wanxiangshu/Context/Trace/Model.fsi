namespace Wanxiangshu.Context.Trace

open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

type XTraceItem =
    { Cursor: XTraceCursor
      Provenance: string
      Role: string
      Part: SemanticPart }

[<RequireQualifiedAccess>]
module XTrace =
    val sliceBetween: start: XTraceCursor -> endExclusive: XTraceCursor -> items: XTraceItem list -> XTraceItem list
    val sliceFrom: start: XTraceCursor -> items: XTraceItem list -> XTraceItem list
    val head: items: XTraceItem list -> XTraceCursor
    val flatten: messages: SemanticMessage list -> {| Part: SemanticPart; Role: string |} list
    val isWorkRecordPart: part: SemanticPart -> bool
    val forWorkRecord: items: XTraceItem list -> XTraceItem list
    val forOpening: items: XTraceItem list -> XTraceItem list
    val renderItem: item: XTraceItem -> string
    val render: items: XTraceItem list -> string
