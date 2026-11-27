import React, { useState } from 'react';
import { ShieldCheck, KeyRound, Lock, AlertCircle, Activity } from 'lucide-react';

interface LoginViewProps {
  onSuccess: () => void;
}

export const LoginView: React.FC<LoginViewProps> = ({ onSuccess }) => {
  const [password, setPassword] = useState('');
  const [totpCode, setTotpCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({
          password,
          totpCode: totpCode.trim(),
        }),
      });

      if (!response.ok) {
        const data = await response.json().catch(() => null);
        setError(data?.error ?? 'Authentication failed. Check password and 2FA code.');
        return;
      }

      onSuccess();
    } catch {
      setError('Cannot reach monitoring API.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-100 flex items-center justify-center p-4 relative font-sans">
      <div className="fixed inset-0 pointer-events-none bg-[radial-gradient(ellipse_60%_40%_at_50%_0%,rgba(16,185,129,0.06),rgba(0,0,0,0))]" />

      <div className="w-full max-w-md rounded-2xl bg-zinc-900/70 backdrop-blur-xl border border-zinc-800/90 p-7 shadow-2xl shadow-black/60 relative z-10">
        <div className="flex items-center gap-3 mb-6">
          <div className="w-10 h-10 rounded-xl bg-zinc-950 border border-zinc-800 flex items-center justify-center">
            <Activity className="w-5 h-5 text-emerald-400" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-base font-semibold font-mono text-zinc-100">NexusPulse NOC</h1>
              <span className="text-[10px] font-mono text-emerald-400 bg-emerald-500/10 border border-emerald-500/20 px-2 py-0.5 rounded-full">
                2FA Protected
              </span>
            </div>
            <p className="text-xs text-zinc-400 font-mono mt-0.5">
              Infrastructure Telemetry Console
            </p>
          </div>
        </div>

        {error && (
          <div className="mb-5 flex items-start gap-2.5 rounded-xl border border-rose-500/30 bg-rose-500/10 px-3.5 py-3 text-xs text-rose-300 font-mono">
            <AlertCircle className="w-4 h-4 text-rose-400 flex-shrink-0 mt-0.5" />
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-[11px] font-mono uppercase tracking-wider text-zinc-400 mb-1.5">
              Operator Password
            </label>
            <div className="relative">
              <Lock className="w-4 h-4 text-zinc-500 absolute left-3.5 top-1/2 -translate-y-1/2" />
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                autoComplete="current-password"
                placeholder="••••••••••••"
                className="w-full rounded-xl bg-zinc-950/90 border border-zinc-800 focus:border-emerald-500/60 focus:outline-none pl-10 pr-4 py-2.5 text-sm font-mono text-zinc-100 placeholder:text-zinc-600 transition"
              />
            </div>
          </div>

          <div>
            <label className="block text-[11px] font-mono uppercase tracking-wider text-zinc-400 mb-1.5">
              Google Authenticator Code
            </label>
            <div className="relative">
              <KeyRound className="w-4 h-4 text-zinc-500 absolute left-3.5 top-1/2 -translate-y-1/2" />
              <input
                type="text"
                inputMode="numeric"
                pattern="[0-9]{6}"
                maxLength={6}
                value={totpCode}
                onChange={(e) => setTotpCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                required
                autoComplete="one-time-code"
                placeholder="000000"
                className="w-full rounded-xl bg-zinc-950/90 border border-zinc-800 focus:border-emerald-500/60 focus:outline-none pl-10 pr-4 py-2.5 text-base font-mono tracking-[0.35em] text-zinc-100 placeholder:text-zinc-600 transition"
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={submitting || totpCode.length !== 6}
            className="w-full mt-2 flex items-center justify-center gap-2 rounded-xl bg-emerald-500/15 hover:bg-emerald-500/25 disabled:opacity-50 disabled:cursor-not-allowed border border-emerald-500/40 px-4 py-2.5 text-sm font-mono font-medium text-emerald-300 transition"
          >
            <ShieldCheck className="w-4 h-4" />
            {submitting ? 'Verifying TOTP...' : 'Unlock Telemetry Dashboard'}
          </button>
        </form>
      </div>
    </div>
  );
};
