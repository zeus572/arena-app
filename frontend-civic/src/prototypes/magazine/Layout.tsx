import { Outlet, Link, NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "@/auth/AuthContext";
import { DEBATE_ARENA_URL } from "@/lib/links";
import { BottomTabs } from "./components/BottomTabs";
import "./theme.css";

const NAV_LINKS = [
  { to: "/", label: "Home", end: true },
  { to: "/candidates", label: "Feed" },
  { to: "/campaigns", label: "Campaign" },
  { to: "/leagues", label: "Leagues" },
  { to: "/coalition", label: "Coalition" },
  { to: "/quizzes", label: "Quizzes" },
];

function TopNav() {
  return (
    <nav
      className="hidden items-center gap-6 md:flex"
      data-testid="top-nav"
      aria-label="Primary"
    >
      {NAV_LINKS.map((l) => (
        <NavLink
          key={l.to}
          to={l.to}
          end={l.end}
          data-testid={`top-nav-${l.label.toLowerCase()}`}
          className={({ isActive }) =>
            [
              "text-xs font-semibold uppercase tracking-wider transition",
              isActive
                ? "text-[var(--accent)]"
                : "text-[var(--muted)] hover:text-[var(--fg)]",
            ].join(" ")
          }
        >
          {l.label}
        </NavLink>
      ))}
      <a
        href={DEBATE_ARENA_URL}
        target="_blank"
        rel="noreferrer"
        data-testid="top-nav-debate-arena"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] transition hover:text-[var(--fg)]"
      >
        Debate Arena ↗
      </a>
    </nav>
  );
}

function AuthStrip() {
  const { user, isAuthenticated, isLoading, logout } = useAuth();
  const navigate = useNavigate();

  if (isLoading) {
    return <span className="text-xs text-[var(--muted)]">…</span>;
  }

  if (isAuthenticated && user) {
    return (
      <div className="flex items-center gap-3" data-testid="auth-strip-authed">
        <span
          className="hidden text-xs font-semibold uppercase tracking-wider text-[var(--fg-soft)] sm:inline"
          data-testid="auth-strip-email"
        >
          {user.displayName ?? user.email}
        </span>
        <span
          className="text-xs font-semibold uppercase tracking-wider text-[var(--fg-soft)] sm:hidden"
          data-testid="auth-strip-email-mobile"
          aria-hidden
        >
          {(user.displayName ?? user.email).slice(0, 1).toUpperCase()}
        </span>
        <button
          type="button"
          onClick={async () => {
            await logout();
            navigate("/");
          }}
          className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
          data-testid="auth-strip-logout"
        >
          Log out
        </button>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-3" data-testid="auth-strip-anon">
      <Link
        to="/login"
        className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)] hover:text-[var(--fg)]"
        data-testid="auth-strip-login-link"
      >
        Sign in
      </Link>
      <Link
        to="/register"
        className="rounded-full bg-[var(--accent)] px-3 py-1.5 text-xs font-semibold uppercase tracking-wider text-white"
        data-testid="auth-strip-signup-link"
      >
        Sign up
      </Link>
    </div>
  );
}

export default function MagazineLayout() {
  return (
    <div className="theme-magazine min-h-screen pb-20 md:pb-0">
      <header
        className="sticky top-0 z-20 border-b border-[var(--border)] bg-[var(--bg)]/95 backdrop-blur md:static md:bg-[var(--bg)]"
        data-testid="magazine-header"
      >
        <div className="mx-auto max-w-5xl px-4 py-3 md:px-8 md:py-8">
          <div className="flex items-center justify-between gap-4">
            <Link
              to="/"
              className="display text-lg tracking-tight text-[var(--accent)] md:hidden"
              data-testid="masthead-mobile"
            >
              Public Lab
            </Link>
            <TopNav />
            <AuthStrip />
          </div>
          <Link
            to="/"
            className="display mt-6 hidden text-center text-5xl tracking-tight md:block md:text-6xl"
            data-testid="masthead"
          >
            Public Lab
          </Link>
          <p className="mt-2 hidden text-center text-xs font-semibold uppercase tracking-[0.3em] text-[var(--muted)] md:block">
            Civics for the world you actually live in
          </p>
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-4 py-6 md:px-8 md:py-12">
        <Outlet />
      </main>

      <footer className="hidden border-t border-[var(--border)] bg-[var(--bg)] py-10 md:block">
        <div className="mx-auto max-w-5xl px-8 text-center">
          <p className="display text-2xl text-[var(--accent)]">Public Lab</p>
          <p className="mt-2 text-xs uppercase tracking-[0.2em] text-[var(--muted)]">
            Civics for the world you actually live in
          </p>
        </div>
      </footer>

      <BottomTabs />
    </div>
  );
}
