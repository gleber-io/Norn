import { Link } from "react-router";
import { QueryState } from "@/components/query-state";
import { usePlansQuery } from "@/lib/queries";

const STATUS_CLASS: Record<string, string> = {
  Llm: "text-purple-600 dark:text-purple-400",
  RuleEngine: "text-blue-600 dark:text-blue-400",
  Fallback: "text-amber-600 dark:text-amber-400",
};

export function PlansListPage() {
  const plansQuery = usePlansQuery(100);
  const plans = plansQuery.data ?? [];

  return (
    <QueryState
      isLoading={plansQuery.isLoading}
      isError={plansQuery.isError}
      error={plansQuery.error}
      isEmpty={plans.length === 0}
      emptyMessage="Nenhum plano decidido ainda."
    >
      <ul className="flex flex-col divide-y divide-border rounded-lg border border-border">
        {plans.map((plan) => (
          <li key={plan.planId}>
            <Link
              to={`/plans/${plan.planId}`}
              className="flex items-center justify-between px-4 py-3 hover:bg-accent"
            >
              <div>
                <div className="font-medium">{plan.actions?.[0]?.type ?? "NoOp"}</div>
                <div className="text-sm text-muted-foreground">{plan.rationale}</div>
              </div>
              <span className={STATUS_CLASS[plan.decidedBy]}>{plan.decidedBy}</span>
            </Link>
          </li>
        ))}
      </ul>
    </QueryState>
  );
}
