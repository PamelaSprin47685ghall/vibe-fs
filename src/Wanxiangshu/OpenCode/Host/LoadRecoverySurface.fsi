namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks

/// Pure production fold and load-decision observations; no process restart or append is simulated.
module LoadRecoverySurface =
    val create: unit -> obj
    val foldCanonical: state: obj -> factJson: string -> obj
    val childSettlements: state: obj -> string array
    val childView: state: obj -> parent: string -> child: string -> obj
    val lookupChild: state: obj -> parent: string -> key: string -> byName: bool -> obj
    val bindingEvidence: state: obj -> child: string -> obj
    val installResolvers: state: obj -> unit
    val clearResolvers: unit -> unit
    val lane: session: string -> obj
    val clearLane: session: string -> unit
    val staleBloggerRequests: state: obj -> liveFlight: (string -> string -> bool) -> obj array
    val drainCompletions: state: obj -> parent: string -> maxCount: int -> completedAt: string -> Task<obj>
