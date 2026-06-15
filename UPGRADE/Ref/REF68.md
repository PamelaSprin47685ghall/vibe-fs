# REF68: 批判聚合与冲突解决算法

## 1. 批判聚合（Dissected Observations Synthesis）

```typescript
buildDissectedSynthesisPrompt({
    challengeText,
    knowledgePacket,          // 可选的假设包
    solutionsWithCritiques,   // 所有方案 + 批判的封装
}) → prompt
```

## 2. 冲突解决协议

当多个批判之间存在矛盾时：

```
优先级规则:
  1. 更具体、基于证据的批判优先
  2. 识别关键渲染/逻辑错误的批判优先
  3. 更严格的批判优先
```

## 3. 合成结构

```
CRITICAL BUGS & SYNTAX ERRORS CHECKLIST
  - 所有编码错误、崩溃、逻辑缺陷的合并列表
DESIGN & UX/UI GAPS CHECKLIST
  - 响应式问题、样式问题、缺失状态
STRATEGY CONFLICTS & ROBUST PATHWAYS
  - 哪些策略成功/失败，哪些技术应合成
UNIFIED SUGGESTIONS INVENTORY
  - 按影响分类的功能建议、设计增强
```

## 4. EDFS 中的批判管理

EDFS 模式下没有全局合成（dissectedObservationsEnabled 被禁用），
批判存储在分支历史中：

```typescript
runtime.history.push({
    globalIteration,
    branchIteration,
    branchVersion,
    label,
    solution,
    critique,    // 本条历史的批判
})
```

## 5. 批判的版本化

每个批判携带版本信息：
```typescript
interface DeepthinkSolutionCritiqueData {
    branchVersion?: number           // 哪个分支版本
    globalIteration?: number         // 哪个全局迭代
    branchIteration?: number         // 哪个分支本地迭代
    strategyTextSnapshot?: string    // 批判时的策略文本快照
}
```
