glob(pattern) 以 gitignore/wildmatch 语义在当前路径边界下枚举文件。* 不跨
/。** 匹配零段或多段目录。pattern 不含 / 时匹配任意深度（*.md 匹配每个
.md 文件）。{a,b} 展开为交替。结果省略 .git、省略 gitignored 路径、不
跟随符号链接，并已排序。返回值是 { paths }。glob
不授予 Read。
