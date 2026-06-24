module VibeFs.Methodology.Registry

open VibeFs.Methodology.SchemaCommon
open VibeFs.Methodology.FirstPrinciples
open VibeFs.Methodology.Axiomatization
open VibeFs.Methodology.Deduction
open VibeFs.Methodology.Induction
open VibeFs.Methodology.Abduction
open VibeFs.Methodology.Analogy
open VibeFs.Methodology.Specialization
open VibeFs.Methodology.Generalization
open VibeFs.Methodology.WorkingBackwards
open VibeFs.Methodology.AnalysisSynthesis
open VibeFs.Methodology.AuxiliaryConstruction
open VibeFs.Methodology.EquivalentTransformation
open VibeFs.Methodology.DecompositionRecombination
open VibeFs.Methodology.ModelProblemTransfer
open VibeFs.Methodology.ConstructiveMethod
open VibeFs.Methodology.ReductioAdAbsurdum
open VibeFs.Methodology.Invariance
open VibeFs.Methodology.SymmetryAnalysis
open VibeFs.Methodology.DimensionalReduction
open VibeFs.Methodology.PerturbationContinuity
open VibeFs.Methodology.PigeonholePrinciple
open VibeFs.Methodology.Duality
open VibeFs.Methodology.QuotientSpace
open VibeFs.Methodology.CategoryMapping
open VibeFs.Methodology.Relaxation
open VibeFs.Methodology.SearchSpaceExploration
open VibeFs.Methodology.BranchAndBound
open VibeFs.Methodology.DynamicProgramming
open VibeFs.Methodology.MonteCarloSampling
open VibeFs.Methodology.SimulatedAnnealing
open VibeFs.Methodology.SwarmOptimization
open VibeFs.Methodology.SystemsThinking
open VibeFs.Methodology.RootCauseAnalysis
open VibeFs.Methodology.StateMachineReasoning
open VibeFs.Methodology.TypeDrivenDesign
open VibeFs.Methodology.EventSourcing
open VibeFs.Methodology.Operationalism
open VibeFs.Methodology.BayesianUpdate
open VibeFs.Methodology.Falsification
open VibeFs.Methodology.ThoughtExperiment
open VibeFs.Methodology.TranscendentalArgument
open VibeFs.Methodology.ConceptualAnalysis
open VibeFs.Methodology.DialecticalAnalysis
open VibeFs.Methodology.HermeneuticCircle
open VibeFs.Methodology.Deconstruction
open VibeFs.Methodology.Renormalization
open VibeFs.Methodology.Simplification
open VibeFs.Methodology.TradeoffAnalysis
open VibeFs.Methodology.RiskAnalysis
open VibeFs.Methodology.TestDrivenReasoning
open VibeFs.Methodology.DebuggingTrace
open VibeFs.Methodology.SecurityReview
open VibeFs.Methodology.PerformanceAnalysis
open VibeFs.Methodology.UserIntentClarification

let allSchemas: MethodologySchema list =
    [ FirstPrinciples.schema
      Axiomatization.schema
      Deduction.schema
      Induction.schema
      Abduction.schema
      Analogy.schema
      Specialization.schema
      Generalization.schema
      WorkingBackwards.schema
      AnalysisSynthesis.schema
      AuxiliaryConstruction.schema
      EquivalentTransformation.schema
      DecompositionRecombination.schema
      ModelProblemTransfer.schema
      ConstructiveMethod.schema
      ReductioAdAbsurdum.schema
      Invariance.schema
      SymmetryAnalysis.schema
      DimensionalReduction.schema
      PerturbationContinuity.schema
      PigeonholePrinciple.schema
      Duality.schema
      QuotientSpace.schema
      CategoryMapping.schema
      Relaxation.schema
      SearchSpaceExploration.schema
      BranchAndBound.schema
      DynamicProgramming.schema
      MonteCarloSampling.schema
      SimulatedAnnealing.schema
      SwarmOptimization.schema
      SystemsThinking.schema
      RootCauseAnalysis.schema
      StateMachineReasoning.schema
      TypeDrivenDesign.schema
      EventSourcing.schema
      Operationalism.schema
      BayesianUpdate.schema
      Falsification.schema
      ThoughtExperiment.schema
      TranscendentalArgument.schema
      ConceptualAnalysis.schema
      DialecticalAnalysis.schema
      HermeneuticCircle.schema
      Deconstruction.schema
      Renormalization.schema
      Simplification.schema
      TradeoffAnalysis.schema
      RiskAnalysis.schema
      TestDrivenReasoning.schema
      DebuggingTrace.schema
      SecurityReview.schema
      PerformanceAnalysis.schema
      UserIntentClarification.schema ]

let allToolSpecs: VibeFs.Kernel.ToolCatalog.ToolSpec list =
    allSchemas |> List.map toToolCatalogSpec

let tryFindSchema methodologyId =
    allSchemas |> List.tryFind (fun s -> s.methodologyId = methodologyId)

let tryFindToolSpec methodologyId =
    tryFindSchema methodologyId |> Option.map toToolCatalogSpec