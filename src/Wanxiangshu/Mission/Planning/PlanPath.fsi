namespace Wanxiangshu.Mission.Planning

module PlanPath =
    val validateWorkId: workId: string -> Result<string, string>
    val canonicalWorkKey: workId: string -> Result<string, string>
    val resolvePlanPath: root: string -> workId: string -> Result<string, string>
    val readPlan: root: string -> workId: string -> Result<string, string>
