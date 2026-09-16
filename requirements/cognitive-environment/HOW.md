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

2. **装配位置（结构，不改变行为）**：`PromptCatalog`（9 个 prompt 字段的纯数据 record）声明在纯数据 contract 分片 `resources/resources-promptcatalog`（零 ProjectReference）；提示词/规则书的**物理装载**（`PromptResources.load*`、`EnforcerCatalogResource.loadFor`、`PackageResources.readFileSync`）只允许由 composition 分片 `enforcer/enforcer-catalog-resource` 的 `RuntimeResourceAssembly.load`/`loadFor` 执行，enforcer 的 runtime 分片 `resources-runtimeresources` 只持有装载结果的 record 与 `install`/`current`/`enforcerRulesFor` 访问器。业务分片不得为了拿提示词而直接调用 `PromptResources`/`PackageResources` 读文件（013/014）。
3. **Pair Hint 注入机制**：
   - 结对提示词通过 HOST-013 机制以合成 `skill` 内容的形式注入模型输入前沿，保证协作纪律、就绪前沿无阻塞并发，并仅用一句高显著性提示提醒复杂非线性工作可转入 `assume` jq 画板；完整画板方法与持久化语义留在工具描述，避免每轮重复灌输。
   - 对特定白名单模型的局部辅助提示（如 Blogger 的 chronicle-direct text nudge）仅在当次 transform 阶段临时注入并随后清除。
