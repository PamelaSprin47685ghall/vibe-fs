# 019/018 变异测试构造说明

本文件仅记录 `tests/019.test.mjs` 与 `tests/018.test.mjs` 两条测试的构造思路，供需要修改这两个测试的人参考；不是规范，不是验收证据。现行规格见 [../../WHAT.md](../../WHAT.md) 的 [019] 与 [018]。

- 019 用 held terminal barrier 观察 delete/cancel 排空时序：lease 计数断言守「release 不得先于 durable terminal」，completed 布尔断言守「完成发布必须等待排空」，decoy 会话断言守「结算只限本 session」。
- 018 用四场景（turn-aborted、provider-retry、fission-external-abort、unknown-stop）断言 stop 按真实语义结算，永不把观察性停止升级成 Abandoned。
- 串行 drain 按字典序取第一个 key：fixture 命名决定 held 期间哪个 key 可观察，改命名前先确认断言锚。
