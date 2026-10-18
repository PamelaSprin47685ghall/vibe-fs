# 八小时收尾证据

本目录补充各工作包原有证据，不能替代它们或关闭父项。产品固定706/710，SHA256 `8572c309c3c5e64011e51972a70058c044b5cfd1478a650a460e64f2de4d2be2`。2076文件完全相同，两个tar也逐字节同一。

| 文件 | SHA256 | 证明范围 |
| --- | --- | --- |
| `ROOT-VERIFICATION-710.tgz` | `3fb565e7bc0a15d46f34408fdc9367f159b1d3d7eacf3dccc2ae150a5e3b332b` | 708–710构建、148文件完整选集、709沙箱阻断/夹具首红、710最终655/0普通fail+6skip95TODO、收尾accepted、源码原件快照、pack及706/710相同回执 |
| `GAP124-TRUE667-PHYSICAL-706-RAW-AUDIT-20261019.tgz` | `6e76d73ab87f72df7978a8d8ef6cb7c51827e50e9b25cdd3e077a80997377ddf` | 真实667 producer与706新OS reader、完整物理种子/实际cut、原断言的当前live append强红、旧首test绿、两份确切npm包及各2076安装文件核对、程序与原日志 |
| `D1-D2-LIB012-REVIEW.md` | `aee54eaf15c2a39685959fe45a4177cff6e9bbd0c17afcbcd8f76142d646c2e5` | 非作者源码边界复核；未新增测试通过数 |
| `B1-DELIVERY-AUDIT.md` | `120393e51bf836f7deebee0684c2333dde00173bcce3f1dd72febba5df092fe6` | 非作者实际核十外档、1454条manifest及700/706只有一个产品文件变化；两处过期GAP描述已由root同步 |

历史原件为904字节、两行，SHA256 `2e2e8bd3ba8e30cba169da5b9b8236a6173e8474902ad814dde7051f1c40e6d0`，长期用例已保存同字节副本。667的append是带cut成功，不能称旧authority已解码；706的raw读取成功也不等于FactCodec或活跃身份许可。两版live append差异归680的有意收紧，先前“raw reader缺失”review泛化已撤回。

[暂存树消费](CLEAN-STAGED-021.md)及[原生日志](CLEAN-STAGED-021.log)证明被忽略的NDJSON已实际进入可消费快照：3pass/1TODO，不当作正式整套绿或新npm安装。

`run.mjs`、`worker.mjs`与fixture-metadata在历史档内。其生产/冷读用两个不同OS进程。复跑需私有安装两份原tar及明确peer，按档内README调用；本目录不声称依赖closure离线自包含。R12-U1未重启调查，registry/Host未部署。
