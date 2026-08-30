# provider-language — HOW

## 架构与实现机制

1. **类型绑定与继承模型**：
   - owner-local `ProviderLanguage`（`English | SimplifiedChinese`）定义语言值与解析策略，`SessionProviderLanguage` 负责会话的 bind-once、继承与查询策略。
   - raw-observation Host `ProviderLanguageBinding` 仅把主机会话文本或全局偏好转换为 owner 策略调用：根会话通过 `ensureRoot` 绑定，子会话通过 `ensureInherited` 复制父级语言，拒绝二次读取全局环境。

2. **资源加载管线（Class A 入口）**：
   - owner-local `ProviderResources` 通过 `relativePath`、`requireLanguagePair` 与 `readText` 唯一定位和读取领域文本；`ProviderResourceBytes` 是窄物理适配器，仅把 owner 已批准的相对路径交给 `PackageResources`。
   - owner-local `ProviderProse` 提供模板渲染与安全替换（`render`、`substitute`），确保无残留占位符并移交投影层。
   - 加载缺失任一语言文件时直接抛出异常，杜绝静默回退。

3. **结构对称性与防退化门禁**：
   - `language-parity-gate` 检查资源文件成对存在、占位符集合一致、标识符不翻译以及语义锚点双语覆盖。
   - `provider-prose-ownership` 扫描源码，禁止在业务逻辑中硬编码散落的自然语言字面量。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PROVIDER-LANGUAGE-001 | `requirements/provider-language/tests/provider-language.test.mjs::WHAT[PROVIDER-LANGUAGE-001] ProviderLanguage parses en and zh-CN with locale mapping` |
| PROVIDER-LANGUAGE-002 | `requirements/provider-language/tests/provider-language.test.mjs::WHAT[PROVIDER-LANGUAGE-002] bind once is immutable and conflicting rebind fails closed` |
| PROVIDER-LANGUAGE-003 | `requirements/provider-language/tests/provider-language.test.mjs::WHAT[PROVIDER-LANGUAGE-003] child inherits owner language without re-reading global` |
| PROVIDER-LANGUAGE-004 | `requirements/provider-language/tests/provider-prose-and-preference.test.mjs::WHAT[PROVIDER-LANGUAGE-004] preference change only affects future sessions` |
| PROVIDER-LANGUAGE-005 | `requirements/provider-language/tests/provider-prose-ownership.test.mjs::WHAT[PROVIDER-LANGUAGE-005] heuristic excludes paths and identifiers from Class A` |
| PROVIDER-LANGUAGE-006 | `requirements/provider-language/tests/provider-prose-and-preference.test.mjs::WHAT[PROVIDER-LANGUAGE-006] require language pair fails closed on missing semantic path` |
| PROVIDER-LANGUAGE-007 | `requirements/provider-language/tests/provider-prose-and-preference.test.mjs::WHAT[PROVIDER-LANGUAGE-007] substitute replaces values and fails closed on missing or leftover` |
| PROVIDER-LANGUAGE-008 | `requirements/provider-language/tests/provider-language.test.mjs::WHAT[PROVIDER-LANGUAGE-008] bound language loads its own locale leaf` |
| PROVIDER-LANGUAGE-009 | `requirements/provider-language/tests/provider-prose-ownership.test.mjs::WHAT[PROVIDER-LANGUAGE-009] zero hits is closed` |
| PROVIDER-LANGUAGE-010 | `requirements/provider-language/tests/language-parity-gate.test.mjs::WHAT[PROVIDER-LANGUAGE-010] semantic anchor parity detects missing zh id` |
| PROVIDER-LANGUAGE-011 | `requirements/provider-language/tests/language-parity-gate.test.mjs::WHAT[PROVIDER-LANGUAGE-011] identifier parity mismatch reports semantic and diff` |
