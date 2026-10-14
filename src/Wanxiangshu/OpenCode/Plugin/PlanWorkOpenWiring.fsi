namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module PlanWorkOpenWiring =
    /// planning-020 entry wiring: opens WorkOpened + first IncumbencyOpened
    /// in the wanxiangli journal. Failure is diagnosed and stops the request;
    /// the legacy workspace EventStore is not a lifecycle dependency.
    val ensurePlanWorkOpenedForSession:
        workspaceDirectory: string option -> sid: string -> observedHead: int64 option -> Task<unit>
