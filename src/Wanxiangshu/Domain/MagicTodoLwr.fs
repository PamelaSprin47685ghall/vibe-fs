namespace Wanxiangshu.Domain

/// Exclusive XTrace range for one LWR materialization (COMPANION-015 / EXEC-031).
module MagicTodoLwr =

    /// Inclusive start, exclusive end on XTrace for one invocation / request.
    type BoundedRange =
        {
            /// Inclusive start cursor (often WorkRecordStart / invocation send head).
            StartInclusive: XTraceCursor
            /// Exclusive end frontier (ReviewFrontier / invocation completion head).
            EndExclusive: XTraceCursor
        }
