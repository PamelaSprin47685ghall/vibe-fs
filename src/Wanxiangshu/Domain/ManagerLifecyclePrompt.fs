namespace Wanxiangshu.Domain

/// GLORY-019/029 + SURFACE-004: Manager continuation prompt owner.
/// Prose meaning lives in `resources/provider/lifecycle/manager/**` (PROMPT-019).
module ManagerLifecyclePrompt =

    [<RequireQualifiedAccess>]
    module Path =
        /// GLORY-019 / legacy only: production path must not send (GLORY-018).
        [<Literal>]
        let WorkActivation = "lifecycle/manager/work-activation"

        /// GLORY-029 / §7.4.6: Pre-T1 idle encouragement.
        [<Literal>]
        let IdleEncouragementPreT1 = ManagerNarrative.Path.IdlePreT1

        /// GLORY-029 / §7.4.6: Post-T1 idle encouragement.
        [<Literal>]
        let IdleEncouragementPostT1 = ManagerNarrative.Path.IdlePostT1

        /// GLORY-057 / A.5.4: infrastructure-failure notice.
        [<Literal>]
        let FinalityUndecidable = "lifecycle/manager/finality-undecidable"
