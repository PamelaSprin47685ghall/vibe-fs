# guessed-migration — Main

停止 runtime archaeology，先建立 schema provenance。

理想状态是 durable record 自己带明确 version/generation，migration 只接受一个已知 predecessor：

```text
V1 bytes --migrateV1toV2--> V2
V2 bytes --migrateV2toV3--> V3
```

每个 transform 都应 deterministic、可用真实历史 fixture 测试，并在成功后把新 version 一起 durable 写回。

如果 old bytes 本身没有 version，不要继续堆 shape heuristic。选择更诚实的路径：

- 能从其他 authoritative metadata 证明版本，就用那份 evidence；
- 能限定成一批 known export/import，做一次 operator-authorized conversion；
- 仍然 ambiguous，就 fail closed / quarantine / require explicit decision。

常见假修复：

- 再加几个 `if field exists` 让 fixtures 都能过；
- latest parser 能 parse 就当 latest schema；
- unknown 默认映成 newest version；
- migration 出错时吞掉 field、补 default，产出“看起来完整”的对象；
- 每次 recovery 都 best-effort 猜，而不是一次转换后固定 provenance；
- 把 heuristic 结果写进 cache，却不更新真正 durable schema identity。

验证要使用**真实旧版本 bytes**，而不是根据今天 type 反向生成一个自称 v1 的 fixture。每个 supported historical version 都应有代表性 artifact 与预期 semantic result。

还要专门测 ambiguous shape：两个版本都能 parse 的 bytes 必须由 version identity 决定，不能看哪个 parser 先成功。Unknown version 应明确失败，而不是误读成“最接近的 known version”。

如果历史债来自 `unversioned-schema`，修当前 writer 同时记录 version，防止继续新增 ambiguous data；旧债则单独 migration，不要让 compatibility 永久污染 hot recovery path。

完成时每次 migration 都能回答“我从哪个已证明的旧语言出发”，而不是“我看这些字段像某个版本”。

> 历史允许缺信息，但不允许今天的代码为了顺利启动就替过去编造信息。