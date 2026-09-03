# finality — WHY

## 1. 存在理由与核心矛盾

Manager 自行宣告「工作已完成」仅属于参与者的单方主张。在允许 mission 不可逆结束（LifeCompleted、会话终结及资源释放）之前，系统必须严格检验三个独立维度的条件：

1. **当前义务**：Manager 是否已进入并遵守 checkpoint 协议，并确立 plan commitment？（`obligation-ledger`）
2. **当前被审对象**：终审结论是否切实针对当前 Git 代码树与当前请求，而非历史失效证据？（`review-assurance`）
3. **合格证据**：是否存在针对当前代码状态的合格终审确认见证（fresh barrier + dual-PERFECT witness）？

若缺乏严格的终结协议，系统将退化为以下失败形态：

- **自宣即完成**：Manager 调用终结工具即可直接退出，零 checkpoint、零终审亦能通过，终局质量门退化为形式化步骤。
- **终态语义含混**：未获接受被误作安息，或已获接受却被禁止收尾；Manager 无法区分「被拒后继续工作」、「已获接受但需处理 minor 观察」与「真正安息」。
- **已接受成果被后续轻微观察推翻**：已经获得的 acceptance 因后续 non-blocking 观察被撤销，缺乏稳定性保护。
- **隐藏审查编排泄漏**：Manager 获悉 Reviewer、barrier、witness 及 2N 机制，将终局质量门重新当成可预谋的最后一步，在工作未收敛时抢先触发。
- **系统故障被掩盖为第三种裁定**：Reviewer 传输、Journal 或报告物化失败被压制为 `Undecided`，使真实故障被掩盖，污染持久化历史。

`finality` 保证：**不可逆结束资格必须牢固建立在清偿义务、当前代码树与合格评审证据之上；rejection、blessed 与 rest 是三种正交经验；Acceptance 与 rest 具有不同门槛。**

## 2. 独立存在测试（Independent Change Test）

若重写终结工具的 UI 形态或内部 cohort 组织形式，只要保持「仅凭合格证据方可达成 life completion」的规范不变，`obligation-ledger`、`review-assurance` 与 `participant-horizon` 的规范定义完全无需修改。

反之，若允许绕过未清偿义务或取消双重确认直接结束，将彻底破坏 mission 的交付纪律。这是一个完全独立的失败域。

## 3. 核心不变量与失败判定

系统在以下任一情况发生时判定为 RED：

- 参与者绕过未清偿义务或评审证据直接结束（零 checkpoint 终结、无新鲜 barrier/tree 即可获得 blessing）。
- acceptance、rejection 与 rest 被压缩为单一含混状态。
- Manager 视野内暴露隐藏 Reviewer、barrier、witness 或 cohort 编排细节。
- 状态推导依赖故事文本匹配而非强类型事实。
- 现代 Finality 将基础设施故障映射为业务级 `Undecided` 或向模型返回未能裁定，而非直接 fail-fast。

## 4. 依赖边界

```text
DEPENDS ON: obligation-ledger, review-assurance, participant-horizon
```

## Physical fatal boundary

Finality owns infrastructure-failure classification and the point beyond which no business fallback exists；console/process termination remains aHost adapter capability。Direct fatal would mix adjudication withphysical authority and permit duplicate termination across tool and composition layers。
