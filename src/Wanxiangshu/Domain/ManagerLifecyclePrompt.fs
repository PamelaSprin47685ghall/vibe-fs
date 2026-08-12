namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel

/// GLORY-019/029 + SURFACE-004: Manager continuation prompt owner.
module ManagerLifecyclePrompt =

    /// GLORY-019 / legacy only: production path must not send (GLORY-018).
    let WorkActivation =
        SyntheticToml.document
            [ "Now complete it yourself."
              "Carry out the work you described until the final goal is fully achieved."
              ""
              "Planning is not completion."
              "Delegation is not completion."
              "A child finishing is not completion."
              "A successful command is not completion while meaningful uncertainty remains."
              "An explanation of the work is not the work itself."
              "A partial implementation is not completion merely because the remaining work is difficult."
              "As long as any useful action remains, continue." ]
            []

    /// GLORY-029 / §7.4.6: Pre-T1 idle encouragement.
    let IdleEncouragementPreT1 = ManagerNarrative.preT1IdleDocument

    /// GLORY-029 / §7.4.6: Post-T1 idle encouragement.
    let IdleEncouragementPostT1 = ManagerNarrative.postT1IdleDocument

    /// GLORY-057 / A.5.4: infrastructure-failure notice.
    let FinalityUndecidable =
        SyntheticToml.document
            [ "Your ending could not be decided."
              "You still have time. Continue, and seek your end again when you are ready." ]
            []
