module Wanxiangshu.Kernel.KnowledgeGraph

type KnowledgeGraphId = Wanxiangshu.Kernel.KnowledgeGraph.Types.KnowledgeGraphId
type KnowledgeGraphEntry = Wanxiangshu.Kernel.KnowledgeGraph.Types.KnowledgeGraphEntry
type KnowledgeGraphDraft = Wanxiangshu.Kernel.KnowledgeGraph.Types.KnowledgeGraphDraft
type KnowledgeGraphHeader = Wanxiangshu.Kernel.KnowledgeGraph.Types.KnowledgeGraphHeader
type KnowledgeGraphFile = Wanxiangshu.Kernel.KnowledgeGraph.Types.KnowledgeGraphFile
type KnowledgeGraphProjection = Wanxiangshu.Kernel.KnowledgeGraph.Types.KnowledgeGraphProjection
type KnowledgeGraphJobKind = Wanxiangshu.Kernel.KnowledgeGraph.Types.KnowledgeGraphJobKind
type KnowledgeGraphJobContext = Wanxiangshu.Kernel.KnowledgeGraph.Types.KnowledgeGraphJobContext

let tryParseId = Wanxiangshu.Kernel.KnowledgeGraph.Id.tryParseId
let idValue = Wanxiangshu.Kernel.KnowledgeGraph.Id.idValue
let allocateRandomHexId = Wanxiangshu.Kernel.KnowledgeGraph.Id.allocateRandomHexId
let renderJobMarker = Wanxiangshu.Kernel.KnowledgeGraph.Job.renderJobMarker
let prependJobMarker = Wanxiangshu.Kernel.KnowledgeGraph.Job.prependJobMarker
let tryParseJobMarker = Wanxiangshu.Kernel.KnowledgeGraph.Job.tryParseJobMarker
let projectLatestWins = Wanxiangshu.Kernel.KnowledgeGraph.Projection.projectLatestWins
let buildPreludeSection = Wanxiangshu.Kernel.KnowledgeGraph.Projection.buildPreludeSection
let validateDraft = Wanxiangshu.Kernel.KnowledgeGraph.Draft.validateDraft
let applyDrafts = Wanxiangshu.Kernel.KnowledgeGraph.Draft.applyDrafts
let fetchAnswer = Wanxiangshu.Kernel.KnowledgeGraph.Fetch.fetchAnswer
let returnBookkeeperToolName = Wanxiangshu.Kernel.KnowledgeGraph.Idempotency.returnBookkeeperToolName
let historyHasCompletedReturnBookkeeper = Wanxiangshu.Kernel.KnowledgeGraph.Idempotency.historyHasCompletedReturnBookkeeper
let rejectSecondReturnBookkeeperMessage = Wanxiangshu.Kernel.KnowledgeGraph.Idempotency.rejectSecondReturnBookkeeperMessage