namespace Wanxiangshu.Mission.Manager.Life

module ManagerLifecyclePrompt =
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val WorkActivation: string = "lifecycle/manager/work-activation"

        [<Literal>]
        val IdleEncouragementPreT1: string = "lifecycle/manager/idle-pre-t1"

        [<Literal>]
        val IdleEncouragementPostT1: string = "lifecycle/manager/idle-post-t1"
