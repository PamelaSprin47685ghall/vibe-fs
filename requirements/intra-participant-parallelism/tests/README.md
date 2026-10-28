# Fission 测试说明

WHAT 是规格。本目录用 `node scripts/build.mjs` 生成当前产物后，经 `requirements/verification-system/tests/run.mjs` 指定本目录编号文件运行；集成用例需 `WXS_TIER_INTEGRATION=1`。

- 001–005：多 lane 身份与责任不变、prompt 数组解析、同 parent 独立 session、全量准入与失败回滚、静默退休。
- 006–009：裂变前 completion 广播、裂变后亲和、keyed 合并、单次 logical completion 与作废结清。
- 010–015、017、018：durable 重放、单 active group、权能来源、subsession 来源、控制面先结算、确定性环收敛、准入条件、成员签名。
- 019：`wanxiangli/src/fission-group` 的 group law 与 lane registry 纯裁决——终态后迟到物理回调被拒绝，不改动已收敛的 terminal。

`support/admission.mjs` 给生产 admission 注入 Host 端口并记录实际调用，不复制准入算法。插件用例运行生产工具与 journal，但 Host 是受控端口，未运行真实模型；`missingIdleTerminalBridgeScenario` 直接给 scheduler 发布投影并 kick，不代表 Host terminal 回调已接通 Fission。
