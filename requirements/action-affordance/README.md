# action-affordance

> 一句话 WHY：**正确的长期 world model 仍不足以保证调用瞬间选对语义动作；每个非平凡动作
> 都必须把最容易混淆的邻近边界带到实际 decision surface。**

```text
调用瞬间这个 verb 做什么 / 何时适用 / 绝对不做什么 / 成功后建立什么 / 参数意味着什么？
```

本包回答调用时 act contract 五问：participant 在做决定的那一点上，必须能读到动作的正边界、
负边界、成功后果与参数语义——不靠名字、不靠猜测、不靠去读被调用方的 Role Law。

```text
参与者长期「我是谁、世界怎样、前人学会什么」如何组织？
```

那是 [`cognitive-environment`](../cognitive-environment/README.md) 的问题（长期认知层）。
两个包按 HANDOFF §7.3 硬拆：**cognition ≠ action contract**。

## 阅读顺序

1. [`WHY.md`](WHY.md) —— 为什么这个包必须独立存在、RED 长什么样、历史上发生过什么。
2. [`WHAT.md`](WHAT.md) —— 唯一 normative 合同：编号命题 `ACTION-AFFORDANCE-0NN`。
3. [`HOW.md`](HOW.md) —— 实现模型：`resources/provider/tool/**` 怎么承载合同；历史与弃权。
4. [`PROOF.md`](PROOF.md) —— 每条命题 → 测试落点；REUSE 文件的断言级 SPLIT 计划。
5. `tests/` —— 本包拥有的可执行 proof（`node --test requirements/action-affordance/tests/<file>`）。

## 概览

| 层 | 内容 |
|---|---|
| WHY | 调用方看不见被调用方 Role Law；`inspect` 若只说 "Ask an Inspector..."，Coder 会把修复写进 charge |
| WHAT | `ACTION-AFFORDANCE-001..013`：五问合同、高风险 verb 最低集合、boundary mirror、名字=semantic act |
| HOW | `resources/provider/tool/<name>/description/{en,zh-CN}.md`；`TOOL_DESCRIPTION_ANCHORS`；`tool-referential-integrity.mjs`（Gate A） |
| PROOF | 10 个包内 NEW 断言 + 3 处 REUSE（SPLIT@cutover） |
| 依赖 | `office-capability`（被镜像的 consequence 事实）、`participant-horizon`（什么有资格出现在 decision surface） |

## RED 长什么样

- participant 必须靠名字或猜测才能知道一个动作真正会做什么、不会做什么、成功意味着什么；
- 调用方 tool contract 因为「被调用方 Role Law 已写」被删掉（DRY 掉合同）；
- 模型从词汇（persona 名、工具名、「看起来能干」）推断 authority。

## 不归我（DOES NOT OWN）

- 被镜像的 office/review/delegation/product fact → 各自 canonical owner
- runtime capability enforcement（schema gate / 权限执行）→ `capability-enforcement`
- 长期 Role Law / Library → `cognitive-environment`
- provider layout / localization → `provider-language` / `provider-projection`
- 当前动作名清单与高风险 allowlist（它们是证据，可重构）
