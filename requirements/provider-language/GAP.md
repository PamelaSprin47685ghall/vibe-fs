# provider-language — GAP

## GAP-PL-001: 核心角色双语 Prompt 同源性与分身表述清理闭包（CLOSED）

- **状态**：CLOSED
- **现状与闭合证明**：核心角色双语 Prompt 语义已保持一致同源，违规分身表述已彻底清除。落点 `tests/012.test.mjs` + `scripts/checks/language-parity-gate.mjs` 的 `scanRolePromptParity` 与 `scanForbiddenPromptPhrases` 校验。
- **目标合同**：PROVIDER-LANGUAGE-012 保证核心角色双语 Prompt 语义一致同源并彻底清除违规分身表述。
- **影响范围**：`resources/provider/role/`、`scripts/checks/language-parity-gate.mjs`。
