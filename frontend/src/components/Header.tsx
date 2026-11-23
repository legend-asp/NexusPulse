import React from 'react';
import { Activity } from 'lucide-react';

interface HeaderProps {
  isConnected: boolean;
  activeIncidentsCount: number;
}

export const Header: React.FC<HeaderProps> = ({ isConnected, activeIncidentsCount }) => {
  return (
    <header className="border-b border-zinc-800 bg-zinc-950/90 backdrop-blur sticky top-0 z-40">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-14 flex items-center justify-between">
        {/* Brand */}
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-md bg-zinc-900 border border-zinc-700/80 flex items-center justify-center text-zinc-100 shadow-sm">
            <Activity className="w-4 h-4 text-emerald-400" />
          </div>
          <div className="flex items-center gap-2.5">
            <span className="font-semibold text-sm text-zinc-100 tracking-tight font-mono">
              NexusPulse
            </span>
            <span className="text-[11px] font-mono text-zinc-400 bg-zinc-900 px-1.5 py-0.5 rounded border border-zinc-800">
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
            className={`flex items-center gap-2 px-3 py-1 rounded-md border font-mono ${
              activeIncidentsCount > 0
                ? 'bg-red-950/40 text-red-400 border-red-800/60'
                : 'bg-zinc-900 text-zinc-300 border-zinc-800'
            }`}
          >
            {activeIncidentsCount > 0 ? (
              <>
                <span className="w-2 h-2 rounded-full bg-red-500 animate-pulse" />
                <span>
                  {activeIncidentsCount} {activeIncidentsCount === 1 ? 'Outage Active' : 'Outages Active'}
                </span>
              </>
            ) : (
              <>
                <span className="w-2 h-2 rounded-full bg-emerald-500" />
                <span>All Nodes Operational</span>
              </>
            )}
          </div>

          {/* WebSocket stream status */}
          <div className="flex items-center gap-2 px-3 py-1 rounded-md bg-zinc-900 border border-zinc-800 font-mono text-zinc-400">
            <span
              className={`w-1.5 h-1.5 rounded-full ${
                isConnected ? 'bg-emerald-400' : 'bg-zinc-600'
              }`}
            />
            <span className="hidden md:inline">{isConnected ? 'SignalR: Connected' : 'SignalR: Standby'}</span>
            <span className="md:hidden">{isConnected ? 'Live' : 'Standby'}</span>
          </div>
        </div>
      </div>
    </header>
  );
};
