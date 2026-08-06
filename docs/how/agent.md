# Agent — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
在多 Agent 协同体系中，Agent 拥有不同的能力层级（Fast / Deep Tier）、模型配置与工具使用权限。如果允许模糊别名、运行时猜想 Agent 名或全局混用工具权限，会导致越权工具调用、混乱的提示词上下文与难以调试的越界行为。Agent 模块旨在提供静态校验的 20 名 Agent 目录、对称的 Fast/Deep 配对校验，以及基于 `CanonicalRole` 的严格工具权限裁剪。

### 2. 输入输出与规则边界
- **输入**：`resources/prompts/` 下的静态 Prompt assets、`CanonicalRole` 规则。
- **输出**：`ManagedAgentConfig` 实例、ToolPermission 结构、可暴露给 Provider 的 Agent 模式清单。
- **核心边界与不变量**：
  1. 20 名 Agent 静态目录（AGENT-002）：启动时校验 Fast/Deep 配对与模型非空；旧别名（AGENT-004）直接拒绝，不设兼容 Aliases。
  2. 基于 CanonicalRole 的工具权限（AGENT-007）：工具使用权限严格从 CanonicalRole 派生；Role 未确定前可见工具集必须为空。
  3. Blogger / Executor 物理隐匿（AGENT-008）：内部角色绝对禁止进入 Provider 可见的 Agent 模式或枚举。
  4. 静态构造 Fork Schema（AGENT-009）：Fork 可选集合静态构造，禁止根据“当前哪些 Session 活跃”反推。

---

## 启动装配

1. 枚举 AGENT-002 二十名；缺一 fail fast。  
2. 校验 `peer(fast)=deep` 对称；model 字符串非空且 pair 内互异。  
3. 非法旧名（AGENT-004）在配置验证阶段拒绝，无 alias。

---

## 权限对象

1. 由 `CanonicalRole` 导出角色工具集 → Host-final permission + ToolRegistry 两层（AGENT-007）。  
2. Role 未定 → 模型可见插件工具集为空。  
3. `external_directory="allow"` 仅经 `StaticTools.permissionObj` → `ManagedAgentConfig.applyOwnedFields` 写入每个 managed agent（AGENT-019）。  
4. 禁止全局 permission 顶替 agent 级写入。

---

## 可见性

1. Blogger/Executor 从一切 provider-visible enum/schema 剔除（AGENT-008）。  
2. fork schema 可见集合按 AGENT-009 **静态**构造，不从运行时「当前有哪些 agent」反推。  
3. list() 只暴露 running handle，不是可创建目录。
