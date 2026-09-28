namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks

/// Production fold and load decisions, with controlled external ports for failure cases.
module LoadRecoverySurface =
    val create: unit -> obj
    val foldCanonical: state: obj -> factJson: string -> obj
    val childSettlements: state: obj -> string array
    val rejectChildSettlementAppends: state: obj -> unknown: bool -> Task<obj>
    val childView: state: obj -> parent: string -> child: string -> obj
    val settleChatTerminal: state: obj -> factJson: string -> obj
    val lookupChild: state: obj -> parent: string -> key: string -> byName: bool -> obj
    val bindingEvidence: state: obj -> child: string -> obj
    val installResolvers: state: obj -> unit
    val clearResolvers: unit -> unit
    val lane: session: string -> obj
    val clearLane: session: string -> unit
    val staleBloggerRequests: state: obj -> liveFlight: (string -> string -> bool) -> obj array
    val drainCompletions: state: obj -> parent: string -> maxCount: int -> completedAt: string -> Task<obj>
