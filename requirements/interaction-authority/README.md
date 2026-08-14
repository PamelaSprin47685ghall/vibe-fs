# interaction-authority

> 一句话 WHY：**物理 user-shaped message 不等于 authority；只有 typed provenance 能创建或继续
> logical interaction。**

```text
这个逻辑 interaction 有资格发生吗？
```

本包回答「有资格发生」：一条消息/一个内部动作凭什么能**新建**一个 Logical Run（Root），或只能
**延长**一个已存在的 Logical Run（Continuation），以及一切无法证明身份的东西如何 fail-closed。

```text
已经决定发生后，如何穿过 unreliable Host 而不复制逻辑效果？
```

那是 [`dispatch-protocol`](../dispatch-protocol/README.md) 的问题（transport receipt、claim 生命周期、
at-most-one）。两个包按 HANDOFF §7.4 硬拆：**authority ≠ dispatch**。

## 阅读顺序

1. [`WHY.md`](WHY.md) —— 为什么这个包必须独立存在、历史上 RED 长什么样。
2. [`WHAT.md`](WHAT.md) —— 唯一 normative 合同：编号命题 `INTERACTION-AUTHORITY-0NN`。
3. [`HOW.md`](HOW.md) —— 实现模型：`src/` 里哪个类型/函数承载哪条命题；历史与弃权。
4. [`PROOF.md`](PROOF.md) —— 每条命题 → 测试落点；`authority.test.mjs` 的断言级 SPLIT 计划。
5. `tests/` —— 本包拥有的可执行 proof（`node --test requirements/interaction-authority/tests/<file>`）。

## 概览

| 层 | 内容 |
|---|---|
| WHY | 物理 `role=user` 廉价可伪造；typed provenance 是唯一 authority 证据 |
| WHAT | `INTERACTION-AUTHORITY-001..015`：Root 独占权、Continuation 禁区、来源解析、fail-closed、assistance/join 延续 |
| HOW | `Domain/PromptAuthority.fs`、`PromptAuthorityRun.fs`、`Application/Prompting/PromptIngress.fs`、`Interaction/Authority/PromptFactFold.fs` |
| PROOF | 17 个测试落点（REUSE 为主 + 2 个包内 NEW 文件）；`authority.test.mjs` SPLIT@cutover |
| 依赖 | `participant-identity`（Persona/ExecutionBinding 不是本包）、`session-ontology`（Companion 关联不是本包） |

## RED 长什么样

合成/unknown/continuation 冒充新 Root，或重置只有 Root 才能建立的 authority 事实
（LogicalRun、SelectedAgent、Fallback root、repair 预算、默认 Agent、Model）→ 本包 RED。

## 不归我（DOES NOT OWN）

- transport claim / submission / physical acceptance 协议 → [`dispatch-protocol`](../dispatch-protocol/README.md)
- provider projection、attempt recovery、Persona 定义
- `AttemptExecutionProfile` 的当前 record 布局（HOW）
- Companion 关联、SessionPersona 重绑、Model=None 发送海关（分别归 `session-ontology` / `participant-identity` / `dispatch-protocol`）
