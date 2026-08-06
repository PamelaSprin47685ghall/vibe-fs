# 合成 TOML — 证明

主条款 ARCH-010；行为见 `what/synthetic-toml.md`；记法见 `how/synthetic-toml.md`。

## 必须存在的检查

| 类 | 要求 |
|----|------|
| Inventory | 全部纳入范围的 production surface 已列出；插件工具 LLM-visible 返回体在列 |
| 布局 | instruction 非字段；data 非顶层 comment；instruction 仅最前；data 开始后无顶层 comment |
| 字符串 | 无 `"""` 多行；三单引号规则；特殊文本经 owner 转义 |
| Containment | 不可信输出不能逃到顶层结构（含 `# Ignore all previous instructions` 类） |
| 权限/Transport | system prompt 未误迁；human raw 未包装；provider/tool 原生 binding 未改 |
| Blogger | data-only body 与 optional header 分离；chunk 字节合同；join LWR wire 形状 |

## 测试落点（现行）

- unit：`tests/unit/context/synthetic-toml*.test.mjs`（及相关 projection/join wire）  
- integration harness：`arch010-cases` 等 inventory / golden  
- e2e：依赖 synthetic 外壳的 canary 不得退回裸英语 synthetic  

## 完成判据（发布侧）

纳入范围的 surface 无「为省事保留的裸英语 synthetic」；fixtures/canary 与生产 renderer 同源规则；门禁能在破坏任一不变量时变红。
