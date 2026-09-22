namespace Wanxiangshu.Sphinx

type TurnExpectation =
    { ExpectedTurns: int
      TurnPrice: float
      CalibrationSamples: int }

type TurnCalibration =
    { Samples: int
      SumLogCoefficient: float }

module TurnBudget =
    let minimum = 5
    let safetyLimit = 512
    let maximum = safetyLimit - 1
    let defaultExpected = 12

    let validate (expected: int) =
        if
            expected < minimum
            || expected > maximum
            || System.Double.IsNaN(float expected)
            || floor (float expected) <> float expected
        then
            Error(sprintf "expectTurns must be an integer from %d to %d for a complete inquiry" minimum maximum)
        else
            Ok expected

    // Legacy reference loss: L(K)=0.72/(K+1), T=2K+3.
    // Minimizing L(T)+lambda*T yields lambda=1.44/(T-1)^2.
    let initialPrice (expected: int) = 1.44 / (float (expected - 1) ** 2.0)

    let emptyCalibration = { Samples = 0; SumLogCoefficient = 0.0 }

    let expectation (expected: int) (calibration: TurnCalibration) : TurnExpectation =
        let logCoefficient =
            (log 1.44 + calibration.SumLogCoefficient) / float (calibration.Samples + 1)

        { ExpectedTurns = expected
          TurnPrice = exp (max -32.0 (min 32.0 logCoefficient)) / (float (expected - 1) ** 2.0)
          CalibrationSamples = calibration.Samples }

    // A price-limited completed run observes A=lambda*(T-1)^2 in
    // T=1+sqrt(A/lambda). Log sufficient statistics are pooled only within
    // one question/target. The reference coefficient is one prior sample.
    let observe (expectation: TurnExpectation) (usedTurns: int) (calibration: TurnCalibration) =
        { Samples = calibration.Samples + 1
          SumLogCoefficient =
            calibration.SumLogCoefficient
            + log expectation.TurnPrice
            + 2.0 * log (float (max 2 usedTurns - 1)) }
