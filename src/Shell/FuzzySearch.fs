module VibeFs.Shell.FuzzySearch

type FinderLike = FuzzyFinderShell.FinderLike
type FinderCache = FuzzyFinderShell.FinderCache

let resultFromRaw = FuzzyFinderShell.resultFromRaw
let createFinder = FuzzyFinderShell.createFinder

type FuzzyFindParams = FuzzyCoordinator.FuzzyFindParams
type FuzzyGrepParams = FuzzyCoordinator.FuzzyGrepParams
type SearchOptions = FuzzyCoordinator.SearchOptions
type FuzzyFindState = FuzzyCoordinator.FuzzyFindState
type FuzzyGrepState = FuzzyCoordinator.FuzzyGrepState
type SearchOutcome = FuzzyCoordinator.SearchOutcome

let resolveStore = FuzzyCoordinator.resolveStore
let parseExcludeField = FuzzyCoordinator.parseExcludeField
let resolveFindSearchState = FuzzyCoordinator.resolveFindSearchState
let resolveGrepSearchState = FuzzyCoordinator.resolveGrepSearchState
let acquireFinderFromOptions = FuzzyCoordinator.acquireFinderFromOptions
let releaseFinder = FuzzyCoordinator.releaseFinder

let optStr = FuzzyRawMapping.optStr
let optInt = FuzzyRawMapping.optInt
let itemsOf = FuzzyRawMapping.itemsOf
let stringListOf = FuzzyRawMapping.stringListOf
let annotationOf = FuzzyRawMapping.annotationOf
let toFindMatch = FuzzyRawMapping.toFindMatch
let toGrepMatch = FuzzyRawMapping.toGrepMatch

type ResolvedGrep = FuzzyGrepCmd.ResolvedGrep

let findNextIterator = FuzzyFindCmd.findNextIterator
let fuzzyFind = FuzzyFindCmd.fuzzyFind
let resolveResult = FuzzyGrepCmd.resolveResult
let buildGrepOutput = FuzzyGrepCmd.buildGrepOutput
let fuzzyGrep = FuzzyGrepCmd.fuzzyGrep
