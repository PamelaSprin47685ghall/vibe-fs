module Wanxiangshu.Methodology.Registry

open Wanxiangshu.Methodology.SchemaCommon
open Wanxiangshu.Methodology.FirstPrinciples
open Wanxiangshu.Methodology.Axiomatization
open Wanxiangshu.Methodology.Deduction
open Wanxiangshu.Methodology.Induction
open Wanxiangshu.Methodology.Abduction
open Wanxiangshu.Methodology.Analogy
open Wanxiangshu.Methodology.Specialization
open Wanxiangshu.Methodology.Generalization
open Wanxiangshu.Methodology.WorkingBackwards
open Wanxiangshu.Methodology.AnalysisSynthesis
open Wanxiangshu.Methodology.AuxiliaryConstruction
open Wanxiangshu.Methodology.EquivalentTransformation
open Wanxiangshu.Methodology.DecompositionRecombination
open Wanxiangshu.Methodology.ModelProblemTransfer
open Wanxiangshu.Methodology.ConstructiveMethod
open Wanxiangshu.Methodology.ReductioAdAbsurdum
open Wanxiangshu.Methodology.Invariance
open Wanxiangshu.Methodology.SymmetryAnalysis
open Wanxiangshu.Methodology.DimensionalReduction
open Wanxiangshu.Methodology.PerturbationContinuity
open Wanxiangshu.Methodology.PigeonholePrinciple
open Wanxiangshu.Methodology.Duality
open Wanxiangshu.Methodology.QuotientSpace
open Wanxiangshu.Methodology.CategoryMapping
open Wanxiangshu.Methodology.Relaxation
open Wanxiangshu.Methodology.SearchSpaceExploration
open Wanxiangshu.Methodology.BranchAndBound
open Wanxiangshu.Methodology.DynamicProgramming
open Wanxiangshu.Methodology.MonteCarloSampling
open Wanxiangshu.Methodology.SimulatedAnnealing
open Wanxiangshu.Methodology.SwarmOptimization
open Wanxiangshu.Methodology.SystemsThinking
open Wanxiangshu.Methodology.RootCauseAnalysis
open Wanxiangshu.Methodology.StateMachineReasoning
open Wanxiangshu.Methodology.TypeDrivenDesign
open Wanxiangshu.Methodology.EventSourcing
open Wanxiangshu.Methodology.Operationalism
open Wanxiangshu.Methodology.BayesianUpdate
open Wanxiangshu.Methodology.Falsification
open Wanxiangshu.Methodology.ThoughtExperiment
open Wanxiangshu.Methodology.TranscendentalArgument
open Wanxiangshu.Methodology.ConceptualAnalysis
open Wanxiangshu.Methodology.DialecticalAnalysis
open Wanxiangshu.Methodology.HermeneuticCircle
open Wanxiangshu.Methodology.Deconstruction
open Wanxiangshu.Methodology.Renormalization
open Wanxiangshu.Methodology.Simplification
open Wanxiangshu.Methodology.TradeoffAnalysis
open Wanxiangshu.Methodology.RiskAnalysis
open Wanxiangshu.Methodology.TestDrivenReasoning
open Wanxiangshu.Methodology.DebuggingTrace
open Wanxiangshu.Methodology.SecurityReview
open Wanxiangshu.Methodology.PerformanceAnalysis
open Wanxiangshu.Methodology.UserIntentClarification

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

let allToolSpecs: Wanxiangshu.Kernel.ToolCatalog.ToolSpec list =
    allSchemas |> List.map toToolCatalogSpec

let tryFindSchema methodologyId =
    allSchemas |> List.tryFind (fun s -> s.methodologyId = methodologyId)

let tryFindToolSpec methodologyId =
    tryFindSchema methodologyId |> Option.map toToolCatalogSpec