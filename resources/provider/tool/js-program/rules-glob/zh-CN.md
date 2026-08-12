glob(pattern) 在当前路径边界下按 gitignore/wildmatch 语义枚举文件。
* 不跨越 /。** 匹配零段或多段目录。
不含斜杠的 pattern 匹配任意深度（*.md 命中每一个 .md 文件）。
{a,b} 展开为交替。结果省略 .git、省略 gitignored 路径、不
跟随符号链接，并已排序。返回值是 { paths, truncated }。
界限打在匹配条数；截断时 truncated 为 true。glob
不授予 Read。
