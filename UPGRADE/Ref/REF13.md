# REF13: Deepthink 模式——架构概览

## 1. 定位

Deepthink 是一个多代理搜索和精炼系统，核心思想是"搜索优于选择"——先生成多个独立策略分支，让各分支独立探索，经受批判压力，最后通过裁判收敛。

## 2. 两种执行路径

| 路径 | 配置 | 适用场景 |
|------|------|----------|
| 单通道 | 不使用 EDFS | 简单到中等复杂度任务，可选子策略和单次修正 |
| EDFS | 使用 EDFS | 复杂任务，多轮迭代修正，策略池，记忆压缩，策略替换 |

## 3. 核心概念

- **Strategy-as-lens**: 策略是观察问题的镜头，不是步骤清单
- **分支身份**: 每个主策略是独立分支，下游代理不能更换策略
- **精选上下文**: 每个代理只收到其角色所需的最小上下文
- **深度与广度分离**: 修正循环做深度，解决方案池做广度

## 4. 文件结构

| 文件 | 职责 |
|------|------|
| DeepthinkCore.ts | 核心编排、并行、重试、状态转换 |
| DeepthinkIterativeHistory.ts | 确定性 prompt 和仓库构建 |
| DeepthinkPrompts.ts | 可自定义系统指令 |
| Deepthink.ts | UI、逻辑、事件协调 |
| Deepthink.tsx | React 组件 |
| DeepthinkAgents.ts | 可重用代理 |
| SolutionPool.ts / .tsx | 解决方案池管理 + UI |
| DeepthinkConfigPanel.tsx | 配置面板 |
| DeepthinkLiveTab.tsx | 实时执行监控 |
| DeepthinkConfigController.ts | 配置约束 |
| ArxivAPI.ts | arXiv API 集成 |

## 5. 状态数据结构

核心状态类型约 20 个，涵盖策略、子策略、假设、批判、解决方案池、PQF、记忆、分支替换等。
