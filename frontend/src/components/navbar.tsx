import { Link, useLocation } from "react-router-dom";
import { useState } from "react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { LayoutList, PlusCircle, Menu, X, Landmark, Users, Zap, MessageSquarePlus, LogIn, User, LogOut, Crown, AlertCircle, ShieldCheck, Newspaper } from "lucide-react";
import { ThemeToggle } from "@/components/theme-toggle";
import { forceTick, forceNewsSync } from "@/api/client";
import { useAuth } from "@/contexts/AuthContext";

const NAV_LINKS = [
  { href: "/", label: "Feed", icon: LayoutList },
  { href: "/topics", label: "Topics", icon: MessageSquarePlus },
  { href: "/start", label: "Start Debate", icon: PlusCircle },
  { href: "/agents", label: "Agents", icon: Users },
  { href: "/sources", label: "Sources", icon: ShieldCheck },
];

export function Navbar() {
  const { pathname } = useLocation();
  const [open, setOpen] = useState(false);
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const { user, isAuthenticated, isPremium, logout } = useAuth();

  return (
    <header className="sticky top-0 z-50 border-b border-border bg-background/95 backdrop-blur-sm">
      <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
        <Link
          to="/"
          className="flex items-center gap-2 font-bold text-foreground tracking-tight no-underline"
        >
          <span className="flex h-7 w-7 items-center justify-center rounded bg-primary text-primary-foreground">
            <Landmark size={14} />
          </span>
          <span className="text-sm font-semibold">Debate Arena</span>
        </Link>

        <nav className="hidden md:flex items-center gap-1">
          {NAV_LINKS.map(({ href, label, icon: Icon }) => {
            const isActive = href === "/" ? pathname === "/" : pathname.startsWith(href);
            return (
              <Link key={href} to={href}>
                <Button
                  variant={isActive ? "secondary" : "ghost"}
                  size="sm"
                  className={cn("gap-1.5 text-xs font-medium", isActive && "text-primary")}
                >
                  <Icon size={14} />
                  {label}
                </Button>
              </Link>
            );
          })}
        </nav>

        <div className="flex items-center gap-2">
          {import.meta.env.DEV && (
            <Button
              variant="ghost"
              size="sm"
              className="gap-1 text-[10px] text-amber-500 hover:text-amber-400"
              onClick={async () => {
                try {
                  await forceTick();
                  window.location.reload();
                } catch (e) {
                  console.error("Force tick failed", e);
                }
              }}
            >
              <Zap size={12} />
              Tick
            </Button>
          )}
          {import.meta.env.DEV && (
            <Button
              variant="ghost"
              size="sm"
              className="gap-1 text-[10px] text-emerald-500 hover:text-emerald-400"
              onClick={async () => {
                try {
                  await forceNewsSync();
                  alert("News topics synced!");
                } catch (e) {
                  console.error("News sync failed", e);
                }
              }}
            >
              <Newspaper size={12} />
              News
            </Button>
          )}

          <ThemeToggle />

          {isAuthenticated ? (
            <div className="relative">
              <button
                onClick={() => setUserMenuOpen(!userMenuOpen)}
                className="flex items-center gap-1.5 rounded-full border border-border bg-card px-2.5 py-1.5 hover:border-primary/40 transition-colors"
              >
                <div className="relative">
                  <div className="h-6 w-6 rounded-full bg-primary/10 flex items-center justify-center text-[11px] font-bold text-primary">
                    {(user?.displayName ?? user?.email ?? "U")[0].toUpperCase()}
                  </div>
                  {!user?.emailVerified && (
                    <span className="absolute -top-0.5 -right-0.5 h-2 w-2 rounded-full bg-amber-500" />
                  )}
                </div>
                <span className="text-xs font-medium text-card-foreground hidden sm:inline max-w-[80px] truncate">
                  {user?.displayName ?? user?.email?.split("@")[0]}
                </span>
                {isPremium && <Crown size={10} className="text-amber-500" />}
              </button>

              {userMenuOpen && (
                <>
                  <div className="fixed inset-0 z-40" onClick={() => setUserMenuOpen(false)} />
                  <div className="absolute right-0 top-full mt-1 z-50 w-48 rounded-lg border border-border bg-card shadow-lg py-1">
                    <div className="px-3 py-2 border-b border-border">
                      <p className="text-xs font-medium text-card-foreground truncate">{user?.displayName ?? user?.email}</p>
                      <div className="flex items-center gap-1.5 mt-0.5">
                        <span className="text-[10px] text-muted-foreground">{user?.plan} Plan</span>
                        {!user?.emailVerified && (
                          <span className="text-[9px] font-semibold text-amber-600 bg-amber-500/10 rounded px-1 py-0.5">Unverified</span>
                        )}
                      </div>
                    </div>
                    {(!user?.emailVerified || !user?.politicalLeaning) && (
                      <Link
                        to="/profile"
                        onClick={() => setUserMenuOpen(false)}
                        className="flex items-center gap-2 px-3 py-2 text-[11px] text-amber-600 bg-amber-500/5 hover:bg-amber-500/10 transition-colors"
                      >
                        <AlertCircle size={11} />
                        Complete your profile
                      </Link>
                    )}
                    <Link
                      to="/profile"
                      onClick={() => setUserMenuOpen(false)}
                      className="flex items-center gap-2 px-3 py-2 text-xs text-card-foreground hover:bg-secondary transition-colors"
                    >
                      <User size={12} /> Profile
                    </Link>
                    <button
                      onClick={() => { setUserMenuOpen(false); logout(); }}
                      className="w-full flex items-center gap-2 px-3 py-2 text-xs text-card-foreground hover:bg-secondary transition-colors"
                    >
                      <LogOut size={12} /> Log Out
                    </button>
                  </div>
                </>
              )}
            </div>
          ) : (
            <div className="flex items-center gap-1">
              <Link to="/login">
                <Button variant="ghost" size="sm" className="text-xs gap-1">
                  <LogIn size={12} /> Log In
                </Button>
              </Link>
              <Link to="/register">
                <Button size="sm" className="text-xs">Sign Up</Button>
              </Link>
            </div>
          )}

          <button
            className="md:hidden p-1.5 rounded text-muted-foreground hover:text-foreground"
            onClick={() => setOpen(!open)}
            aria-label="Toggle menu"
          >
            {open ? <X size={18} /> : <Menu size={18} />}
          </button>
        </div>
      </div>

      {open && (
        <nav className="md:hidden border-t border-border bg-background px-4 py-3 flex flex-col gap-1">
          {NAV_LINKS.map(({ href, label, icon: Icon }) => {
            const isActive = href === "/" ? pathname === "/" : pathname.startsWith(href);
            return (
              <Link key={href} to={href} onClick={() => setOpen(false)}>
                <Button
                  variant={isActive ? "secondary" : "ghost"}
                  size="sm"
                  className={cn(
                    "w-full justify-start gap-2 text-xs font-medium",
                    isActive && "text-primary"
                  )}
                >
                  <Icon size={14} />
                  {label}
                </Button>
              </Link>
            );
          })}
        </nav>
      )}
    </header>
  );
}
