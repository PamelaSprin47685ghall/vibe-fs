# Agent — 目标实现

## Implements

行为合同见 `what/agent.md`；本文件只描述目录装配、权限计算和可见面生成算法。

## Ownership

模块边界与写入口见 `shape/agent.md`。

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
