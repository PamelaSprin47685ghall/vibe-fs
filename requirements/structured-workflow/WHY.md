# structured-workflow — WHY

`WHAT.md` 是唯一 normative 合同。本文只解释为什么架构治理收敛为 `subsystem -> compile shard -> source`。

## 为什么只保留 subsystem

过去同时维护 semantic owner、locality、fsproj、slice、exposure、audience、manifest 与 adjudication。每层单看都有理由，叠加后却产生第二套产品：维护者必须先理解治理模型，才能修改业务代码；而大量细项目仍没有换来稳定的 change locality。

Subsystem 回答唯一值得人工治理的问题：**哪一组知识、决策、不变量、失败语义与外部合同可以整体理解、整体替换？** 重构、重写、删除、任务分工与验收只以 subsystem 为单位。一个 subsystem 内部可以有很多 compile shard，但 shard 不拥有新的业务身份。

## 为什么 compile shard 仍然可以很细

增量编译需要比业务治理更细的机械边界。稳定 `.fsi`、小 ProjectReference closure 和独立 adapter 能减少 implementation-only 修改的影响集合；这些收益不要求再创造 semantic owner。compile shard 应像函数内局部变量一样可调整：有收益就拆，无收益就合，不改变系统架构词汇。

因此：

```text
人类架构：Subsystem A -> Subsystem B
构建实现：A/a1 -> A/a2 -> B/b1 -> ...
源码：每个 .fs 恰属于一个 shard，进而恰属于一个 subsystem
```

## 为什么依赖倒置比 ACL 更重要

真正的解耦来自依赖方向，不来自授权表。若业务 consumer 为取得一个小类型而依赖一个同时携带 registry、factory、codec、并行工具和其他领域事实的大 project，再精细的 manifest 也没有把知识拆开。

正确做法是先按 reason-to-change 分离知识，再让 consumer 只依赖所需的窄 contract/port。通用平台原语必须不认识 Session、Provider、Git、Relay 等领域概念；物理 adapter 依赖外界，业务依赖 capability port。这样编译边界与认知边界同时缩小。

## 当前迁移如何判断进展

不以 shard 数量、owner 数量或目录整齐程度衡量。关注：

- subsystem SCC 是否缩小；
- ordinary change 是否更常落在 1 个 subsystem；
- shared gravity well 的 reverse closure 是否下降；
- 一个 subsystem 是否能只凭 public contract + proof 被替换；
- compile shard 拆分是否真的减少依赖，而不是把同一宽依赖改名。

第一批拆分因此选择 `Foundation.Identity` 与 `foundation-taskresult`：把 Outcome、Nudge、MetadataCodec、CanonicalJson、Parallel、AsyncSupport 与 Fission facts 从历史大包中分离，使依赖方直接取得所需知识，而不是补一轮新 ACL。
