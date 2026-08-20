# cognitive-environment — HOW

## 架构与核心机制

`cognitive-environment` 通过标准化的提示词资源加载器组装模型视野：

```text
PromptResources.systemForRole (语言 lang, 角色 role)
       │
       ├──► Common Law (通用世界观)
       ├──► Role Law (role 专属自我模型)
       └──► Office Library (依据 libraryPaths 引入对应的继承卷与闭卷)
```

1. **五层流水线组合**：
   - `PromptResources` 作为唯一提示词源，强制执行 EN/zh-CN 双语成对存在与锚点一致性检查（`ensureParity`）。
   - System Prompt 仅包含身份与知识层，Tools 描述由 ToolRegistry 独立注入，Runtime 与 Mission 材料通过会话上下文传递。

2. **Pair Hint 注入机制**：
   - 结对提示词通过 HOST-013 机制以合成 `skill` 内容的形式注入模型输入前沿，保证协作纪律、就绪前沿无阻塞并发以及假设承诺规则的高显著性。
   - 对特定白名单模型的局部辅助提示（如 Blogger 的 chronicle-direct text nudge）仅在当次 transform 阶段临时注入并随后清除。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| COGNITIVE-ENVIRONMENT-001 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-002 | `requirements/cognitive-environment/tests/semantic-anchor-parity.test.mjs` |
| COGNITIVE-ENVIRONMENT-003 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-004 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-005 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-006 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-007 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-008 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-009 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-010 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-011 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-012 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-013 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-014 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
| COGNITIVE-ENVIRONMENT-015 | `requirements/cognitive-environment/tests/blogger-chronicle-text.test.mjs` |
| COGNITIVE-ENVIRONMENT-016 | `requirements/cognitive-environment/tests/cognitive-environment.test.mjs` |
