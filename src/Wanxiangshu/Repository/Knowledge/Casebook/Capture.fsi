namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks

/// Access tracker for substantive file access collection.
type AccessTracker =
    new: unit -> AccessTracker
    member recordRead: path: string * contentHash: string -> unit
    member recordCreate: path: string -> unit
    member recordEdit: path: string -> unit
    member recordDelete: path: string -> unit
    member recordMove: source: string * destination: string -> unit
    member recordGrep: pattern: string * path: string -> unit
    member recordGlob: pattern: string -> unit
    member recordAttemptedMutation: path: string * committed: bool -> unit
    member getRelatedPaths: unit -> string array
    member RecordRead: path: string * contentHash: string -> unit
    member RecordCreate: path: string -> unit
    member RecordEdit: path: string -> unit
    member RecordDelete: path: string -> unit
    member RecordMove: source: string * destination: string -> unit
    member RecordGrep: pattern: string * path: string -> unit
    member RecordGlob: pattern: string -> unit
    member RecordAttemptedMutation: path: string * committed: bool -> unit
    member GetRelatedPaths: unit -> string list

/// CASE-003 / KR-003 / KR-014: typed observation capture and substantive access.
module CasebookCapture =

    /// Stable content fingerprint.
    val contentHash: text: string -> string

    val ofReadExecution: args: obj -> output: string -> Observation option
    val ofGlobExecution: args: obj -> output: string -> Observation option
    val ofGrepExecution: args: obj -> output: string -> Observation option

    /// Dispatch by tool name.
    val capture: toolName: string -> args: obj -> output: string -> Observation option

    val ofExecCommand: command: string -> Observation option

    /// KR-003 / KR-014: Check if tool is substantive.
    val isSubstantiveTool: toolName: string -> bool

    /// Create an access tracker.
    val createAccessTracker: unit -> AccessTracker
    val baselineFromObservations: observations: Observation list -> relatedPaths: string list -> obj

    /// Record substantive access onto a tracker.
    val recordSubstantiveAccess: tracker: AccessTracker -> toolName: string -> args: obj -> committed: bool -> unit

    /// KR-010: Merge fission lane substantive accesses.
    val mergeFissionSubstantiveAccess: preFission: string list -> laneAccesses: string list list -> string list

    /// KR-010: Scoped case identity for an invocation.
    val caseIdentityForInvocation: sessionId: string -> invocationId: string -> string

    /// KR-014: Budget truncation for large diffs.
    val truncateDiffForBudget: diff: string -> budget: int -> obj

    /// KR-004: Freeze completion file state baseline.
    val freezeCompletionState: workspaceRoot: string -> paths: string list -> Task<obj>

    /// KR-004 / KR-005: Compute diff between baseline and current workspace.
    val computeMaintenanceDiff: workspaceRoot: string -> baseline: obj -> Task<obj>
