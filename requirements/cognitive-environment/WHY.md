# WHY —— 不可替代的存在理由

## 为什么必须独立存在

participant 面对的材料天然分两层：

```text
长期层   World（世界常识）· Role（我是谁）· inherited knowledge（前人学会什么）
瞬时层   Runtime（此刻发生了什么）· Mission（这个 assignment 要成为什么）
```

两层必须分开，因为：

- **瞬时污染长期**：如果把「当前 mission 的进展」「当前 execution strength」「当前工具清单」写进
  Role Law，换任务/换 binding 就等于换人格；`fast-`/`deep-` 会演化成两套产品、Peer Fallback 换模型时
  半途换人。自我模型必须只由稳定事实构成（`The system prompt names the office. The conversation tells
  you which road is yours.`）。
- **知识偷渡 authority**：技术书籍会教「怎么识别缺陷、怎么验证」——一旦书被视为资格来源，
  「读过的角色」就获得了「没读过的角色」没有的权。`Information may cross authority boundaries.
  Authority does not travel with it.`（PROMPT-016）。knowledge≠authority 是本包核心命题。
- **组合需要唯一权威**：每个 provider-facing 自然语言材料必须恰属一个主权威，冲突按语义所有权裁决，
  不设「更靠近 system 者胜」全序（PROMPT-015）——否则 role/office/delegation 各写一份互相矛盾的
  「我是谁」。

**唯一不可替代的 WHY**：长期认知的组织边界。它可以整体重写（比如换掉 Common Law / Role Law /
Office Library 的名称与结构）而不动 authority、Persona、action contract；反过来，任何 authority 模型
的重写都不应要求认知层跟着改。

## RED 长什么样（失败模式）

| 症状 | 历史出处 |
|---|---|
| 书扩大 Role 权（Library 教识别缺陷却暗示可修复） | archive/docs/why/prompt.md「Office Library：knowledge ≠ authority」：拒书授职权 |
| universal bible 灌每个 persona；同 role 的 fast/deep 异书 | PROMPT-016 禁令；fast/deep 共享同一 Role Law（AGENT-001） |
| 把隐藏编排写进 Reviewer 书 | PROMPT-016 禁令；REVIEW-012：双 PERFECT 流程不入 Reviewer prompt |
| 把工具清单/瞬时 capability 枚举进 system prompt | PROMPT-015：`Tools 不是 Role Prompt 章节`；PromptRestoration：Manager Role Law 去掉工具清单 |
| 从 persona 名/工具名推断 authority | PROMPT-021：模型不得从词汇推断 authority；名字只表达 semantic act |
| Prompt Restoration 前：system prompt = zh-CN、tool description = English 的半 i18n 世界 | PromptRestoration.md Gate 0：语言不统一会撕裂认知环境（语言面归 `provider-language`，本包取组合纪律） |

## 为什么不是「所有模型该知道的」垃圾桶

旧 `participant-guidance` 名字太容易变成「everything the model should know」（HANDOFF §6.1）。
本包只拥有**认知层的组织边界**：分层、组合顺序、knowledge≠authority、Role Law 自我模型。
每个 canonical fact 仍归其真正 owner——Coder mutation authority → `office-capability`；
PERFECT/REVISE 意义 → `review-judgement`；persona 稳定性 → `participant-identity`。
Prompt/Role Law 只是这些事实的 presentation surface，不获得 semantic ownership（HANDOFF §6.1 末段）。

## 被拒方案（考古）

- **「更靠近 system 者胜」全序覆盖。** 拒绝：Mission 不能授予 Role 没有的权；Library 不能扩大 Role；
  Handbook 遇 concrete requirement 时具体要求胜；Rulebook 不是 present-case evidence
  （archive/docs/why/prompt.md）。
- **把 Pair Hint 拆成多个独立 synthetic 消息（一个中文、一个 NEEDHELP、一个并行工具）。** 拒绝：
  一个 canonical Pair Hint occurrence 承载全部 craft；provider renderer 只决定 wire 形状
  （pair-parallel-tools.md §18）。
- **把 NEEDHELP 写成稀缺/失败语言（"only when truly blocked"）。** 拒绝：制造求助羞耻、诱导长时
  低价值自我挣扎（increase-strength.md §3.1）；budget 是机器侧护栏，不进 provider 文案。
