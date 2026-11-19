import React from 'react';
import { Activity, Radio, ShieldCheck } from 'lucide-react';

interface HeaderProps {
  isConnected: boolean;
  activeIncidentsCount: number;
}

export const Header: React.FC<HeaderProps> = ({ isConnected, activeIncidentsCount }) => {
  return (
    <header className="border-b border-slate-800 bg-slate-900/60 backdrop-blur-md sticky top-0 z-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        {/* Логотип и Название */}
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-cyan-500 to-indigo-600 flex items-center justify-center shadow-lg shadow-indigo-500/20">
            <Activity className="w-5 h-5 text-white" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="font-bold text-lg text-slate-100 tracking-tight">NexusPulse</span>
              <span className="text-[10px] font-mono uppercase bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 px-1.5 py-0.5 rounded">
                v2.0 AI
              </span>
            </div>
            <p className="text-xs text-slate-400 hidden sm:block">
              Distributed Observability & AI Incident Response
            </p>
          </div>
        </div>

        {/* Правая часть: Статус SignalR и индикатор инцидентов */}
        <div className="flex items-center gap-4">
          {/* Индикатор инцидентов */}
          <div
            className={`hidden sm:flex items-center gap-2 px-3 py-1 rounded-full text-xs font-medium border ${
              activeIncidentsCount > 0
                ? 'bg-rose-500/10 text-rose-300 border-rose-500/30 animate-pulse'
                : 'bg-emerald-500/10 text-emerald-300 border-emerald-500/30'
            }`}
          >
            <ShieldCheck className="w-3.5 h-3.5" />
            <span>
              {activeIncidentsCount > 0 ? `${activeIncidentsCount} Incident Active` : 'All Systems Operational'}
            </span>
          </div>

          {/* Статус WebSockets (SignalR) */}
          <div className="flex items-center gap-2 px-3 py-1 rounded-full bg-slate-950/60 border border-slate-800 text-xs">
            <span
              className={`w-2 h-2 rounded-full ${
                isConnected ? 'bg-emerald-400 animate-pulse' : 'bg-rose-500'
              }`}
            />
            <Radio className="w-3.5 h-3.5 text-slate-400" />
            <span className="font-mono text-slate-300 text-xs">
              {isConnected ? 'SignalR: Live' : 'SignalR: Offline'}
            </span>
          </div>
        </div>
      </div>
    </header>
  );
};
