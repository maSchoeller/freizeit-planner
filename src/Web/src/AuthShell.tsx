import type { ReactNode } from "react";

export function AuthShell({
  eyebrow,
  heading,
  children,
}: {
  eyebrow: string;
  heading: string;
  children: ReactNode;
}) {
  return (
    <div className="login-layout">
      <main id="main" className="login-card" tabIndex={-1}>
        <Brand />
        <p className="eyebrow">{eyebrow}</p>
        <h1>{heading}</h1>
        {children}
      </main>
    </div>
  );
}

export function Brand() {
  return (
    <div className="login-brand" aria-label="Freizeit-Cockpit">
      <span className="brand-mark" aria-hidden="true">
        F
      </span>
      <span>Freizeit-Cockpit</span>
    </div>
  );
}
