module Wanxiangshu.Runtime.PromptFrontMatter

open Wanxiangshu.Runtime.PromptHeader

type FrontMatterField = HeaderField

let yamlField = yamlField
let yamlStringSeqField = yamlStringSeqField
let yamlSeqField = yamlSeqField
let frontMatter = promptHeader
let frontMatterPrompt = promptHeaderPrompt
let frontMatterRoot = promptHeaderRoot
let frontMatterPromptRoot = promptHeaderPromptRoot
let stringifyFields = stringifyFields
