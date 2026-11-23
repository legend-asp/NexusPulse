import React from 'react';
import type { MonitoredNode } from '../types';
import { Server, Activity, AlertCircle, CheckCircle2, Flame, ExternalLink, RotateCcw } from 'lucide-react';

export interface NodeCardProps {
  node: MonitoredNode;
  onSimulateFailure: (id: string) => void;
  onRestoreNode: (id: string) => void;
}

export const NodeCard: React.FC<NodeCardProps> = ({ node, onSimulateFailure, onRestoreNode }) => {
  const status = node.healthStatus || 'Healthy';
  const isDown = status === 'Down';
  const isDegraded = status === 'Degraded';
  const latency = node.lastLatencyMs;

  const getStatusBadge = () => {
    switch (status) {
      case 'Healthy':
        return {
          icon: <CheckCircle2 className="w-3.5 h-3.5" />,
          label: 'HEALTHY',
          classes: 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30'
        };
      case 'Degraded':
        return {
          icon: <Activity className="w-3.5 h-3.5" />,
          label: 'DEGRADED',
          classes: 'bg-amber-500/10 text-amber-400 border-amber-500/30'
        };
      case 'Down':
        return {
          icon: <AlertCircle className="w-3.5 h-3.5" />,
          label: 'DOWN',
          classes: 'bg-rose-500/10 text-rose-400 border-rose-500/40 animate-pulse'
        };
      default:
        return {
          icon: <CheckCircle2 className="w-3.5 h-3.5" />,
          label: status,
          classes: 'bg-slate-500/10 text-slate-400 border-slate-500/20'
        };
    }
  };

  const badge = getStatusBadge();

  return (
    <div
      className={`group relative flex flex-col justify-between rounded-xl border p-5 transition-all duration-300 shadow-md ${
        isDown
          ? 'bg-rose-950/20 border-rose-500/50 shadow-rose-950/40 hover:border-rose-400'
          : isDegraded
          ? 'bg-amber-950/20 border-amber-500/40 hover:border-amber-400'
          : 'bg-slate-900/80 border-slate-800/80 hover:border-slate-700 hover:shadow-lg'
      }`}
    >
      {/* Верхняя часть: Иконка, Имя, Ссылка и Статус-бейдж */}
      <div>
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-center gap-3 min-w-0">
            <div
              className={`p-2.5 rounded-lg flex-shrink-0 transition-colors ${
                isDown
                  ? 'bg-rose-500/20 text-rose-400'
                  : isDegraded
                  ? 'bg-amber-500/20 text-amber-400'
                  : 'bg-emerald-500/10 text-emerald-400 group-hover:bg-emerald-500/20'
              }`}
            >
              <Server className="w-5 h-5" />
            </div>
            <div className="min-w-0 flex-1">
              <h3 className="truncate font-semibold text-slate-100 text-base">
                {node.name}
              </h3>
              <a
                href={node.targetUrl}
                target="_blank"
                rel="noreferrer"
                className="mt-0.5 inline-flex items-center gap-1 text-xs text-slate-400 hover:text-emerald-400 transition truncate max-w-full"
              >
                <span className="truncate">{node.targetUrl}</span>
                <ExternalLink className="w-3 h-3 flex-shrink-0" />
              </a>
            </div>
          </div>

          <span
            className={`inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-semibold tracking-wider ${badge.classes}`}
          >
            {badge.icon}
            {badge.label}
          </span>
        </div>

        {/* Метрики отклика */}
        <div className="mt-4 grid grid-cols-2 gap-2 rounded-lg bg-slate-950/60 border border-slate-800/80 p-3 text-xs">
          <div>
            <span className="text-[10px] uppercase tracking-wider text-slate-500 font-semibold block">
              RTT Latency
            </span>
            {isDown ? (
              <span className="font-mono font-bold text-rose-400 text-sm">Timeout / 504</span>
            ) : (
              <span
                className={`font-mono font-bold text-sm ${
                  (latency ?? 0) > 400
                    ? 'text-amber-400'
                    : 'text-emerald-400'
                }`}
              >
                {latency !== undefined ? `${latency} ms` : '24 ms'}
              </span>
            )}
          </div>
          <div>
            <span className="text-[10px] uppercase tracking-wider text-slate-500 font-semibold block">
              Interval
            </span>
            <span className="font-mono text-slate-300 text-sm font-medium">
              {node.checkIntervalSeconds ?? 5}s
            </span>
          </div>
        </div>
      </div>

      {/* Нижняя часть: Кнопка действия (Simulate Outage ИЛИ Restore Service) */}
      {isDown ? (
        <button
          type="button"
          onClick={() => onRestoreNode(node.id)}
          className="mt-4 flex w-full items-center justify-center gap-2 rounded-lg border py-2.5 px-4 text-xs font-semibold transition-all duration-200 bg-emerald-500/10 hover:bg-emerald-600 text-emerald-300 hover:text-white border-emerald-500/30 hover:border-emerald-600 shadow-sm active:scale-[0.98]"
        >
          <RotateCcw className="w-3.5 h-3.5 text-emerald-400 group-hover:text-white" />
          Restore Node
        </button>
      ) : (
        <button
          type="button"
          onClick={() => onSimulateFailure(node.id)}
          className="mt-4 flex w-full items-center justify-center gap-2 rounded-lg border py-2.5 px-4 text-xs font-semibold transition-all duration-200 bg-rose-500/10 hover:bg-rose-600 text-rose-300 hover:text-white border-rose-500/30 hover:border-rose-600 shadow-sm active:scale-[0.98]"
        >
          <Flame className="w-3.5 h-3.5 text-rose-400" />
          Simulate Outage
        </button>
      )}
    </div>
  );
};
