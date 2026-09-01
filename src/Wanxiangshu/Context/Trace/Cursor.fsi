namespace Wanxiangshu.Context.Trace

type XTraceCursor
type RecordCoverage
type XTraceRange

type XTraceOpeningEvidence =
    { AssignmentText: string
      AuthoritativeRequirements: string list
      ConstitutiveBody: string }

[<RequireQualifiedAccess>]
module XTraceCursor =
    val originCursor: XTraceCursor
    val create: sequence: int64 -> XTraceCursor
    val sequence: cursor: XTraceCursor -> int64
    val nextCursor: cursor: XTraceCursor -> XTraceCursor
    val isAfter: next: XTraceCursor -> previous: XTraceCursor -> bool
    val isAtOrAfter: cursor: XTraceCursor -> lowerBound: XTraceCursor -> bool
    val isBefore: cursor: XTraceCursor -> upperBound: XTraceCursor -> bool

[<RequireQualifiedAccess>]
module XTraceRange =
    val create: startInclusive: XTraceCursor -> endExclusive: XTraceCursor -> XTraceRange
    val startInclusive: range: XTraceRange -> XTraceCursor
    val endExclusive: range: XTraceRange -> XTraceCursor
    val contains: cursor: XTraceCursor -> range: XTraceRange -> bool
    val isEmpty: range: XTraceRange -> bool

[<RequireQualifiedAccess>]
module RecordCoverage =
    val create: ingestedThrough: XTraceCursor -> RecordCoverage
    val ingestedThrough: coverage: RecordCoverage -> XTraceCursor
    val covers: cursor: XTraceCursor -> coverage: RecordCoverage -> bool
