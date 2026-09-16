# external-investigation — WHY

## 演进背景与撤销声明

`external-investigation` 规范包已正式废止，其历史条款全部撤销。

历史设计中曾引入 Browser 角色与专属的 `stealth-browser-mcp` 集成，用于在公开网络环境中开展网页调查与外部事实溯源（EXTERNAL-INVESTIGATION-001 ~ 011）。在角色收拢与工程职责整合方案中，系统确立了以下决策：

1. **Browser 角色与集成全链清除**：删除 Browser 角色及仓库拥有的 Browser MCP 集成文件、配置、环境变量与角色提示词，未来专业网络工具若有需求另立独立规范，不在本次重构中保留空壳代理或兼容分支；
2. **职责不转交任何人**：外部网络调查职责彻底撤销，不转交给 Engineer、DevOps、Manager 或任何其他角色。Engineer 专注于本地代码事实与实现，DevOps 专注于构建执行与环境运维；
3. **正常构建依赖拉取与外部调查的负向边界**：DevOps 在执行已有项目构建、测试流程时的正常网络依赖下载（例如 `npm install`、`cargo fetch`、`git submodule update` 等标准构建工具命令），属于既有工程行为，不等于承担 Browser 职责；但**严禁**任何角色通过 `curl`、`wget`、通用 shell 脚本或临时 JS 包装复活外部网络抓取与事实调查角色。

本包保留文档作为历史演进记录，不建兼容 facade。

## 依赖关系

本包已废止，无活跃依赖。
