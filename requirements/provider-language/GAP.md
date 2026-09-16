# provider-language — GAP

## GAP-PL-001: 核心角色双语 Prompt 同源性与分身表述清理闭包

- **现状**：部分角色提示词中仍留有旧 Manager Fission 或旧五分法示例；需按重构方案 §7.4 全面更新双语 Prompt 并通过 parity gate 校验。
- **目标合同**：PROVIDER-LANGUAGE-012 保证核心角色双语 Prompt 语义一致同源并彻底清除违规分身表述。
- **影响范围**：`resources/provider/role/`、`scripts/checks/language-parity-gate.mjs`。
