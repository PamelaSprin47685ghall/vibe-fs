# WHY — review-judgement

## 不可替代的存在理由

万象术的质量门是 **Reviewer 对工作的判断**，最终汇入 mission 的终局（Finality）与过程节拍（TodoProcessReview）。这条链上最容易坏的环节不是「确认是否发生」（那是 `review-assurance` 的因果证明问题），而是**「判断到底意味着什么」**：

> 一个 reviewer 输出了 `PERFECT` 或 `REVISE`。这两个词在被系统消费之前，必须已经有不可替代的意义。

如果判断语义缺席，下列退化就会发生：

- **表演式拒绝**：reviewer 认为「拒绝越多 = 越谨慎」，把无关痛感抬成 withhold。系统里的 REVISE 不再是「工作尚未挣得 acceptance」的信号，而是「reviewer 在摆姿态」的信号——Manager 被迫去修并不存在的缺陷。
- **固定 checklist 机械总分**：把判断压成必填八维评估表 / Pass 表。审查退化为填表；八维全过就 PERFECT，任何一格没勾就 REVISE。判断的「区分力」被表格结构替代。
- **无证据偏好冒充缺陷**：reviewer 把「我会写得不同」说成缺陷。REVISE 不购买任何实质改进，只表达口味。
- **PERFECT 被误读为全知/字面无瑕**：reviewer 不敢在 PERFECT 时说真话（怕「不完美」与「接受」冲突），于是压制真实但 non-blocking 的观察；或者反过来，任何 tiny typo 都自动 REVISE。
- **判断对象漂移**：判断变成对 reviewer mood 的投射，而不是对 root requirement + 当前被审对象的评估。

`review-judgement` 存在的意义：**PERFECT/REVISE 必须由 discrimination 挣得**——acceptance 要挣，rejection 也要挣；material defect 才能 withhold；non-blocking workmanship 可以与 acceptance 共存；PERFECT ≠ 全知；REVISE 必须购买实质更好/更真的结果。

## RED 长什么样

满足以下任一情形，世界就是 RED：

1. reviewer 可以凭表演式谨慎（多 REVISE）、固定 checklist、或无证据偏好拒绝/接受工作；
2. 系统把 `judge` 的 verdict 当作可回声的状态对象（描述字段、回执 echo、旧名 `verdict` 复活）；
3. 判断不相对 root requirement / 当前被审对象，而相对 reviewer 的情绪或口味；
4. PERFECT 被当作「字面无瑕 / 全知」的承诺，或 non-blocking 观察被 PERFECT 噤声；
5. REVISE 不购买任何实质更好/更真的结果（例如 tiny typo 自动 REVISE、「测试必须总跑过」万能律）；
6. Examiner's Ledger 八维被烙成必填 report 字段 / Pass 表 / 固定八段标题。
7. reusable dedicated Reviewer 在上一轮成功 `judge` 后，被 session 级“already judged”标记永久静音；下一条独立 review request 到来时仍被当作上一轮重复提交并被 abort。

## 为什么必须独立存在（Independent Change Test）

HANDOFF §6.4 / §7.6 的裁决：`review-protocol` 拆成 `review-judgement` 与 `review-assurance`，因为

> 判断哲学可以整体重写，而 witness/finality 因果协议不变；反之亦然。

具体：你可以把 Role Law / Examiner's Ledger 的判断方向整个换成另一套 craft guidance（比如新的 materiality 理论），`ConfirmedReviewWitness`、typed physical challenge edge、record-ready 代数一行不用改。反过来，你可以重写 dual-PERFECT 的 CE 因果协议，判断的 discrimination 语义也不动。两个 failure meaning 完全不同：

- `review-judgement` RED = reviewer 可以凭表演/checklist/偏好决定 accept/reject；
- `review-assurance` RED = 系统可以消费针对旧 tree / 未看 challenge / 缺报告 的 judgement。

## 与相邻包的边界（为什么这些不归我）

| 邻近事实 | 真正的 owner | 为什么不归 judgement |
|---|---|---|
| 第二次 PERFECT 是否因果成立（direct CE、physical identity、attempt identity、tree invalidation） | `review-assurance` | 那是「这个判断有没有资格被消费」的证明，不是判断本身的含义 |
| 过程评审 1:1 节拍、Rk 义务、lag-1 | `obligation-ledger` | 何时派生评审义务是账本规则；judgement 只回答这一次判断怎么说 |
| 终末 cohort、rejection/blessing/rest、drain | `finality` | 不可逆结束的资格建立在 obligations + tree + review evidence 上，judgement 不是 finality 本身 |
| Reviewer 提示词怎么组合（Common Law → Role Law → Ledger） | `cognitive-environment` | 组合权威是认知环境的事；本包拥有的是判断方向的内容 |
| Manager 只能看到 outcome/report 窄例外 | `participant-horizon` | 信息准入边界，不是判断语义 |
| dedicated reviewer session 的 create/retire/replace | `managed-session-lifecycle` | 生命周期管理，非判断含义 |

## 历史教训（考古）

- 历史 why/review「判断哲学是 discrimination，不是 rejection 表演」：曾拒绝过「谨慎 = 多 REVISE」与「可描述偏好即缺陷」两个方向（备选与被拒节）。
- 历史 what/review REVIEW-011：Examiner's Ledger 是判断方向非 checklist；PERFECT+minor 共存；material defect 才 REVISE；无 prose 的过程 PERFECT 无效。
- 历史 why/glory「为什么 Manager 不能知道隐藏 review 机制」：旧 prompt 把 review 显式化为 checklist 的最后一步，Manager 会在工作未收敛时机械执行最后一项——checklist 化的代价是真实失败模式。
- 语义锚 `semantic-anchors.mjs` reviewer family 五条（discrimination / rejection-must-purchase / non-blocking / perfect-not-flawless / acceptance-not-omniscience）逐条对应本包命题——证明这些不是装饰性散文，而是被 gate 校验的合同。
