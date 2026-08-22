# review-judgement — WHAT

## REVIEW-JUDGEMENT-001: judge 工具是 typed judgment surface

Reviewer 的判断接口是 `judge(verdict)` 工具。`verdict` 为强类型枚举，仅接受 `PERFECT` 与 `REVISE` 两个值；工具不接受任何额外描述字段，成功回执绝不 echo verdict 本身。已废弃工具名（如 `verdict`）非法且不设别名。Verdict 是模型自主创作的强类型裁决动作，而非 Host 回声的系统状态镜像。

## REVIEW-JUDGEMENT-002: acceptance 与 rejection 都必须由 discrimination 挣得

Judgement 的目标是建立具有区分力（discrimination）的判断。Acceptance 必须凭事实挣得，Rejection 也必须凭证据挣得。拒绝并非严谨的姿态表演，拒绝必须购买实质价值；接受代表按比例调查后未见足以扣留的材料，不代表全知。匹配与发现是观察（observation），缺陷认定属于判断（judgement），严禁以个人偏好冒充缺陷。

## REVIEW-JUDGEMENT-003: 判断相对 root requirement 与当前被审对象而非 mood

判断必须严格针对真实存在的工作、真实存在的 obligation 以及真实存在的 evidence。`PERFECT` 与 `REVISE` 是 verdict 的 wire literal，不是审查者的情绪或严苛度量尺。审查的特定关注视角（lens）可以收窄视线，但绝不得收窄责任边界，亦不得抹消属于原始请求的义务。

## REVIEW-JUDGEMENT-004: material defect 才能 withhold acceptance

唯有实质性缺陷（material defect）或未履行的实质义务才构成 REVISE 的合法理由。`PERFECT` 裁决允许与真实的非阻断工艺观察（non-blocking workmanship）共存：轻微观察进入文本记录并不撤销已挣得的 acceptance。同时，非阻断并不意味着不必做，严禁因 verdict 为 PERFECT 而刻意隐瞒真实观察。判断依据必须追溯后果，严禁「小改动自动重要」或「微小笔误自动 REVISE」。

## REVIEW-JUDGEMENT-005: PERFECT 不等同全知，REVISE 必须购买实质改进

`PERFECT` 表示当前未发现足以正当扣留 acceptance 的实质缺陷，不代表字面上的毫无瑕疵，亦不代表穷尽所有未来失败可能。`REVISE` 表示 acceptance 因当前工作缺少关键产出或存在阻断性缺陷而被扣留；要求修复必须能够购买实质上更好或更真实的结果，严禁免费否决。

## REVIEW-JUDGEMENT-006: evidence、inference、preference 与 defect 的认知地位分离

工作记录、测试输出、构建状态、diff 与源码均为证据（evidence），单独任何一项均不构成 judgement。证据强度必须与主张（claim）相称，强硬主张需要强硬支撑。当已有证据无法消除关键不确定性时，必须在 judgement 中诚实保留该不确定性，严禁通过修辞手法将其粉饰为确定的 PERFECT 或 REVISE。

## REVIEW-JUDGEMENT-007: Examiner's Ledger 是判断方向而非 checklist

Reviewer 依据 Examiner's Ledger 的判断方向建立审查视角，仅在存在实质问题时发言。Ledger 与 Rulebook 是思维引导方向，不是逐项打勾的 checklist，亦非固定 schema：禁止将各维度固化为必填表格字段、Pass 打分表或固定标题模板。报告形式为诚实陈述的自然语言文本，不设固定 DTO 骨架。

## REVIEW-JUDGEMENT-008: 过程评审一次 durable judge 即 terminal

TodoProcessReview（过程评审）是针对当前 checkpoint 的单次真实判断，单次持久化 `judge` 即为 terminal。防重机制绑定于当前物理请求，同一物理请求内的重复调用予以收束，不同请求复用同一 dedicated 会话时重新具备单次 `judge` 资格。过程评审成功回执文本保持不变并明确指示结束；当 Host 准备把该 terminal tool result 送入同一物理请求的下一次 LLM continuation 时，provider transform 必须先确认该 judgement 对应的 exact `tool_result` 已进入 durable XTrace，并以该 part 的 `cursor+1` 持久化 `ReviewAttemptClosed`。record-ready 的物理排序以 durable Chronicle 为准：若观察点已经存在 canonical Chronicle，则不得再等待同一 terminal transform 后续才会触发的 Blogger continuation；只有 Reviewer 已链接 Blogger、尚无任何 Chronicle 且存在 durable-open producer 时，才以 journal change 驱动等待该 producer commit/abandon，从而保证首次 record capture 不被 terminal interrupt 饿死。随后 transform 只能把物理 interrupt 交给 runtime-owned background executor 并立即正常返回，严禁在 `messages.transform` 内同步等待 Host abort——abort 的完成本身依赖 transform 返回，会形成自锁。interrupt 由首次成功提交建立的 request-scoped 标记触发，不得等待第二次 `judge` 才触发，也不得把 durable closure 留给已被 interrupt 截断的通用 turn-completion observer。Finality 与 Change 的 pre/post-rebase review 必须共享同一个 Host future-terminal 解释器；clean-abort authority 由强类型 `(ReviewerSessionId, ReviewBarrierId)` occasion 携带，并且只有该**当前 barrier**已经具有 durable exact `ReviewAttemptClosed` 时，受管 `InterruptAttempt` 随后的 Abort 才可解释为 clean terminal。旧 barrier closure、无 closure Abort 与真实 failure 均必须 fail closed。崩溃或旧版本已经留下 `VerdictKnown` 但未 closure 时，只允许从同一 `(ProviderRun, ToolCallId)` 的 durable `tool_result` 恢复同一排他 frontier，严禁拿当前 session head 扩大审查记录。Finality 首次 PERFECT 严禁复用过程评审的 terminal received 回执，必须返回 skeptical challenge 并要求再次评估；只有 terminal judgement 被标记后才适用 transform interrupt。无实质文本记录的过程 PERFECT 无效。

## REVIEW-JUDGEMENT-009: 拒绝必须把伤口说清且不发明 obligation

当 Reviewer 做出 REVISE 裁决时，必须将具体缺陷（伤口）阐述清晰，使后续修复能够明确换取更优结果。严禁为了显得审慎而凭空发明真实 obligation 中并不存在的假设性风险、多余测试或边界要求。

## REVIEW-JUDGEMENT-010: 不得奖励自信、惩罚不熟悉或因口味拒绝

Judgement 不得因作者自信的修辞而予以宽容，不得因实现方式不熟悉而惩罚，亦不得仅因「我会写得不同」而拒绝，或因「表面光鲜」而接受。新颖性不是缺陷，风格偏好不会仅因可被描述就升级为实质缺陷。
