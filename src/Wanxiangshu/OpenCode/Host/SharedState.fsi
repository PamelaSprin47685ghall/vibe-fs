namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime

/// HOST-012: cross-instance shared state — module-level singletons that all
/// plugin instances (root + worktree) read and write through the same
/// reference.
module SharedState =

    /// Cross-instance session parent map.
    val SessionParents: Dictionary<string, string>

    /// Judged physical review request identities across plugin instances.
    val VerdictSubmissions: HashSet<string>

    /// Cross-instance session directory map.
    val SessionDirectories: Dictionary<string, string>

    /// Gate object for review guard nudge reservations.
    val ReviewGuardNudgeGate: obj

    /// Cross-instance review guard nudge reservation set.
    val ReviewGuardNudges: HashSet<string>

    /// Unit-test isolation only: production must not wipe cross-instance reservations.
    val clearReviewGuardNudgesForTests: unit -> unit

    /// The ROOT workspace, set by whichever plugin instance boots first.
    val mutable RootWorkspace: string option

    /// Gate object for blogger flight ownership.
    val BloggerFlightGate: obj

    /// Cross-instance blogger flight ownership registry.
    val BloggerFlights: Dictionary<string, BloggerRequestContext>

    /// Cross-instance per-Blogger materialization admission.
    val BloggerMaterializationAdmission: BloggerMaterializationAdmission

    /// Unit-test isolation only: production Dispose must not wipe cross-instance flights.
    val clearBloggerFlightsForTests: unit -> unit
