# REF18: Deepthink 模式——执行代理 (Execution Agent)

## 1. 角色定位

执行代理（也称为"首次工作产出代理"）是为每个策略/子策略创建第一个完整工作产出的代理。

## 2. 上下文输入

执行代理接收：
- Core Challenge
- 分配的主策略文本
- 分配的子策略文本（或直接策略）
- 其他策略文本（仅用于情境感知）
- 适用的假设信息包（hypothesis packet）
- 分支身份元数据（版本、迭代号）
- 原始图片（如果有）

不接收：
- 其他策略的执行结果
- 批判
- 记忆银行
- 解决方案池

## 3. 提示词构建 (buildSolutionAttemptPrompt)

```typescript
const prompt = buildSolutionAttemptPrompt({
    challengeText,
    mainStrategy,
    subStrategy,
    knowledgePacket,       // 策略感知的知识包
    otherStrategyContext,  // 跨策略上下文
    branchContext,          // 分支身份
})
```

## 4. 执行语义

- 必须忠实执行分配的策略框架
- 产生的用户面向的工作应直接回应用户挑战
- 不能输出系统层面的解释（"我遵循了策略X"）
- 如果选择性假设包或信息包存在，可使用其中已验证的发现
- 使用假设信息时不能直接引用"假设包说"，而应将证据重构为自己的论证

## 5. 领域适应

根据任务类型自动适应：
- 数学：严谨推导、情况分类
- 代码：完整可执行、边界处理
- 创意写作：实际产出工件
- 法律：结构化论证
- 产品：可操作的输出
