namespace Wanxiangshu.Context.Trace

/// COMPANION-003 / HOST-005: X 的唯一原始语义轨迹。
///
/// `XTraceCursor` 在 X 生命周期内严格单调，独立于 Host transcript 的临时数组
/// 下标与 semantic turn 编号——Host compaction 会作废后者（HOST-006 重锚），
/// 但 XTrace 与 RecordCoverage 是 durable lifecycle facts，不得随 compaction
/// 清零或重读（COMPANION-008）。因此 cursor 是自增序列，不是 turn/part 坐标。
type XTraceCursor = { Sequence: int64 }

/// COMPANION-003: Y 已消化到哪（决定 LWR gap 起点）。可落在 turn 中间。
type RecordCoverage = { IngestedThrough: XTraceCursor }

[<RequireQualifiedAccess>]
module XTraceCursor =
    let originCursor = { Sequence = 0L }

    let nextCursor (cursor: XTraceCursor) : XTraceCursor = { Sequence = cursor.Sequence + 1L }

    /// 严格单调比较。同 cursor 重复 append 是 PERSIST-010 的拒绝条件。
    let isAfter (next: XTraceCursor) (previous: XTraceCursor) = next.Sequence > previous.Sequence
