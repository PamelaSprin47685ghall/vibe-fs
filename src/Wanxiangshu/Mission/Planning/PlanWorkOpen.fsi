namespace Wanxiangshu.Mission.Planning

open System.Threading.Tasks

module PlanWorkOpen =
    /// root is the plan root directory (<state-root>/wanxiangshu in
    /// production); sessionId is the plan session id, which becomes the workId.
    /// observedHead is the XTrace head observed at opening; without it the
    /// entry refuses with zero writes.
    val ensurePlanWorkOpened:
        gitCommonDir: string ->
        root: string ->
        sessionId: string ->
        observedHead: int64 option ->
            Task<Result<unit, string>>

    /// planning-020 / planning-010: the single runtime derivation of the
    /// fixed DevOps identity for a plan work. The entry binding and the
    /// single-flight ask binding must resolve the same devopsId for the same
    /// work, so the derivation lives here once.
    val internal deriveDevOpsId: workId: string -> string
