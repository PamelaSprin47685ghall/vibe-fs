module Wanxiangshu.Kernel.EventLog.ReviewVerdictWire

/// Wire-format review verdict values in NDJSON / front-matter (SSOT for end-verdict tests).
[<RequireQualifiedAccess>]
type ReviewWireVerdict =
    | Accepted
    | Cancelled
    | NeedsRevision
    | Terminated

let accepted = "accepted"
let cancelled = "cancelled"
let needsRevision = "needs_revision"
let terminated = "terminated"

let tryParse (verdict: string) : ReviewWireVerdict option =
    match verdict with
    | v when v = accepted -> Some ReviewWireVerdict.Accepted
    | v when v = cancelled -> Some ReviewWireVerdict.Cancelled
    | v when v = needsRevision -> Some ReviewWireVerdict.NeedsRevision
    | v when v = terminated -> Some ReviewWireVerdict.Terminated
    | _ -> None

let isEndVerdict (verdict: string) : bool =
    match tryParse verdict with
    | Some ReviewWireVerdict.Accepted
    | Some ReviewWireVerdict.Cancelled -> true
    | _ -> false
