namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel

/// GLORY-019/029 + SURFACE-004: the Manager continuation prompt owner.
/// Activation, ordinary idle encouragement, and the infrastructure-failure
/// notice all live here; the Finality rejection prompt has its own owner
/// (`FinalityPrompt`) because it renders a dynamic work record.
module ManagerLifecyclePrompt =

    /// GLORY-019 / A.5.1: Activation as instruction-only synthetic surface.
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

    /// GLORY-029 / A.5.2: ordinary idle encouragement as instruction-only synthetic surface.
    let IdleEncouragement =
        SyntheticToml.document
            [ "You are doing well."
              "You have plenty of time."
              "You can continue."
              "When nothing useful remains, call suicide." ]
            []

    /// GLORY-057 / A.5.4: the infrastructure-failure notice. No fabricated work
    /// record may ever be attached (GLORY-056).
    let FinalityUndecidable =
        SyntheticToml.document
            [ "Your ending could not be decided."
              "You still have time. Continue, and seek your end again when you are ready." ]
            []
