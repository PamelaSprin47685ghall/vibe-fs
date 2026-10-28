# fatal 清单已退役

本文件只是存档索引，不是规范。现行失败分类见本包 `WHAT.md` 的「封闭失败分类」，fatal 边界见「结算之后才可 fatal」。

旧的 `fatal-inventory.json` 入口索引与 `scripts/checks/fatal-inventory-gate.mjs` 比对门禁已删除：程序树中已无 `fatal-inventory` 符号，`FatalProcess` 的调用点可直接用 `grep -rn "FatalProcess.trip\|FatalProcess.kill" src` 定位。新增进程 fatal 调用点时，在调用处写明触发前提与所属状态即可，不再需要登记编号。
