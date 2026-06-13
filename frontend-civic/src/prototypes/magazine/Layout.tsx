import { Outlet, Link, NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "@/auth/AuthContext";
import { DEBATE_ARENA_URL } from "@/lib/links";
import { BottomTabs } from "./components/BottomTabs";
import { MobileMenu } from "./components/MobileMenu";
import { NavDropdown } from "./components/NavDropdown";
import { ButtonLink } from "./components/Button";
import { NAV_COMPETE, NAV_EXPLORE, NAV_PRIMARY, NAV_TRAILING } from "./nav";
import "./theme.css";

function TopNav() {
  // Flat, high-engagement links (Home, Feed + the Compete group) sit in the row;
  // the Explore tools live under a dropdown; About is pinned to the end.
  const flatLinks = [...NAV_PRIMARY, ...NAV_COMPETE.links];

  return (
    <nav
      className="hidden items-center gap-6 md:flex"
      data-testid="top-nav"
      aria-label="Primary"
    >
      {flatLinks.map((l) => (
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

      <NavDropdown label={NAV_EXPLORE.heading} links={NAV_EXPLORE.links} />

      {NAV_TRAILING.map((l) => (
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
      <ButtonLink
        to="/register"
        size="sm"
        className="uppercase tracking-wider"
        data-testid="auth-strip-signup-link"
      >
        Sign up
      </ButtonLink>
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
            <div className="flex items-center gap-1.5 md:hidden">
              <MobileMenu />
              <Link
                to="/"
                className="display flex items-center text-lg tracking-tight text-[var(--accent)]"
                data-testid="masthead-mobile"
              >
                <img src="/brand-mark.png" alt="C" className="-mr-1 h-6 w-6 object-contain" />
                <span>iversify</span>
              </Link>
            </div>
            <TopNav />
            <AuthStrip />
          </div>
          <Link
            to="/"
            className="mt-6 hidden items-center justify-center md:flex"
            data-testid="masthead"
          >
            <img
              src="/brand-mark.png"
              alt="C"
              className="-mr-1.5 h-14 w-14 object-contain md:-mr-2 md:h-[4.25rem] md:w-[4.25rem]"
            />
            <span className="display text-5xl tracking-tight md:text-6xl">
              iversify
            </span>
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
          <p className="display text-2xl text-[var(--accent)]">Civersify</p>
          <p className="mt-2 text-xs uppercase tracking-[0.2em] text-[var(--muted)]">
            Civics for the world you actually live in
          </p>
          <div className="mt-4 flex items-center justify-center gap-5 text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
            <Link to="/about" className="hover:text-[var(--accent)]">About</Link>
            <Link to="/coalition" className="hover:text-[var(--accent)]">Coalitions</Link>
            <Link to="/zeitgeist" className="hover:text-[var(--accent)]">Zeitgeist</Link>
          </div>
        </div>
      </footer>

      <BottomTabs />
    </div>
  );
}
