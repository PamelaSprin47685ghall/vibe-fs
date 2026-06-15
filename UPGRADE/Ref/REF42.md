# REF42: SolutionPool 管理

## 1. 版本管理

```typescript
interface SolutionPoolVersion {
    content: string
    title: string
    timestamp: number
}
```

内存 Map 存储每个 pipeline 的版本历史：
```typescript
const solutionPoolVersions = new Map<string, SolutionPoolVersion[]>()
```

## 2. 核心函数

| 函数 | 功能 |
|------|------|
| `addSolutionPoolVersion(id, content, iter)` | 添加新版本快照 |
| `getSolutionPoolVersionsForExport(id)` | 获取导出副本 |
| `restoreSolutionPoolVersions(id, versions)` | 从导出恢复 |
| `openSolutionPoolEvolution(id)` | 打开演化查看器 |
| `downloadSolutionPoolAsJSON(id)` | 下载为 JSON |
| `computeIterationCount(process)` | 计算迭代数 |

## 3. SolutionPoolTabContent

React 组件，在 Deepthink 的解决方案池 tab 中显示：
- 每个迭代一个容器
- 每个分支的池状态（可用/处理中/错误）
- 查看方案池按钮
- 记忆银行条
- 替换分支信息

## 4. 全屏面板

`openSolutionPoolModal()` 和 `openCurrentSolutionPool()` 提供全屏查看方案池详细内容的功能。

## 5. Memory Bank Strip

在方案池 tab 中显示所有策略的记忆银行：活跃分支和已替换分支的记忆银行均显示。
