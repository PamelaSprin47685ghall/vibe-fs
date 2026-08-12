namespace Wanxiangshu.Domain

/// Host/Application hand-off for one reconciled turn. `ClaimedButUnresolved`
/// means the abort belonged to assistance, but the continuation could not be
/// established; normal terminal ownership may run, while recovery/fallback must not.
[<RequireQualifiedAccess>]
type AssistanceTurnDisposition =
    | NotAssistance
    | Handled
    | ClaimedButUnresolved

/// AGENT-031 / PROMPT-018 provider-facing assistance continuations.
[<RequireQualifiedAccess>]
module AssistancePrompt =

    [<Literal>]
    let Sentinel = "[NEEDHELP]"

    /// The sentinel is control-plane syntax, never engineering evidence. XTrace
    /// capture removes the exact bytes from reasoning before persistence while
    /// preserving every surrounding byte.
    let stripSentinel (text: string) =
        if System.String.IsNullOrEmpty text then
            text
        else
            text.Replace(Sentinel, "")

    let escalation =
        SyntheticToml.document
            [ "你刚刚主动请求了协作。现在以同一角色的更强推理继续原来的 charge。"
              "这不是新的任务，也不是 provider failure；保持原有目标、权限与证据边界。"
              "先利用已有上下文突破刚才的困难，然后继续完成原任务。" ]
            []

    let consultationAssignment =
        "如何解决这个 agent 的当前困难？\n\n"
        + "请阅读下方 Commissioner 的工作记录，为它提供一个高质量的独立思考视角。\n"
        + "识别最值得突破的困难、可能遗漏的假设、可行的解法、关键判断依据，\n"
        + "以及最能帮助 Commissioner 快速继续原任务的下一步。\n\n"
        + "Commissioner 主动寻求协作是正常行为；不要把求助本身解释成失败。\n"
        + "不要接管 Commissioner 的任务，也不要扩大任务范围。\n"
        + "你的职责是帮助它更好、更快地继续。"

    let advice (childWorkRecord: string) =
        SyntheticToml.document
            [ "下面是一次独立 consultation 的 canonical child→parent LifecycleWorkRecord。"
              "把它当作第二视角，不是替代 assignment；结合你原来的 charge 与已验证证据继续工作。" ]
            [ SyntheticToml.field "consultation_record" (SyntheticToml.renderString childWorkRecord) ]

    let consultationFailed reason =
        SyntheticToml.document
            [ "独立 consultation 没有产出可用建议。继续你原来的 charge；这不是 provider failure，也不改变 fallback 状态。" ]
            [ SyntheticToml.field "consultation_failure" (SyntheticToml.renderString reason) ]
