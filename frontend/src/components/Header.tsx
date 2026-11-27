import React from 'react';
import { Activity, LogOut } from 'lucide-react';

interface HeaderProps {
  isConnected: boolean;
  activeIncidentsCount: number;
  authEnabled?: boolean;
  onLogout?: () => void;
}

export const Header: React.FC<HeaderProps> = ({
  isConnected,
  activeIncidentsCount,
  authEnabled = false,
  onLogout,
}) => {
  return (
    <header className="border-b border-zinc-800/80 bg-zinc-950/80 backdrop-blur-md sticky top-0 z-40 transition-colors">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-14 flex items-center justify-between">
        {/* Brand */}
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-zinc-900 border border-zinc-700/70 flex items-center justify-center text-zinc-100 shadow-sm shadow-black/40">
            <Activity className="w-4 h-4 text-emerald-400" />
          </div>
          <div className="flex items-center gap-2.5">
            <span className="font-semibold text-sm text-zinc-100 tracking-tight font-mono">
              NexusPulse
            </span>
            <span className="text-[10px] font-mono font-medium text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded-full border border-emerald-500/20">
              v2.0
            </span>
            <span className="text-zinc-600 hidden sm:inline">•</span>
            <span className="text-xs text-zinc-400 font-mono hidden sm:inline">
              Distributed Telemetry &amp; Observability
            </span>
          </div>
        </div>

        {/* Status Indicators */}
        <div className="flex items-center gap-3 text-xs">
          {/* Operational Status */}
          <div
            className={`flex items-center gap-2 px-3 py-1 rounded-full border font-mono text-xs transition-colors ${
              activeIncidentsCount > 0
                ? 'bg-rose-500/10 text-rose-400 border-rose-500/30'
                : 'bg-zinc-900/80 text-zinc-300 border-zinc-800'
            }`}
          >
            {activeIncidentsCount > 0 ? (
              <>
                <span className="relative flex h-2 w-2">
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-rose-400 opacity-75"></span>
                  <span className="relative inline-flex rounded-full h-2 w-2 bg-rose-500"></span>
                </span>
                <span className="font-medium">
                  {activeIncidentsCount} {activeIncidentsCount === 1 ? 'Outage Active' : 'Outages Active'}
                </span>
              </>
            ) : (
              <>
                <span className="w-2 h-2 rounded-full bg-emerald-400"></span>
                <span className="text-zinc-300">Cluster Operational</span>
              </>
            )}
          </div>

          {/* WebSocket stream status */}
          <div className="flex items-center gap-2 px-3 py-1 rounded-full bg-zinc-900/80 border border-zinc-800 font-mono text-zinc-400 text-xs">
            {isConnected ? (
              <span className="relative flex h-2 w-2">
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500"></span>
              </span>
            ) : (
              <span className="w-2 h-2 rounded-full bg-zinc-600" />
            )}
            <span className="hidden md:inline">{isConnected ? 'SignalR: Live' : 'SignalR: Standby'}</span>
            <span className="md:hidden">{isConnected ? 'Live' : 'Off'}</span>
          </div>

          {authEnabled && onLogout && (
            <button
              type="button"
              onClick={onLogout}
              title="Lock & Sign out"
              className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-zinc-900/80 hover:bg-zinc-800 border border-zinc-800 hover:border-zinc-700 font-mono text-zinc-400 hover:text-zinc-200 text-xs transition"
            >
              <LogOut className="w-3 h-3" />
              <span className="hidden sm:inline">Lock</span>
            </button>
          )}
        </div>
      </div>
    </header>
  );
};
