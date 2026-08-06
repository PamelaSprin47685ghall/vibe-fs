# 上下文恢复 — 可观察行为

条款前缀：`CTX-`。  
分派边界见 `shape/context.md`。  
probe / squash / delta 算法见 `how/context.md`。

## 核心裁决

**不预测，只恢复。** 正常请求总是先直接执行。只有真实 provider attempt 失败之后的恢复槽，才允许尝试前缀替换或 frame 压缩。

## CTX-001：不观察容量

不得读取、查询、推导、缓存任何模型上下文窗口大小。  
禁止 contextWindow / remainingTokens / headroom / nearLimit / shouldCompact / ensureCapacity 等一切同义词概念。  
禁止 tokenizer、模型窗口表、字节→token 换算。  
管理员配置与 provider 元数据不得改变本条。

唯一允许的字节计量：CTX-003 输入合同，以及文件/进程类既有合法计数（EXEC-011）。

## CTX-002：不主动预测溢出

请求前不得判断「是否接近上限」。  
禁止按投影长度比例、剩余输出预算、Y 字节阈值、累计 token、型号选压缩点。  
真实失败是唯一恢复触发信号。第一次溢出表现为失败——这是主动接受的代价。

区分：HOST-006 重锚读的是「已发生的 compaction 事实」，不是「还剩多少空间」。

## CTX-003：最低环境合同

支持的 LLM 在扣除固定 system/tools/封装后，至少能接收 **200 KiB** provider-visible 动态输入。  
`BloggerDeltaLimitBytes = 200 * 1024` 是**输入合同**，不是窗口估算：不与窗口比、不算比例、不触发主动 squash。  
计量点：TOML 渲染后的 UTF-8 字节。

## CTX-004：输出预算属 provider

插件不计算 squash 应占多少 token，不检查压缩比。  
唯一内容校验：`isValidTerminal = 非空 ∧ 非 XML-only`（与 FALLBACK-008 对齐）。

## CTX-005：失败不分类

控制流只看 snapshot `Outcome`：`Completed | Failed | Aborted`。  
不得按错误文字/类型名区分溢出、网络、限流等。  
「溢出」只许出现在诊断，不得进 Journal 字段或 probe/squash 判定。  
compaction 来源同样不分类。

## CTX-006：恢复槽

合取三者才允许恢复动作：

```text
armed ∧ primed ∧ hasMaterial
```

| Session | 动作 | 额外 LLM |
|---------|------|----------|
| X | prefix probe（不先永久提交） | 无 |
| Y | frame squash（有效则提交） | 一次 |

无材料时发正常主请求——正常状态，不是错误。  
恢复槽是机会，不是「必然压缩」。

## CTX-009：X 不发压缩请求

Work Session 从不向主模型发送「请压缩历史」类请求。压缩只发生在 Y 的 squash 或 X 的 prefix 替换投影。

## CTX-014：诊断边界

可观测诊断不得变成控制输入：不得用日志字段驱动 Fallback/probe/squash 分支。
