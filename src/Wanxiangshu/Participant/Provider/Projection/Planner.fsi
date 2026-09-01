namespace Wanxiangshu.Participant.Provider.Projection

[<RequireQualifiedAccess>]
module ProjectionPlanner =
    val plan: intents: ProjectionIntent list -> Result<ProjectionIntent list, ProjectionConflict>
