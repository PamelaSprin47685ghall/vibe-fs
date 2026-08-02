# SSOT修正方案指导

**User:** Anonymous  
**Created:** 8/2/2026 20:48:41  
**Updated:** 8/2/2026 21:02:28  
**Exported:** 8/2/2026 21:02:44  
**Link:** [https://chatgpt.com/c/6a6f3be4-e064-83eb-b799-af22c4ba6e15](https://chatgpt.com/c/6a6f3be4-e064-83eb-b799-af22c4ba6e15)  

## Prompt:
8/2/2026, 8:48:38 PM

目前父给子的 prompt，工作记录，以及子给父的 tool result，工作记录，不对称，而且样式混乱，toml 中夹杂空行，而且很多非语义字段（llm 根本不关心，浪费 token）。我们应该统一成 Y 版工作记录（这个名词不好，但大概是这个意思）。注意：每个 session 第一条 prompt 应该在 Y 版工作记录中保持原状，无需压缩（这是新的行为，请修改提示词和工作流程支持，因为第一条 prompt 往往是用户的真实需求）。同样，每个 session 的最后一次输出不会发回 llm，因此不会经过 transform 钩子，最自然的方法就是不专门对其压缩，而是原样进入 Y 版工作记录（也有道理，因为往往是详细工作总结）。这样 Y 版工作记录就完全是自包含的全生命周期描述，因此除了 Y 版工作记录，不用再专门收集最后一条“回复”或者其他机制。还有个问题：父传子的时候，由于 Y 存在时滞，可能 Y 还没有追平（大概率也追不平），中间的缺口可以合法用 X 的部分补齐。这是唯一需要 fallback 到 X 的。X 的定义统一包含思考 reasoning，无论是用于生成 Y，还是用于补齐 Y，只要有 reasoning 也包含。没有 reasoning 不强求。

这样大大简化了整个系统逻辑。请你给出哪些 SSOT 需要修正，然后生成 STATUS/ 保姆级方案指导程序员实现全部修改。

[Attachment: repomix-output(133).xml]

## Response:
8/2/2026, 8:48:40 PM

我会先定位现有 X/Y、transform、父子传递和 SSOT 的真实定义，再把修改面收敛成一份可直接落地的 STATUS/ 实施方案。重点会核对首条 prompt、末条输出、Y 时滞补洞、reasoning 归属以及 TOML 线格式。

初步确认，当前规范把 A(X) 定义为 assistant 正文与 reasoning，把 B(X) 定义为仅含 Y assistant 日志；父子背景、Y delta 和 tool result 又走不同表示。这正是“不对称”的根源，修改不会只落在一个 prompt 文件。

已经发现两个直接的浪费点：正常 Blogger 指令目前既写进 delta 的 TOML comment，又作为独立 synthetic user message 注入，实际上重复了一遍；`join` 还把最终文本、工作记录和大量运行时 ID 同时发给父模型。方案会把这两处一起收敛。

> **制定提示词匹配与伴侣回复修正方案**
>
> 
>
> **检索 XML 中关键术语**
>
>
> **检索并检查XML及SSOT文件关键词与规范**
>
> 
>
> **检查SSOT文档与代码变更**
>
>
> **保留原始提示**
>
> 我正在统一父子会话的工作记录格式，去除空行和无意义字段，并让每个会话的第一条提示保持原样。后续记录则统一采用精简、语义优先的结构。
>
> **保留了原始提示内容**
>
> 
>
> **检查工作记录编解码与测试**
>
>
> **制定实现方案**
>
> 我正在据此整理 SSOT 修订清单与程序员实施方案：统一父子会话工作记录，保留每个会话首条提示原文，规范 TOML 结构，并修正终端记录注入时序与状态同步。
>
> **制定了实现方案**
>
> 
>
> **分析父子生命周期与 Y 记录设计**
>
>
> **检查SSOT、源码、工具编码及伴随执行流程一致性**
>
> 
>
> **查看项目配置与测试命令**
>
>
> **检查项目配置、源码及保姆计划验收要求**
>
> 
>
> **撰写全生命周期工作记录迁移方案**
>
>
> **撰写并验证全生命周期工作记录迁移方案**
>
> 
>
> 思考了 13m 28s

已基于完整仓库快照梳理现有 A/B、Companion、fork/join、terminal、prefix probe、持久化与 TOML 链路。`repomix-output.xml`

## 需要修正的 SSOT

**核心规范必须重写：**

- `SSOT/01.md`：ARCH-010、TOML 空行与最小字段规则
- `SSOT/07.md`：废止 session-wide A，改为统一 `XTrace`
- `SSOT/08.md`：重写 Companion、Y coverage、Seed、FrozenB 等定义
- `SSOT/09.md`：统一父→子和子→父的 LWR wire
- `SSOT/11.md`：Opening、XTrace、Terminal 的持久化事实和 schema
- `SSOT/12.md`：Blogger delta、双 coverage、prefix probe 与 X gap
- `SSOT/13.md`：最小 tool result、fork envelope 和 TOML 详细合同

**术语、验证和交叉引用必须同步：**

- `SSOT/00.md`
- `SSOT/10.md`
- `SSOT/14.md`
- `SSOT/15.md`
- `SSOT/99.md`

`SSOT/03.md` 只需在首条 prompt 的捕获点绑定 PromptAuthority 时补交叉引用；`SSOT/16.md` 暂未发现必须改变的核心语义，但实施时应扫描旧术语。

## 建议冻结的目标模型

```text
LifecycleWorkRecord(X)
  = OpeningPromptRaw
  + CompressedMiddleFromY
  + RawGapFromX
  + TerminalOutputRaw
```

其中：

- X 是唯一、持久化的完整语义轨迹，有 host-visible reasoning 就包含。
- Y 只压缩中段。
- 首条任务 prompt 原样保留，不交给 Y。
- 最终输出原样进入同一个 LWR，不再另设 `FinalText`。
- Y 落后时，只用 X 的未覆盖 suffix 补洞。
- 父→子与子→父只传 LWR。
- 删除 `B else A`、`Seed`、`formalRecord`、独立 final reply 和 work-record metadata。
- prefix replacement 只能使用已被 Y 证明覆盖的前缀，不能使用 RawGap。

方案中特别处理了一个容易被忽略的问题：child 的“首条 prompt 原样”应指 **fork 原始 assignment 和权威 requirements**，而不是包含 `parent_work_record` 的 synthetic transport envelope，否则多代 fork 会递归嵌套父记录并迅速膨胀。

完整 STATUS/ 方案包括 SSOT 逐条修改、目标类型、数据流、十个提交阶段、文件级修改地图、journal 迁移、测试矩阵、gate、canary、验收命令和 Definition of Done：

[下载 STATUS/ 全生命周期工作记录统一改造方案](sandbox:/mnt/data/STATUS-lifecycle-work-record-migration.md)

该文件目前是实施方案，没有直接修改上传的仓库代码。



---
Powered by [ChatGPT Exporter](https://www.chatgptexporter.com)