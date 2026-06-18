module VibeFs.Kernel.FuzzySearch

type ResolvedFuzzySearchPath = FuzzyQuery.ResolvedFuzzySearchPath

let normalizePathConstraint = FuzzyQuery.normalizePathConstraint
let normalizeExcludes = FuzzyQuery.normalizeExcludes
let buildQuery = FuzzyQuery.buildQuery
let resolveFuzzySearchPath = FuzzyQuery.resolveFuzzySearchPath
let resolveExternalPath = FuzzyQuery.resolveExternalPath

let hotFrecency = FuzzyFormat.hotFrecency
let warmFrecency = FuzzyFormat.warmFrecency
let grepMaxLineLength = FuzzyFormat.grepMaxLineLength

type FileAnnotation = FuzzyFormat.FileAnnotation
type GrepMatch = FuzzyFormat.GrepMatch
type GrepResult = FuzzyFormat.GrepResult
type FindMatch = FuzzyFormat.FindMatch
type FindResult = FuzzyFormat.FindResult

let truncateLine = FuzzyFormat.truncateLine
let fileAnnotation = FuzzyFormat.fileAnnotation
let formatGrepOutput = FuzzyFormat.formatGrepOutput
let formatFindOutput = FuzzyFormat.formatFindOutput

let detectGrepMode = FuzzyGrepDetect.detectGrepMode
let checkWildcardOnly = FuzzyGrepDetect.checkWildcardOnly
