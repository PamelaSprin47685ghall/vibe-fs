module VibeFs.Kernel.KnowledgeGraph

type KnowledgeGraphId = VibeFs.Kernel.KnowledgeGraph.Types.KnowledgeGraphId
type KnowledgeGraphEntry = VibeFs.Kernel.KnowledgeGraph.Types.KnowledgeGraphEntry
type KnowledgeGraphDraft = VibeFs.Kernel.KnowledgeGraph.Types.KnowledgeGraphDraft
type KnowledgeGraphHeader = VibeFs.Kernel.KnowledgeGraph.Types.KnowledgeGraphHeader
type KnowledgeGraphFile = VibeFs.Kernel.KnowledgeGraph.Types.KnowledgeGraphFile
type KnowledgeGraphProjection = VibeFs.Kernel.KnowledgeGraph.Types.KnowledgeGraphProjection
type KnowledgeGraphJobKind = VibeFs.Kernel.KnowledgeGraph.Types.KnowledgeGraphJobKind
type KnowledgeGraphJobContext = VibeFs.Kernel.KnowledgeGraph.Types.KnowledgeGraphJobContext

let tryParseId = VibeFs.Kernel.KnowledgeGraph.Id.tryParseId
let idValue = VibeFs.Kernel.KnowledgeGraph.Id.idValue
let allocateRandomHexId = VibeFs.Kernel.KnowledgeGraph.Id.allocateRandomHexId
let renderJobMarker = VibeFs.Kernel.KnowledgeGraph.Job.renderJobMarker
let prependJobMarker = VibeFs.Kernel.KnowledgeGraph.Job.prependJobMarker
let tryParseJobMarker = VibeFs.Kernel.KnowledgeGraph.Job.tryParseJobMarker
let projectLatestWins = VibeFs.Kernel.KnowledgeGraph.Projection.projectLatestWins
let buildPreludeSection = VibeFs.Kernel.KnowledgeGraph.Projection.buildPreludeSection
let validateDraft = VibeFs.Kernel.KnowledgeGraph.Draft.validateDraft
let applyDrafts = VibeFs.Kernel.KnowledgeGraph.Draft.applyDrafts
let fetchAnswer = VibeFs.Kernel.KnowledgeGraph.Fetch.fetchAnswer
let returnBookkeeperToolName = VibeFs.Kernel.KnowledgeGraph.Idempotency.returnBookkeeperToolName
let historyHasCompletedReturnBookkeeper = VibeFs.Kernel.KnowledgeGraph.Idempotency.historyHasCompletedReturnBookkeeper
let rejectSecondReturnBookkeeperMessage = VibeFs.Kernel.KnowledgeGraph.Idempotency.rejectSecondReturnBookkeeperMessage