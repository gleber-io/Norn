import type { ReactNode } from "react";

interface QueryStateProps {
  isLoading: boolean;
  isError: boolean;
  error?: unknown;
  isEmpty?: boolean;
  emptyMessage?: string;
  children: ReactNode;
}

/** Estado de loading/erro/vazio consistente entre as telas (tarefa 10 da Fase 11) — cada feature
 * só precisa decidir o que é "vazio" pra ela; o resto é compartilhado. */
export function QueryState({
  isLoading,
  isError,
  error,
  isEmpty,
  emptyMessage = "Nada por aqui ainda.",
  children,
}: QueryStateProps) {
  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-8 text-muted-foreground" role="status">
        Carregando…
      </div>
    );
  }

  if (isError) {
    return (
      <div className="flex items-center justify-center p-8 text-destructive" role="alert">
        Falha ao carregar dados{error instanceof Error ? `: ${error.message}` : "."}
      </div>
    );
  }

  if (isEmpty) {
    return (
      <div className="flex items-center justify-center p-8 text-muted-foreground">
        {emptyMessage}
      </div>
    );
  }

  return <>{children}</>;
}
