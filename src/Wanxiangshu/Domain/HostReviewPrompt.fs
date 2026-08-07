namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel

/// GLORY-046 + A.6.1 + SURFACE-004: the Host-owned Reviewer opening assignment.
/// The Reviewer still sees real engineering semantics; the Manager never sees
/// this text.
module HostReviewPrompt =

    /// GLORY-046: the frozen opening assignment.
    let OpeningAssignment =
        "Review the current worktree against all authoritative user requirements.\n"
        + "Investigate correctness, completeness, regressions, tests, failure handling, and architectural constraints.\n"
        + "Record concrete evidence and required corrections as you work.\n"
        + "Submit the final decision with the verdict tool."
