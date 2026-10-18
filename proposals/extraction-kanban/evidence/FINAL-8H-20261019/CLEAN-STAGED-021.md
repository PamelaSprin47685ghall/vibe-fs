# 暂存树的旧历史消费

从本轮暂存树以 `git checkout-index --all --prefix=/private/tmp/final8h-clean-staged/` 导出完整独立文件。被忽略的NDJSON已明确 `git add -f`；导出副本确实存在，没有从活动工作区补文件。

测试 `021.test.mjs` SHA256 `5268b67460a1ed334afa6239c8440474ade19910b71bfe8fb4fc7ebcba447f5a`，NDJSON `2e2e8bd3ba8e30cba169da5b9b8236a6173e8474902ad814dde7051f1c40e6d0`，导出和活动文件逐字节同一。dist与依赖引用先前已核对过的固定706独立安装，产品SHA `8572c309c3c5e64011e51972a70058c044b5cfd1478a650a460e64f2de4d2be2`；没有重编译或改动安装文件，不把复用安装说成新干净npm安装。

在导出根执行 `node --test requirements/interaction-authority/tests/021.test.mjs`，原生日志为3pass、0普通fail、1TODO，exit0。类型解码TODO实际仍失败；Node原生exit0不等于正式完整验收。正式监督148选集的655/0普通fail、6skip95TODO、exit1见ROOT档，不重复累计这三项。

本证明只确认永久用例、真实历史原件和依赖在干净暂存快照可消费；不是新OS生产Host重启、全量冷恢复或GAP124整体完成。非作者已分别核对原件、当前断言和710日志，最后的暂存树执行由root完成。
