import type { ReactNode } from "react";
import { NavLink } from "react-router";
import { ConnectionBadge } from "@/components/ConnectionBadge";
import { ModeControl } from "@/features/control/components/ModeControl";
import { cn } from "@/lib/utils";

const NAV_ITEMS = [
  { to: "/", label: "Visão geral", end: true },
  { to: "/plans", label: "Planos" },
  { to: "/metrics", label: "Métricas" },
];

export function Layout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex flex-wrap items-center justify-between gap-4 border-b border-border px-6 py-3">
        <div className="flex items-center gap-6">
          <span className="text-lg font-semibold">Norn</span>
          <nav aria-label="Navegação principal" className="flex gap-1">
            {NAV_ITEMS.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  cn(
                    "rounded px-3 py-1.5 text-sm",
                    isActive
                      ? "bg-secondary text-secondary-foreground"
                      : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
                  )
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>
        </div>
        <div className="flex items-center gap-4">
          <ModeControl />
          <ConnectionBadge />
        </div>
      </header>
      <main className="flex-1 p-6">{children}</main>
    </div>
  );
}
