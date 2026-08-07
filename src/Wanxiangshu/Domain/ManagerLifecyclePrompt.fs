namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel

/// GLORY-019/029 + SURFACE-004: the Manager continuation prompt owner.
/// Activation, ordinary idle encouragement, and the infrastructure-failure
/// notice all live here; the Finality rejection prompt has its own owner
/// (`FinalityPrompt`) because it renders a dynamic work record.
module ManagerLifecyclePrompt =

    /// GLORY-019 / A.5.1: the exact Activation text.
    let WorkActivation =
        "Now complete it yourself.\nCarry out the work you described until the final goal is fully achieved.\n\nPlanning is not completion.\nDelegation is not completion.\nA child finishing is not completion.\nA successful command is not completion while meaningful uncertainty remains.\nAn explanation of the work is not the work itself.\nA partial implementation is not completion merely because the remaining work is difficult.\nAs long as any useful action remains, continue."

    /// GLORY-029 / A.5.2: the exact ordinary idle encouragement text.
    let IdleEncouragement =
        "You are doing well.\nYou have plenty of time.\nYou can continue.\nWhen nothing useful remains, call suicide."

    /// GLORY-057 / A.5.4: the infrastructure-failure notice. No fabricated work
    /// record may ever be attached (GLORY-056).
    let FinalityUndecidable =
        SyntheticToml.document
            [ "Your ending could not be decided."
              "You still have time. Continue, and seek your end again when you are ready." ]
            []
