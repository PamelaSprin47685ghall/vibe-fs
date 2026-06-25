module VibeFs.Tests.KernelPromptSpecs

open VibeFs.Tests.KernelPromptSpecsHost
open VibeFs.Tests.KernelPromptSpecsSubagent
open VibeFs.Tests.KernelPromptSpecsReview

let hostKernel' () = KernelPromptSpecsHost.hostKernel' ()
let toolCatalogCentralized () = KernelPromptSpecsHost.toolCatalogCentralized ()
let hostToolsKnowledgeGraphNames () = KernelPromptSpecsHost.hostToolsKnowledgeGraphNames ()
let subagentDispatch () = KernelPromptSpecsSubagent.subagentDispatch ()
let subagentJoinReports () = KernelPromptSpecsSubagent.subagentJoinReports ()
let mimocodeFormatPromptAppendsAgentReportTail () = KernelPromptSpecsSubagent.mimocodeFormatPromptAppendsAgentReportTail ()
let loopMessagesShared () = KernelPromptSpecsReview.loopMessagesShared ()
let reviewerVerdictPromptsShared () = KernelPromptSpecsReview.reviewerVerdictPromptsShared ()
let reviewResultFormattingShared () = KernelPromptSpecsReview.reviewResultFormattingShared ()
let domainErrorsShared () = KernelPromptSpecsReview.domainErrorsShared ()
let reviewVerdictDecode () = KernelPromptSpecsReview.reviewVerdictDecode ()
let reviewDecisionPolicy () = KernelPromptSpecsReview.reviewDecisionPolicy ()
let reviewMarkdownCodec () = KernelPromptSpecsReview.reviewMarkdownCodec ()
let executorSummarizerNoExitStatus () = KernelPromptSpecsReview.executorSummarizerNoExitStatus ()