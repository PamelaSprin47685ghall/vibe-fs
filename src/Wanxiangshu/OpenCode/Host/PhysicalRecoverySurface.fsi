namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

/// Settle orphaned child work only after its real PTY resources have drained.
module PhysicalRecoverySurface =
    val settleChildRuns: handle: JournalHandle -> commonDir: string -> Task<unit>
