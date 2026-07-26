import { useEffect, useState } from "react";
import { Copy } from "lucide-react";
import { Button } from "../Button";

/**
 * The copyable emoji grid. Built server-side so it's byte-identical on web and Android;
 * this component only renders and copies it.
 *
 * The "Copied." confirmation is not decoration — a copy button with no feedback reads as
 * broken, which is exactly the complaint we already had about the MFA backup-code copy.
 */
export function ShareGrid({ grid }: { grid: string }) {
  const [copied, setCopied] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const t = setTimeout(() => setCopied(false), 2000);
    return () => clearTimeout(t);
  }, [copied]);

  const onCopy = async () => {
    setFailed(false);
    try {
      await navigator.clipboard.writeText(grid);
      setCopied(true);
    } catch {
      // Clipboard can be blocked (insecure origin, permissions, older webview). Say so
      // rather than silently doing nothing — the text stays selectable above.
      setFailed(true);
    }
  };

  return (
    <div
      className="mt-6 border border-[var(--border)] bg-[var(--bg)] p-4"
      data-testid="daily-share"
    >
      <pre
        className="whitespace-pre-wrap break-words font-sans text-sm leading-relaxed text-[var(--fg-soft)]"
        data-testid="daily-share-grid"
      >
        {grid}
      </pre>
      <div className="mt-3 flex items-center gap-3">
        <Button variant="ghost" onClick={() => void onCopy()} data-testid="daily-share-copy">
          <Copy /> Copy result
        </Button>
        {copied && (
          <span className="text-xs font-semibold text-emerald-600" data-testid="daily-share-copied">
            Copied.
          </span>
        )}
        {failed && (
          <span className="text-xs font-semibold text-[var(--muted)]" data-testid="daily-share-copyfail">
            Couldn't copy — select the text above.
          </span>
        )}
      </div>
    </div>
  );
}
