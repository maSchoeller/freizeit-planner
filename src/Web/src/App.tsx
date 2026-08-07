import {
  CalendarDays,
  ChefHat,
  Church,
  ClipboardList,
  NotebookPen,
  ShoppingCart,
} from "lucide-react";
import { Link, Navigate, Route, Routes } from "react-router-dom";
import { LoginPage } from "./LoginPage";
import { SessionsPage } from "./SessionsPage";

const navigation = [
  { label: "Übersicht", icon: ClipboardList },
  { label: "Tagesplan", icon: CalendarDays },
  { label: "Essen & Rezepte", icon: ChefHat },
  { label: "Material & Einkauf", icon: ShoppingCart },
  { label: "Andachten", icon: Church },
  { label: "Notizbuch", icon: NotebookPen },
];

export function App() {
  return (
    <Routes>
      <Route path="/anmelden" element={<LoginPage />} />
      <Route path="/konto/sitzungen" element={<SessionsPage />} />
      <Route
        path="/o/:organizationSlug/camps/:campSlug/*"
        element={<Dashboard />}
      />
      <Route
        path="*"
        element={
          <Navigate replace to="/o/sonnenhoehe/camps/sommerfreizeit-2026" />
        }
      />
    </Routes>
  );
}

function Dashboard() {
  return (
    <div className="app-shell">
      <a className="skip-link" href="#main">
        Zum Inhalt springen
      </a>
      <header className="topbar">
        <div className="brand" aria-label="Freizeit-Cockpit">
          <span className="brand-mark" aria-hidden="true">
            F
          </span>
          <span>Freizeit-Cockpit</span>
        </div>
        <Link
          className="profile-button"
          aria-label="Kontomenü von Miriam öffnen"
          to="/konto/sitzungen"
        >
          MK
        </Link>
      </header>
      <div className="workspace">
        <aside className="sidebar" aria-label="Camp-Navigation">
          <p className="eyebrow">Sonnenhöhe e. V.</p>
          <p className="camp-name">Sommerfreizeit 2026</p>
          <nav aria-label="Camp-Navigation">
            <ul>
              {navigation.map(({ label, icon: Icon }, index) => (
                <li key={label}>
                  <a
                    aria-current={index === 0 ? "page" : undefined}
                    href={index === 0 ? "/" : `#${label}`}
                  >
                    <Icon aria-hidden="true" size={20} />
                    <span>{label}</span>
                  </a>
                </li>
              ))}
            </ul>
          </nav>
          <a className="help-link" href="/hilfe/">
            Hilfe & Anleitung
          </a>
        </aside>
        <main id="main" tabIndex={-1}>
          <div className="page-heading">
            <div>
              <p className="eyebrow">Dienstag, 4. August</p>
              <h1>Guten Morgen, Miriam</h1>
              <p>Hier siehst du, was heute für euer Team wichtig ist.</p>
            </div>
            <button className="primary-action" type="button">
              Eintrag erstellen
            </button>
          </div>
          <section aria-labelledby="today-heading">
            <div className="section-heading">
              <h2 id="today-heading">Heute im Tagesplan</h2>
              <a href="#Tagesplan">Ganzen Plan öffnen</a>
            </div>
            <ol className="timeline">
              <li>
                <time dateTime="2026-08-04T08:00">08:00</time>
                <div>
                  <strong>Frühstück</strong>
                  <span>Speisesaal · Küchenteam</span>
                </div>
                <span className="status">Geplant</span>
              </li>
              <li>
                <time dateTime="2026-08-04T09:30">09:30</time>
                <div>
                  <strong>Geländespiel im Wald</strong>
                  <span>Treffpunkt Haupthaus · Miriam, Jonas</span>
                </div>
                <span className="status info">Parallel</span>
              </li>
              <li>
                <time dateTime="2026-08-04T19:30">19:30</time>
                <div>
                  <strong>Abendandacht</strong>
                  <span>Feuerstelle · Samuel</span>
                </div>
                <span className="status">Vorbereitet</span>
              </li>
            </ol>
          </section>
          <div className="dashboard-grid">
            <section className="card" aria-labelledby="responsibility-heading">
              <h2 id="responsibility-heading">Meine Verantwortungen</h2>
              <p className="metric">
                4 <span>offene Punkte</span>
              </p>
              <a href="#Verantwortungen">Alle anzeigen</a>
            </section>
            <section className="card" aria-labelledby="shopping-heading">
              <h2 id="shopping-heading">Beschaffung</h2>
              <p className="metric">
                12 <span>noch einzukaufen</span>
              </p>
              <a href="#Einkauf">Einkaufslisten öffnen</a>
            </section>
            <section
              className="card activity-card"
              aria-labelledby="activity-heading"
            >
              <h2 id="activity-heading">Jüngste Aktivitäten</h2>
              <ul>
                <li>
                  <span>Jonas hat „Geländespiel“ geändert.</span>
                  <time dateTime="2026-08-04T07:48">vor 12 Min.</time>
                </li>
                <li>
                  <span>Lea hat 3 Einkaufspositionen abgehakt.</span>
                  <time dateTime="2026-08-04T07:31">vor 29 Min.</time>
                </li>
              </ul>
            </section>
          </div>
        </main>
      </div>
    </div>
  );
}
