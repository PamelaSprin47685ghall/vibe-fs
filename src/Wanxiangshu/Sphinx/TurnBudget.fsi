namespace Wanxiangshu.Sphinx

type TurnExpectation =
    { ExpectedTurns: int
      TurnPrice: float
      CalibrationSamples: int }

type TurnCalibration =
    { Samples: int
      SumLogCoefficient: float }

module TurnBudget =
    val minimum: int
    val maximum: int
    val defaultExpected: int
    val safetyLimit: int
    val validate: expected: int -> Result<int, string>
    val initialPrice: expected: int -> float
    val emptyCalibration: TurnCalibration
    val expectation: expected: int -> calibration: TurnCalibration -> TurnExpectation
    val observe: expectation: TurnExpectation -> usedTurns: int -> calibration: TurnCalibration -> TurnCalibration
