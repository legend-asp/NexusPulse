import React from 'react';
import type { MonitoredNode } from '../types';
import { Server, ExternalLink, RotateCcw, FileText } from 'lucide-react';

export interface NodeCardProps {
  node: MonitoredNode;
  onSimulateFailure: (id: string) => void;
  onRestoreNode: (id: string) => void;
  onViewAnalysis: (node: MonitoredNode) => void;
}

export const NodeCard: React.FC<NodeCardProps> = ({
  node,
  onSimulateFailure,
  onRestoreNode,
  onViewAnalysis,
}) => {
  const status = node.healthStatus || 'Healthy';
  const isDown = status === 'Down';
  const isDegraded = status === 'Degraded';
  const latency = node.lastLatencyMs ?? 24;

  // Рассчитываем процент для визуальной шкалы latency (норма < 100ms)
  const latencyBarWidth = isDown
    ? 100
    : Math.min(100, Math.max(12, Math.round((latency / 200) * 100)));

  return (
    <div
      className={`relative flex flex-col justify-between rounded-xl border p-5 transition-all duration-200 backdrop-blur-md overflow-hidden group ${
        isDown
          ? 'bg-zinc-900/70 border-rose-500/50 shadow-lg shadow-rose-950/20'
          : isDegraded
          ? 'bg-zinc-900/50 border-amber-500/50'
          : 'bg-zinc-900/40 border-zinc-800/80 hover:border-zinc-700 hover:bg-zinc-900/60 hover:shadow-lg hover:shadow-black/30'
      }`}
    >
      {/* Тонкий акцентный верхний кант карточки */}
      <div
        className={`absolute top-0 left-0 right-0 h-[2px] transition-all ${
          isDown
            ? 'bg-gradient-to-r from-rose-500 via-red-500 to-rose-600'
            : isDegraded
            ? 'bg-gradient-to-r from-amber-500 to-amber-600'
            : 'bg-gradient-to-r from-emerald-500/40 via-emerald-400/20 to-transparent opacity-0 group-hover:opacity-100'
        }`}
      />

      <div>
        {/* Top: Name, Target URL & Status Badge */}
        <div className="flex items-start justify-between gap-3 mb-4">
          <div className="flex items-center gap-3 min-w-0">
            <div
              className={`w-9 h-9 rounded-lg flex items-center justify-center border flex-shrink-0 transition-colors ${
                isDown
                  ? 'bg-rose-500/10 border-rose-500/30 text-rose-400'
                  : 'bg-zinc-950 border-zinc-800 text-zinc-300'
              }`}
            >
              <Server className="w-4 h-4" />
            </div>
            <div className="min-w-0 flex-1">
              <h3 className="font-semibold text-zinc-100 text-sm truncate font-mono">
                {node.name}
              </h3>
              <a
                href={node.targetUrl}
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-1 text-[11px] text-zinc-400 hover:text-zinc-200 transition font-mono truncate max-w-full"
              >
                <span className="truncate">{node.targetUrl}</span>
                <ExternalLink className="w-2.5 h-2.5 flex-shrink-0" />
              </a>
            </div>
          </div>

          <button
            type="button"
            onClick={() => isDown && onViewAnalysis(node)}
            disabled={!isDown}
            title={isDown ? 'Click to inspect RCA diagnostics' : 'Node is operational'}
            className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-mono border uppercase tracking-wider transition ${
              isDown
                ? 'bg-rose-500/10 text-rose-400 border-rose-500/30 hover:bg-rose-500/20 cursor-pointer shadow-sm shadow-rose-950/40'
                : isDegraded
                ? 'bg-amber-500/10 text-amber-400 border-amber-500/30'
                : 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20'
            }`}
          >
            {isDown ? (
              <>
                <span className="relative flex h-1.5 w-1.5">
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-rose-400 opacity-75"></span>
                  <span className="relative inline-flex rounded-full h-1.5 w-1.5 bg-rose-500"></span>
                </span>
                Down
              </>
            ) : (
              <>
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-400"></span>
                Healthy
              </>
            )}
          </button>
        </div>

        {/* Telemetry Metrics Grid with Latency Bar */}
        <div
          onClick={() => isDown && onViewAnalysis(node)}
          className={`rounded-lg bg-zinc-950/70 border border-zinc-800/80 p-3.5 text-xs mb-4 ${
            isDown ? 'cursor-pointer hover:border-zinc-700 transition' : ''
          }`}
        >
          <div className="grid grid-cols-2 gap-3">
            <div>
              <span className="text-[10px] font-mono uppercase text-zinc-400 block mb-0.5 tracking-wider">
                Response Time
              </span>
              <span
                className={`font-mono text-xs font-semibold ${
                  isDown ? 'text-rose-400' : 'text-zinc-200'
                }`}
              >
                {isDown ? 'Timeout (504)' : `${latency} ms`}
              </span>
            </div>
            <div>
              <span className="text-[10px] font-mono uppercase text-zinc-400 block mb-0.5 tracking-wider">
                Probe Rate
              </span>
              <span className="font-mono text-xs text-zinc-300">
                {node.checkIntervalSeconds ?? 5}s interval
              </span>
            </div>
          </div>

          {/* Visual Latency Meter Bar */}
          <div className="w-full bg-zinc-900 rounded-full h-1 mt-3 overflow-hidden border border-zinc-800/50">
            <div
              className={`h-full rounded-full transition-all duration-300 ${
                isDown
                  ? 'bg-rose-500 animate-pulse'
                  : latency > 150
                  ? 'bg-amber-400'
                  : 'bg-emerald-500'
              }`}
              style={{ width: `${latencyBarWidth}%` }}
            />
          </div>
        </div>
      </div>

      {/* Action Controls */}
      {isDown ? (
        <div className="grid grid-cols-2 gap-2 pt-2 border-t border-zinc-800/60">
          <button
            type="button"
            onClick={() => onViewAnalysis(node)}
            className="flex items-center justify-center gap-1.5 rounded-lg border border-zinc-700/80 bg-zinc-800/90 hover:bg-zinc-700 hover:border-zinc-600 px-3 py-2 text-xs font-mono text-zinc-200 transition shadow-sm"
          >
            <FileText className="w-3.5 h-3.5 text-zinc-400" />
            Inspect RCA
          </button>
          <button
            type="button"
            onClick={() => onRestoreNode(node.id)}
            className="flex items-center justify-center gap-1.5 rounded-lg border border-emerald-500/30 bg-emerald-500/10 hover:bg-emerald-500/20 px-3 py-2 text-xs font-mono text-emerald-300 transition shadow-sm"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            Restore
          </button>
        </div>
      ) : (
        <button
          type="button"
          onClick={() => onSimulateFailure(node.id)}
          className="w-full flex items-center justify-center gap-1.5 rounded-lg border border-zinc-800/80 bg-zinc-950/80 hover:bg-zinc-900 hover:border-zinc-700 px-3 py-2 text-xs font-mono text-zinc-300 transition shadow-sm"
        >
          Simulate Outage
        </button>
      )}
    </div>
  );
};
