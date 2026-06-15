# REF22: Deepthink 模式——记忆银行代理 (Memory Bank Agent)

## 1. 角色定位

记忆银行代理接收一个策略分支、其先前的记忆银行（如果有）和最近 5 个分支本地执行/修正加批判条目。产生一个统一记忆银行，递归合并先前教训与新窗口。

## 2. 上下文输入

- Core Challenge
- 一个活跃策略和分支版本
- 先前的记忆银行（如果有）
- 接下来 5 个未压缩的分支历史条目
- 原始图片（如果有）

不接收：
- 方案池、假设包、其他策略、全局仓库

## 3. 输出结构

```markdown
### Validated Invariants
### Dead Ends
### Persistent Flaws
### Useful Techniques
### Refuted Assumptions
### Open Questions
### Branch-Level Guidance For Future Corrections
```

## 4. 关键约束

- 不要总结方案散文/叙事——总结探索空间
- 不要产生原始挑战的最终答案
- 关注探索景观：尝试了什么、熬过了批判、失败了什么
- 如果有先前的记忆银行，递归合并而不是覆盖

## 5. 调用时机

- EDFS 模式：当分支积累了 5 个未压缩条目后调用
- 记忆光标在成功蒸馏后前进
- 稍后的记忆调用接收先前的记忆银行 + 下 5 个原始条目
- 完整的原始分支历史保留用于 UI 和归档
