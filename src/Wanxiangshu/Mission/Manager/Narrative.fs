namespace Wanxiangshu.Mission.Manager

open System
open Wanxiangshu.Foundation

/// Magic Todo T1 entrustment revelation renderer.
///
/// The old Manager-Life birth/reawakening/idle narrative lived here as well;
/// those lifecycle surfaces no longer exist. This module now owns only the
/// T1 acceptance text still consumed by the obligation ledger.
module ManagerNarrative =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let T1Revelation = "lifecycle/manager/t1-revelation"

    /// TODO-015 / GLORY-074: canonical T1 tool result = entrustment revelation + enriched todo body.
    let wrapT1AcceptedResult (t1RevelationInstructions: string list) (todoWriteResult: string) =
        let normalized = LlmFacing.normalizeNewlines todoWriteResult

        if String.IsNullOrWhiteSpace normalized then
            LlmFacing.renderInstructions t1RevelationInstructions
        else
            LlmFacing.renderInstructions (t1RevelationInstructions @ [ normalized ])
