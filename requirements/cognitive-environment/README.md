# cognitive-environment

> 一句话 WHY：**长期 World / Role / inherited knowledge 与 Runtime / Mission 分开；
> knowledge 不创造 authority。**

```text
参与者长期「我是谁、世界怎样、前人学会什么」如何组织？
```

本包回答长期认知的组织边界：哪些材料属于稳定认知层（World / Role / inherited knowledge），
哪些属于瞬时层（Runtime / Mission），以及为什么继承的知识不能变成资格。

```text
调用瞬间这个 verb 做什么/不做什么？
```

那是 [`action-affordance`](../action-affordance/README.md) 的问题（调用时 act contract 五问）。
两个包按 HANDOFF §7.3 硬拆：**cognition ≠ action contract**。长期自我/知识环境与调用时动作合同
可以独立重写。

## 阅读顺序

1. [`WHY.md`](WHY.md) —— 为什么这个包必须独立存在、RED 长什么样、历史上发生过什么。
2. [`WHAT.md`](WHAT.md) —— 唯一 normative 合同：编号命题 `COGNITIVE-ENVIRONMENT-0NN`。
3. [`HOW.md`](HOW.md) —— 实现模型：`Infrastructure/Resources/PromptResources.fs` 怎么组合各层；历史与弃权。
4. [`PROOF.md`](PROOF.md) —— 每条命题 → 测试落点；REUSE 文件的断言级 SPLIT 计划。
5. `tests/` —— 本包拥有的可执行 proof（`node --test requirements/cognitive-environment/tests/<file>`）。

## 概览

| 层 | 内容 |
|---|---|
| WHY | 瞬时 runtime/mission 会污染身份；知识会偷渡 authority；两者都要靠稳定认知层拦截 |
| WHAT | `COGNITIVE-ENVIRONMENT-001..013`：五层组合、knowledge≠authority、Role Law 自我模型、craft 资产 |
| HOW | `Infrastructure/Resources/PromptResources.fs`（`systemForRole` 组合）；`resources/provider/{world,role,library,host}/**`；`prompt-depth-ratchet.mjs` |
| PROOF | 9 个包内 NEW 断言 + 4 处 REUSE（SPLIT@cutover） |
| 依赖 | `participant-identity`（Persona 稳定）、`office-capability`（只引用 authority facts） |

## RED 长什么样

- 长期 self/world model 被瞬时阶段、能力、任务或外来知识重写（例如把「当前这个任务」写进 Role Law）；
- 继承知识创造原本不存在的 authority（书扩大 Role 权、universal bible、第二真源）；
- 同 role 因 execution strength 获得两套冲突的思想传统（fast/deep 异书）。

## 不归我（DOES NOT OWN）

- office consequence、Persona identity、action contract → `office-capability` / `participant-identity` / `action-affordance`
- mission/lifecycle/todo/review/finality 事实 → 各自 owner（`obligation-ledger` / `review-judgement` / `finality` …）
- provider language、wire rendering、prefix byte stability → `provider-language` / `provider-projection` / `prefix-stability`
- 所有 provider prose 的业务意义；meaning 仍属各 semantic owner（本包只拥有认知层组织边界）
