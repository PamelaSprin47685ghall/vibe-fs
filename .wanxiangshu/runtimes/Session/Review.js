
import { Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { lambda_type, unit_type, record_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { ReviewVerdict, Fact, ReviewFact, TodoSnapshot, ReviewVerdict_$reflection } from "../Kernel/Fact.js";
import { SessionScript_$reflection } from "./Script.js";
import { SessionError, SessionError_$reflection } from "../Kernel/Outcome.js";
import { Flow_fail, FlowBuilder$2__ReturnFrom_Z3BB19842, FlowBuilder$2__Return_1505, FlowBuilder$2__Bind_Z40B88B2D, FlowBuilder$2__Delay_Z73C1716C, Flow$3_$reflection } from "../Kernel/Flow.js";
import { unwrap } from "../fable_modules/fable-library-js.5.6.0/Option.js";
import { session } from "./SessionFlows.js";
import { equals } from "../fable_modules/fable-library-js.5.6.0/Util.js";

export class ReviewReport extends Record {
    constructor(Text$, Verdict) {
        super();
        this.Text = Text$;
        this.Verdict = Verdict;
    }
}

export function ReviewReport_$reflection() {
    return record_type("Wanxiangshu.Next.Session.Review.ReviewReport", [], ReviewReport, () => [["Text", string_type], ["Verdict", ReviewVerdict_$reflection()]]);
}

export class ReviewerChild extends Record {
    constructor(Review) {
        super();
        this.Review = Review;
    }
}

export function ReviewerChild_$reflection() {
    return record_type("Wanxiangshu.Next.Session.Review.ReviewerChild", [], ReviewerChild, () => [["Review", lambda_type(unit_type, Flow$3_$reflection(SessionScript_$reflection(), SessionError_$reflection(), ReviewReport_$reflection()))]]);
}

export class ReviewScript extends Record {
    constructor(StartReviewer, AcceptVerdict) {
        super();
        this.StartReviewer = StartReviewer;
        this.AcceptVerdict = AcceptVerdict;
    }
}

export function ReviewScript_$reflection() {
    return record_type("Wanxiangshu.Next.Session.Review.ReviewScript", [], ReviewScript, () => [["StartReviewer", lambda_type(unit_type, Flow$3_$reflection(SessionScript_$reflection(), SessionError_$reflection(), ReviewerChild_$reflection()))], ["AcceptVerdict", lambda_type(ReviewReport_$reflection(), Flow$3_$reflection(SessionScript_$reflection(), SessionError_$reflection(), ReviewVerdict_$reflection()))]]);
}

export function acceptVerdict(commit, currentRound, report) {
    return FlowBuilder$2__Delay_Z73C1716C(session, () => {
        const verdict = report.Verdict;
        return FlowBuilder$2__Bind_Z40B88B2D(session, commit(new Fact(4, [new ReviewFact({
            ResultingTodo: unwrap((verdict.tag === 0) ? undefined : ((verdict.tag === 2) ? undefined : (new TodoSnapshot(verdict.fields[0])))),
            Round: currentRound + 1,
            Verdict: verdict,
        })])), () => FlowBuilder$2__Return_1505(session, verdict));
    });
}

export function reviewOnce(r) {
    return FlowBuilder$2__Delay_Z73C1716C(session, () => FlowBuilder$2__Bind_Z40B88B2D(session, r.StartReviewer(), (_arg) => FlowBuilder$2__Bind_Z40B88B2D(session, _arg.Review(), (_arg_1) => FlowBuilder$2__ReturnFrom_Z3BB19842(session, r.AcceptVerdict(_arg_1)))));
}

export function requestValidReview(reviewOnce_1, remaining) {
    return FlowBuilder$2__Delay_Z73C1716C(session, () => ((remaining <= 0) ? FlowBuilder$2__ReturnFrom_Z3BB19842(session, Flow_fail(new SessionError(3, []))) : FlowBuilder$2__Bind_Z40B88B2D(session, reviewOnce_1(), (_arg) => {
        const verdict = _arg;
        switch (verdict.tag) {
            case 0:
            case 1:
                return FlowBuilder$2__Return_1505(session, verdict);
            default:
                return FlowBuilder$2__ReturnFrom_Z3BB19842(session, requestValidReview(reviewOnce_1, remaining - 1));
        }
    })));
}

export function requestReview(s, reviewOnce_1) {
    return FlowBuilder$2__Delay_Z73C1716C(session, () => {
        const reviewView = s.GetReview();
        return ((reviewView.Round >= reviewView.MaxRound) && !equals(reviewView.Verdict, new ReviewVerdict(0, []))) ? FlowBuilder$2__ReturnFrom_Z3BB19842(session, Flow_fail(new SessionError(3, []))) : FlowBuilder$2__Bind_Z40B88B2D(session, requestValidReview(reviewOnce_1, s.Config.MaxInvalidRetries), (_arg) => FlowBuilder$2__Return_1505(session, undefined));
    });
}

