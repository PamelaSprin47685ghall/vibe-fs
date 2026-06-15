# REF45: arXiv API 集成 (ArxivAPI.ts)

## 1. 功能

提供 arXiv 学术论文搜索和 PDF 文本提取能力，用于 Agentic 模式的 `searchacademia` 和 `searchacademia_and` 工具。

## 2. 搜索 API

### 搜索类型
| 类型 | 说明 | 查询构建 |
|------|------|----------|
| `simple` | 单查询 | `all:{query}` |
| `and_terms` | 多术语 AND | `all:{term1}+AND+all:{term2}` |

### API 端点
`https://export.arxiv.org/api/query?search_query={query}&start={start}&max_results={max}`

### XML 解析
使用 `DOMParser` 解析 arXiv API 的 XML 响应：
- `entry` → 每篇论文
- `title`, `author → name`, `summary`
- `published`, `updated`
- `category[term]`
- `link[type=application/pdf]`, `link[rel=alternate]`
- `arxiv:journal_ref`, `arxiv:doi`

## 3. 论文类型

```typescript
interface ArxivPaper {
    id: string
    title: string
    authors: string[]
    abstract: string
    published: string
    updated: string
    categories: string[]
    pdfUrl: string
    arxivUrl: string
    journalRef?: string
    doi?: string
}
```

## 4. PDF 提取

```typescript
fetchPaperPDF(pdfUrl: string): Promise<string>
```

使用 `pdfjs-dist` 库：
1. 获取 PDF 二进制数据
2. 使用 pdf.js 加载文档
3. 逐页提取文本
4. 清理空白和换行

## 5. 格式化展示

```typescript
formatPaperForDisplay(paper: ArxivPaper): string
// 返回适合 Agentic 模式 UI 的纯文本格式
```
