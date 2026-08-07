namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel

/// GLORY-019/029 + SURFACE-004: the Manager continuation prompt owner.
/// Activation, ordinary idle encouragement, and the infrastructure-failure
/// notice all live here; the Finality rejection prompt has its own owner
/// (`FinalityPrompt`) because it renders a dynamic work record.
module ManagerLifecyclePrompt =

    /// GLORY-019 / A.5.1: the exact Activation text.
    let WorkActivation =
        "Now complete it yourself.\nCarry out the work you described until the final goal is fully achieved."

    /// GLORY-029 / A.5.2: the exact ordinary idle encouragement text.
    let IdleEncouragement =
        "You are doing well.\nYou have plenty of time.\nYou can continue.\nWhen nothing useful remains, call suicide."

    /// GLORY-057 / A.5.4: the infrastructure-failure notice. No fabricated work
    /// record may ever be attached (GLORY-056).
    let FinalityUndecidable =
        "Your ending could not be decided.\nYou still have time. Continue, and seek your end again when you are ready."
