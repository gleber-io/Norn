-- Fase 12: extrai os três CSV que campaign_metrics.py consome (decisions.csv, loop-latency.csv,
-- mttd.csv) direto do Knowledge. Não há ferramenta .NET dedicada para isto — são consultas de
-- leitura pura sobre tabelas já existentes, sem lógica de negócio nova (mesmo raciocínio que já
-- isenta tools/analysis/ de ser código de produto, aplicado aqui a um passo antes: extrair).
--
-- Uso: psql -U norn -d norn -f export-campaign-metrics.sql, depois copiar os três arquivos de
-- /tmp (dentro do container norn-postgres, se rodando via docker exec) para tools/analysis/data/.
-- Troque 20260919 (randomization_seed da campanha real) se for extrair outra campanha.
--
-- \copy precisa do comando inteiro numa linha só (é meta-comando do psql, não SQL) — por isso as
-- três consultas abaixo, embora densas, não têm quebra de linha dentro dos parênteses.
--
-- Achado ao escrever a consulta de MTTD: restringir a asig.detected_at_utc >= er.onset_at_utc é
-- obrigatório — sem isso, MIN(detected_at_utc) pega o sinal mais antigo do experiment_run_id
-- inteiro, incluindo sinais do warmup/pré-injeção (MTTD saía negativo em minutos, não segundos).
--
-- Achado ao escrever a consulta de decisões: HealingPlan.DecidedBy == 'Fallback' não significa "o
-- LLM falhou" — o braço C também usa esse valor quando o RuleEngine rejeita a própria ação
-- candidata por pré-condição. O sinal real de falha do LLM é jsonb_array_length(failureReasons) > 0,
-- por isso a coluna vem exportada explicitamente em vez de derivada de decided_by.

\copy (select er.run_order, er.scenario, er.arm, er.termination_state, hp.plan_id, hp.decided_by, coalesce((hp.payload->'actions'->0->>'type')::int, 3) as action_type, jsonb_array_length(coalesce(hp.payload->'llmTrace'->'failureReasons', '[]'::jsonb)) as n_failure_reasons, hp.payload->'llmTrace'->'failureReasons' as failure_reasons, ho.status as outcome_status, (ho.payload->>'sloRestored')::boolean as slo_restored from platform.experiment_runs er join platform.anomaly_contexts ac on ac.experiment_run_id = er.experiment_run_id join platform.healing_plans hp on hp.context_id = ac.context_id left join platform.healing_outcomes ho on ho.plan_id = hp.plan_id where er.randomization_seed = 20260919 and er.termination_state is not null) to '/tmp/decisions.csv' with csv header;

\copy (select er.run_order, er.scenario, er.arm, (asig.payload->'window'->>'toUtc')::timestamptz as sample_end_utc, asig.detected_at_utc, ac.created_at_utc as context_created_at_utc, hp.created_at_utc as plan_created_at_utc, ho.applied_at_utc from platform.healing_plans hp join platform.anomaly_contexts ac on ac.context_id = hp.context_id join platform.anomaly_signals asig on asig.signal_id = ac.primary_signal_id join platform.experiment_runs er on er.experiment_run_id = ac.experiment_run_id left join platform.healing_outcomes ho on ho.plan_id = hp.plan_id where er.randomization_seed = 20260919 and er.termination_state is not null) to '/tmp/loop-latency.csv' with csv header;

\copy (select er.run_order, er.scenario, er.arm, er.onset_at_utc, min(asig.detected_at_utc) as first_signal_at_utc from platform.experiment_runs er join platform.anomaly_signals asig on asig.experiment_run_id = er.experiment_run_id and asig.detected_at_utc >= er.onset_at_utc where er.randomization_seed = 20260919 and er.termination_state is not null and er.onset_at_utc is not null group by er.run_order, er.scenario, er.arm, er.onset_at_utc) to '/tmp/mttd.csv' with csv header;
